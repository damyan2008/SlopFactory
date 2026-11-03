using System.Text.Json;
using Meth.Grading;
using Meth.Models;

namespace Meth.Validation
{
    /// <summary>
    /// Content rules from docs/question-grading-contract.md §5.1–§5.3 and §6 that the database
    /// cannot check on its own. Runs on every save of a <see cref="Question"/>
    /// (see ApplicationDbContext) and can be called directly by seeding or editing code.
    /// Returns an empty list when the question is valid.
    /// </summary>
    public static class QuestionContentValidator
    {
        public static IReadOnlyList<string> Validate(Question question)
        {
            var errors = new List<string>();

            if (question.ContractVersion != QuestionContract.CurrentVersion)
            {
                errors.Add($"ContractVersion must be {QuestionContract.CurrentVersion}.");
            }

            if (!Enum.IsDefined(question.Type))
            {
                errors.Add("Type is not a supported question type.");
            }

            if (!Enum.IsDefined(question.Difficulty))
            {
                errors.Add("Difficulty must be Easy, Medium or Hard.");
            }

            if (question.MaxPoints < QuestionContract.MinMaxPoints || question.MaxPoints > QuestionContract.MaxMaxPoints)
            {
                errors.Add($"MaxPoints must be between {QuestionContract.MinMaxPoints} and {QuestionContract.MaxMaxPoints}.");
            }

            CheckNotationText(question.Text, "Text", QuestionContract.TextMaxLength, errors);

            CheckStringArray(question.Hints, "Hints",
                0, QuestionContract.MaxHints, QuestionContract.HintMaxLength, errors);

            CheckStringArray(question.Solution, "Solution",
                QuestionContract.MinSolutionSteps, QuestionContract.MaxSolutionSteps,
                QuestionContract.SolutionStepMaxLength, errors);

            if (Enum.IsDefined(question.Type))
            {
                CheckAnswerConfig(question.Type, question.AnswerConfig, errors);
            }

            return errors;
        }

        // ---------------------------------------------------------------- AnswerConfig (§5.2, §5.3)

        private static void CheckAnswerConfig(QuestionType type, string? json, List<string> errors)
        {
            using var document = ParseJson(json, "AnswerConfig", errors);
            if (document is null)
            {
                return;
            }

            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                errors.Add("AnswerConfig must be a JSON object.");
                return;
            }

            string[] allowed = type switch
            {
                QuestionType.SingleChoice => new[] { "options", "correct" },
                QuestionType.Numeric => new[] { "values", "tolerance" },
                QuestionType.ShortText => new[] { "answers", "caseSensitive" },
                _ => Array.Empty<string>()
            };

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in root.EnumerateObject())
            {
                if (!seen.Add(property.Name))
                {
                    errors.Add($"AnswerConfig has a duplicate property '{property.Name}'.");
                }
                else if (Array.IndexOf(allowed, property.Name) < 0)
                {
                    errors.Add($"AnswerConfig has an unknown property '{property.Name}' for {type} questions.");
                }
            }

            switch (type)
            {
                case QuestionType.SingleChoice:
                    CheckSingleChoice(root, errors);
                    break;
                case QuestionType.Numeric:
                    CheckNumeric(root, errors);
                    break;
                case QuestionType.ShortText:
                    CheckShortText(root, errors);
                    break;
            }
        }

        private static void CheckSingleChoice(JsonElement root, List<string> errors)
        {
            var keys = new List<string>();

            if (!root.TryGetProperty("options", out var options) || options.ValueKind != JsonValueKind.Array)
            {
                errors.Add("AnswerConfig.options must be an array.");
            }
            else
            {
                var count = options.GetArrayLength();
                if (count < QuestionContract.MinOptions || count > QuestionContract.MaxOptions)
                {
                    errors.Add($"AnswerConfig.options must contain {QuestionContract.MinOptions} to {QuestionContract.MaxOptions} options.");
                }

                var index = 0;
                foreach (var option in options.EnumerateArray())
                {
                    var label = $"AnswerConfig.options[{index}]";
                    var expectedKey = ((char)('A' + index)).ToString();
                    index++;

                    if (option.ValueKind != JsonValueKind.Object)
                    {
                        errors.Add($"{label} must be an object with 'key' and 'text'.");
                        continue;
                    }

                    foreach (var property in option.EnumerateObject())
                    {
                        if (property.Name is not ("key" or "text"))
                        {
                            errors.Add($"{label} has an unknown property '{property.Name}'.");
                        }
                    }

                    var key = GetString(option, "key");
                    if (key != expectedKey)
                    {
                        errors.Add($"{label}.key must be \"{expectedKey}\" (keys are consecutive letters starting at A).");
                    }
                    else
                    {
                        keys.Add(expectedKey);
                    }

                    CheckNotationText(GetString(option, "text"), $"{label}.text", QuestionContract.OptionTextMaxLength, errors);
                }
            }

            var correct = GetString(root, "correct");
            if (correct is null || !keys.Contains(correct))
            {
                errors.Add("AnswerConfig.correct must be the key of one of the options.");
            }
        }

        private static void CheckNumeric(JsonElement root, List<string> errors)
        {
            if (!root.TryGetProperty("values", out var values) || values.ValueKind != JsonValueKind.Array)
            {
                errors.Add("AnswerConfig.values must be an array of number strings.");
            }
            else
            {
                var count = values.GetArrayLength();
                if (count < 1 || count > QuestionContract.MaxNumericValues)
                {
                    errors.Add($"AnswerConfig.values must contain 1 to {QuestionContract.MaxNumericValues} values.");
                }

                var distinct = new HashSet<NumericValue>();
                var index = 0;
                foreach (var item in values.EnumerateArray())
                {
                    var label = $"AnswerConfig.values[{index}]";
                    index++;

                    if (item.ValueKind != JsonValueKind.String)
                    {
                        errors.Add($"{label} must be a string such as \"3/4\" or \"-2.5\".");
                    }
                    else if (!NumericValue.TryParse(item.GetString(), out var value, out _))
                    {
                        errors.Add($"{label} is not a valid number (integer, decimal or fraction).");
                    }
                    else if (!distinct.Add(value))
                    {
                        errors.Add($"{label} has the same value as an earlier entry.");
                    }
                }
            }

            if (root.TryGetProperty("tolerance", out var tolerance))
            {
                if (tolerance.ValueKind != JsonValueKind.String
                    || !NumericValue.TryParse(tolerance.GetString(), out var toleranceValue, out _)
                    || toleranceValue.IsNegative)
                {
                    errors.Add("AnswerConfig.tolerance must be a non-negative number string such as \"0\" or \"0.1\".");
                }
            }
        }

        private static void CheckShortText(JsonElement root, List<string> errors)
        {
            var caseSensitive = false;
            if (root.TryGetProperty("caseSensitive", out var flag))
            {
                if (flag.ValueKind is JsonValueKind.True or JsonValueKind.False)
                {
                    caseSensitive = flag.GetBoolean();
                }
                else
                {
                    errors.Add("AnswerConfig.caseSensitive must be true or false.");
                }
            }

            if (!root.TryGetProperty("answers", out var answers) || answers.ValueKind != JsonValueKind.Array)
            {
                errors.Add("AnswerConfig.answers must be an array of strings.");
                return;
            }

            var count = answers.GetArrayLength();
            if (count < 1 || count > QuestionContract.MaxShortTextAnswers)
            {
                errors.Add($"AnswerConfig.answers must contain 1 to {QuestionContract.MaxShortTextAnswers} answers.");
            }

            var distinct = new HashSet<string>(StringComparer.Ordinal);
            var index = 0;
            foreach (var item in answers.EnumerateArray())
            {
                var label = $"AnswerConfig.answers[{index}]";
                index++;

                if (item.ValueKind != JsonValueKind.String)
                {
                    errors.Add($"{label} must be a string.");
                    continue;
                }

                var answer = item.GetString()!;
                var normalized = AnswerNormalization.ShortText(answer, caseSensitive);

                if (answer.Trim().Length > QuestionContract.ShortTextAnswerMaxLength)
                {
                    errors.Add($"{label} must be at most {QuestionContract.ShortTextAnswerMaxLength} characters.");
                }
                else if (normalized.Length == 0)
                {
                    errors.Add($"{label} must not be empty.");
                }
                else if (!distinct.Add(normalized))
                {
                    errors.Add($"{label} is the same as an earlier answer after normalisation.");
                }
            }
        }

        // ---------------------------------------------------------------- Hints and Solution (§6)

        private static void CheckStringArray(string? json, string field, int min, int max, int maxLength, List<string> errors)
        {
            using var document = ParseJson(json, field, errors);
            if (document is null)
            {
                return;
            }

            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Array)
            {
                errors.Add($"{field} must be a JSON array of strings.");
                return;
            }

            var count = root.GetArrayLength();
            if (count < min || count > max)
            {
                errors.Add($"{field} must contain {min} to {max} items.");
            }

            var index = 0;
            foreach (var item in root.EnumerateArray())
            {
                var label = $"{field}[{index}]";
                index++;

                if (item.ValueKind != JsonValueKind.String)
                {
                    errors.Add($"{label} must be a string.");
                    continue;
                }

                CheckNotationText(item.GetString(), label, maxLength, errors);
            }
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>
        /// Non-blank, within length, and TeX delimiters balanced: a literal "$" is not allowed,
        /// so every string must contain an even number of "$" characters (contract §6).
        /// </summary>
        private static void CheckNotationText(string? value, string field, int maxLength, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                errors.Add($"{field} must not be empty.");
                return;
            }

            if (value.Length > maxLength)
            {
                errors.Add($"{field} must be at most {maxLength} characters.");
            }

            if (value.Count(c => c == '$') % 2 != 0)
            {
                errors.Add($"{field} has an unclosed $ math delimiter (a literal $ is not allowed; write the currency as text).");
            }
        }

        private static JsonDocument? ParseJson(string? json, string field, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                errors.Add($"{field} is required.");
                return null;
            }

            try
            {
                return JsonDocument.Parse(json);
            }
            catch (JsonException)
            {
                errors.Add($"{field} is not valid JSON.");
                return null;
            }
        }

        private static string? GetString(JsonElement element, string property) =>
            element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
    }
}

namespace Meth.Grading
{
    /// <summary>Validation error codes from docs/question-grading-contract.md §3.4.</summary>
    public static class AnswerErrorCodes
    {
        public const string Blank = "blank";
        public const string NumericFormat = "numeric_format";
        public const string NumericZeroDenominator = "numeric_zero_denominator";
        public const string ChoiceUnknown = "choice_unknown";
        public const string TextTooLong = "text_too_long";
        public const string AnswerTooLong = "answer_too_long";
    }
}

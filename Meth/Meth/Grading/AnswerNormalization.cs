using System.Text;

namespace Meth.Grading
{
    /// <summary>
    /// Text normalisation from docs/question-grading-contract.md §3.2.
    /// Shared by content validation now and by answer parsing/grading later (#43, #44).
    /// </summary>
    public static class AnswerNormalization
    {
        /// <summary>Blank = null, empty or only whitespace (any Unicode whitespace, including NBSP).</summary>
        public static bool IsBlank(string? raw) => string.IsNullOrWhiteSpace(raw);

        /// <summary>
        /// NFC, then U+2212 (−) and U+2013 (–) become "-".
        /// Compatibility forms are deliberately not folded (no NFKC): "7²" must not become "72".
        /// </summary>
        public static string Basic(string value)
        {
            var s = value.Normalize(NormalizationForm.FormC);
            return s.Replace('−', '-').Replace('–', '-');
        }

        /// <summary>
        /// Comparison form of a short_text answer: <see cref="Basic"/>, culture-independent
        /// lower-casing unless <paramref name="caseSensitive"/>, then every whitespace character removed.
        /// </summary>
        public static string ShortText(string value, bool caseSensitive)
        {
            var s = Basic(value);
            if (!caseSensitive)
            {
                s = s.ToLowerInvariant();
            }

            var sb = new StringBuilder(s.Length);
            foreach (var c in s)
            {
                if (!char.IsWhiteSpace(c))
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }
    }
}

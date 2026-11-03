namespace Meth.Models
{
    /// <summary>
    /// A reusable math question, stored exactly as described in
    /// docs/question-grading-contract.md §5.1. The JSON columns are kept as raw strings so that
    /// attempt snapshots (#21) can copy them verbatim.
    /// Content rules that the database cannot express are enforced by
    /// <see cref="Meth.Validation.QuestionContentValidator"/> on every save.
    /// </summary>
    public class Question
    {
        public int Id { get; set; }

        public int TopicId { get; set; }
        public Topic Topic { get; set; } = null!;

        public QuestionType Type { get; set; }

        /// <summary>Question statement: plain text with TeX in $…$ / $$…$$ (contract §6).</summary>
        public string Text { get; set; } = string.Empty;

        public Difficulty Difficulty { get; set; } = Difficulty.Medium;

        /// <summary>Points for a correct answer, 1–20. A question scores 0 or MaxPoints (no partial credit).</summary>
        public int MaxPoints { get; set; } = 1;

        /// <summary>
        /// Answer key and grading settings as JSON; its shape depends on <see cref="Type"/> (contract §5.2):
        /// SingleChoice <c>{"options":[{"key":"A","text":"…"},…],"correct":"B"}</c>,
        /// Numeric <c>{"values":["3/4"],"tolerance":"0"}</c>,
        /// ShortText <c>{"answers":["x+1","1+x"],"caseSensitive":false}</c>.
        /// </summary>
        public string AnswerConfig { get; set; } = "{}";

        /// <summary>JSON array of 0–3 hint strings, gentlest first.</summary>
        public string Hints { get; set; } = "[]";

        /// <summary>JSON array of 1–10 worked-solution steps; the last step states the final answer.</summary>
        public string Solution { get; set; } = "[]";

        /// <summary>Version of the grading contract this content follows.</summary>
        public short ContractVersion { get; set; } = QuestionContract.CurrentVersion;

        public ICollection<TestQuestion> TestQuestions { get; set; } = new List<TestQuestion>();
    }
}

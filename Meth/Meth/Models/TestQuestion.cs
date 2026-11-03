namespace Meth.Models
{
    /// <summary>
    /// Explicit test–question link with a stable position.
    /// A question appears at most once per test, and each position (1, 2, 3, …) is used once per test.
    /// </summary>
    public class TestQuestion
    {
        public int TestId { get; set; }
        public Test Test { get; set; } = null!;

        public int QuestionId { get; set; }
        public Question Question { get; set; } = null!;

        /// <summary>1-based order of the question within the test. Unique per test.</summary>
        public int Position { get; set; }
    }
}

namespace Meth.Models
{
    /// <summary>
    /// A practice test: an ordered list of questions (see <see cref="TestQuestion"/>).
    /// The maximum score is the sum of its questions' <see cref="Question.MaxPoints"/>.
    /// </summary>
    public class Test
    {
        public int Id { get; set; }

        /// <summary>Non-blank, at most <see cref="QuestionContract.TestTitleMaxLength"/> characters.</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>Optional, at most <see cref="QuestionContract.TestDescriptionMaxLength"/> characters.</summary>
        public string? Description { get; set; }

        public Difficulty Difficulty { get; set; } = Difficulty.Medium;

        /// <summary>Only published tests are offered to students.</summary>
        public bool IsPublished { get; set; }

        /// <summary>Set by the database (SYSUTCDATETIME()) when the row is inserted.</summary>
        public DateTime CreatedAtUtc { get; set; }

        /// <summary>The test's questions in order of <see cref="TestQuestion.Position"/>.</summary>
        public ICollection<TestQuestion> Questions { get; set; } = new List<TestQuestion>();
    }
}

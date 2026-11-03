namespace Meth.Models
{
    /// <summary>
    /// A math topic (e.g. "Quadratic equations"). Each question belongs to exactly one topic;
    /// a test's topics are the topics of its questions.
    /// </summary>
    public class Topic
    {
        public int Id { get; set; }

        /// <summary>Unique, non-blank, at most <see cref="QuestionContract.TopicNameMaxLength"/> characters.</summary>
        public string Name { get; set; } = string.Empty;

        public ICollection<Question> Questions { get; set; } = new List<Question>();
    }
}

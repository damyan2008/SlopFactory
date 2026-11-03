namespace Meth.Validation
{
    /// <summary>Thrown by ApplicationDbContext when a question's content breaks the grading contract.</summary>
    public class QuestionContentException : Exception
    {
        public QuestionContentException(IReadOnlyList<string> errors)
            : base("Question content does not follow the grading contract: " + string.Join(" ", errors))
        {
            Errors = errors;
        }

        public IReadOnlyList<string> Errors { get; }
    }
}

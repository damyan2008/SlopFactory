namespace Meth.Models
{
    /// <summary>
    /// Question types supported by contract version 1 (docs/question-grading-contract.md §2).
    /// Stored as the enum name (string) so the database check constraint stays readable.
    /// </summary>
    public enum QuestionType
    {
        SingleChoice,
        Numeric,
        ShortText
    }

    /// <summary>
    /// Difficulty vocabulary (contract §5.1). Stored as its integer value.
    /// </summary>
    public enum Difficulty
    {
        Easy = 1,
        Medium = 2,
        Hard = 3
    }
}

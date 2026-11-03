namespace Meth.Models
{
    /// <summary>
    /// Limits fixed by docs/question-grading-contract.md (contract version 1).
    /// Database mappings, the content validator and (later) answer parsing all read from here.
    /// </summary>
    public static class QuestionContract
    {
        public const short CurrentVersion = 1;

        // Points (§1, §5.1)
        public const int MinMaxPoints = 1;
        public const int MaxMaxPoints = 20;

        // Question content (§5.1, §5.3)
        public const int TextMaxLength = 2000;
        public const int OptionTextMaxLength = 300;
        public const int MinOptions = 2;
        public const int MaxOptions = 6;
        public const int MaxHints = 3;
        public const int HintMaxLength = 500;
        public const int MinSolutionSteps = 1;
        public const int MaxSolutionSteps = 10;
        public const int SolutionStepMaxLength = 1000;
        public const int MaxNumericValues = 10;
        public const int MaxShortTextAnswers = 10;

        // Student input (§2, §3)
        public const int NumericInputMaxLength = 30;
        public const int ShortTextAnswerMaxLength = 100;
        public const int SubmittedAnswerMaxLength = 200;

        // Tests and topics
        public const int TopicNameMaxLength = 100;
        public const int TestTitleMaxLength = 200;
        public const int TestDescriptionMaxLength = 2000;
    }
}

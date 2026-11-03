using Meth.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Meth.Data.Configurations
{
    /// <summary>
    /// Database-level rules from docs/question-grading-contract.md §5.1.
    /// The JSON shape rules (§5.3) are enforced in code by QuestionContentValidator on save;
    /// the checks here are a safety net for anything written outside EF.
    /// </summary>
    public class QuestionConfiguration : IEntityTypeConfiguration<Question>
    {
        public void Configure(EntityTypeBuilder<Question> builder)
        {
            builder.ToTable("Questions", t =>
            {
                t.HasCheckConstraint("CK_Questions_Type",
                    "[Type] IN (N'SingleChoice', N'Numeric', N'ShortText')");

                t.HasCheckConstraint("CK_Questions_Difficulty",
                    "[Difficulty] BETWEEN 1 AND 3");

                t.HasCheckConstraint("CK_Questions_MaxPoints",
                    $"[MaxPoints] BETWEEN {QuestionContract.MinMaxPoints} AND {QuestionContract.MaxMaxPoints}");

                t.HasCheckConstraint("CK_Questions_Text_NotBlank",
                    "LEN([Text]) > 0");

                t.HasCheckConstraint("CK_Questions_ContractVersion",
                    "[ContractVersion] >= 1");

                // A JSON object.
                t.HasCheckConstraint("CK_Questions_AnswerConfig_Json",
                    "CASE WHEN ISJSON([AnswerConfig]) <> 1 THEN 0 " +
                    "WHEN LEFT(LTRIM([AnswerConfig]), 1) <> N'{' THEN 0 " +
                    "ELSE 1 END = 1");

                // A JSON array with at most 3 items.
                t.HasCheckConstraint("CK_Questions_Hints_Json",
                    "CASE WHEN ISJSON([Hints]) <> 1 THEN 0 " +
                    "WHEN LEFT(LTRIM([Hints]), 1) <> N'[' THEN 0 " +
                    $"WHEN JSON_VALUE([Hints], '$[{QuestionContract.MaxHints}]') IS NOT NULL THEN 0 " +
                    "ELSE 1 END = 1");

                // A JSON array with 1 to 10 items.
                t.HasCheckConstraint("CK_Questions_Solution_Json",
                    "CASE WHEN ISJSON([Solution]) <> 1 THEN 0 " +
                    "WHEN LEFT(LTRIM([Solution]), 1) <> N'[' THEN 0 " +
                    "WHEN JSON_VALUE([Solution], '$[0]') IS NULL THEN 0 " +
                    $"WHEN JSON_VALUE([Solution], '$[{QuestionContract.MaxSolutionSteps}]') IS NOT NULL THEN 0 " +
                    "ELSE 1 END = 1");
            });

            builder.HasKey(q => q.Id);

            builder.Property(q => q.Type)
                .IsRequired()
                .HasConversion<string>()
                .HasMaxLength(20);

            builder.Property(q => q.Text)
                .IsRequired()
                .HasMaxLength(QuestionContract.TextMaxLength);

            builder.Property(q => q.Difficulty).IsRequired();

            builder.Property(q => q.MaxPoints).IsRequired();

            builder.Property(q => q.AnswerConfig).IsRequired();   // nvarchar(max)

            builder.Property(q => q.Hints)
                .IsRequired()
                .HasDefaultValue("[]");

            builder.Property(q => q.Solution).IsRequired();

            builder.Property(q => q.ContractVersion)
                .IsRequired()
                .HasDefaultValue(QuestionContract.CurrentVersion);

            builder.HasOne(q => q.Topic)
                .WithMany(t => t.Questions)
                .HasForeignKey(q => q.TopicId)
                .OnDelete(DeleteBehavior.Restrict);

            // Practice browser filters by topic and difficulty (#56).
            builder.HasIndex(q => new { q.TopicId, q.Difficulty });
        }
    }
}

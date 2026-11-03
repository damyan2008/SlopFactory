using Meth.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Meth.Data.Configurations
{
    public class TestQuestionConfiguration : IEntityTypeConfiguration<TestQuestion>
    {
        public void Configure(EntityTypeBuilder<TestQuestion> builder)
        {
            builder.ToTable("TestQuestions", t =>
            {
                t.HasCheckConstraint("CK_TestQuestions_Position", "[Position] >= 1");
            });

            // A question can appear at most once in a test.
            builder.HasKey(tq => new { tq.TestId, tq.QuestionId });

            // Stable ordering: each position is used once per test.
            builder.HasIndex(tq => new { tq.TestId, tq.Position }).IsUnique();

            builder.HasOne(tq => tq.Test)
                .WithMany(t => t.Questions)
                .HasForeignKey(tq => tq.TestId)
                .OnDelete(DeleteBehavior.Cascade);

            // A question that is used by a test cannot be deleted; remove it from the tests first.
            builder.HasOne(tq => tq.Question)
                .WithMany(q => q.TestQuestions)
                .HasForeignKey(tq => tq.QuestionId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}

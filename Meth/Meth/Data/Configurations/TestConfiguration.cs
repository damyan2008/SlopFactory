using Meth.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Meth.Data.Configurations
{
    public class TestConfiguration : IEntityTypeConfiguration<Test>
    {
        public void Configure(EntityTypeBuilder<Test> builder)
        {
            builder.ToTable("Tests", t =>
            {
                t.HasCheckConstraint("CK_Tests_Title_NotBlank", "LEN([Title]) > 0");
                t.HasCheckConstraint("CK_Tests_Difficulty", "[Difficulty] BETWEEN 1 AND 3");
            });

            builder.HasKey(t => t.Id);

            builder.Property(t => t.Title)
                .IsRequired()
                .HasMaxLength(QuestionContract.TestTitleMaxLength);

            builder.Property(t => t.Description)
                .HasMaxLength(QuestionContract.TestDescriptionMaxLength);

            builder.Property(t => t.Difficulty).IsRequired();

            builder.Property(t => t.CreatedAtUtc)
                .HasDefaultValueSql("SYSUTCDATETIME()");

            builder.HasIndex(t => new { t.IsPublished, t.Difficulty });
        }
    }
}

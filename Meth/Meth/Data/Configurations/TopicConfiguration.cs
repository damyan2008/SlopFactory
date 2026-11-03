using Meth.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Meth.Data.Configurations
{
    public class TopicConfiguration : IEntityTypeConfiguration<Topic>
    {
        public void Configure(EntityTypeBuilder<Topic> builder)
        {
            builder.ToTable("Topics", t =>
            {
                t.HasCheckConstraint("CK_Topics_Name_NotBlank", "LEN([Name]) > 0");
            });

            builder.HasKey(t => t.Id);

            builder.Property(t => t.Name)
                .IsRequired()
                .HasMaxLength(QuestionContract.TopicNameMaxLength);

            builder.HasIndex(t => t.Name).IsUnique();
        }
    }
}

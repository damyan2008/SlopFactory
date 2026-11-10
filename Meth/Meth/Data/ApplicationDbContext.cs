using Meth.Models;
using Meth.Validation;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Meth.Data
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext(options)
    {
        public DbSet<Topic> Topics => Set<Topic>();
        public DbSet<Test> Tests => Set<Test>();
        public DbSet<Question> Questions => Set<Question>();
        public DbSet<TestQuestion> TestQuestions => Set<TestQuestion>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);   // Identity tables

            // Match the existing Identity schema even when the context is created without AddDefaultIdentity.
            var logins = builder.Entity<IdentityUserLogin<string>>();
            logins.Property(login => login.LoginProvider).HasMaxLength(128);
            logins.Property(login => login.ProviderKey).HasMaxLength(128);

            var tokens = builder.Entity<IdentityUserToken<string>>();
            tokens.Property(token => token.LoginProvider).HasMaxLength(128);
            tokens.Property(token => token.Name).HasMaxLength(128);

            builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            ValidateQuestionContent();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            ValidateQuestionContent();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        /// <summary>
        /// Rejects any added or modified question whose content breaks the grading contract
        /// (docs/question-grading-contract.md §5.3) before anything reaches the database.
        /// </summary>
        private void ValidateQuestionContent()
        {
            foreach (var entry in ChangeTracker.Entries<Question>())
            {
                if (entry.State is not (EntityState.Added or EntityState.Modified))
                {
                    continue;
                }

                var errors = QuestionContentValidator.Validate(entry.Entity);
                if (errors.Count > 0)
                {
                    throw new QuestionContentException(errors);
                }
            }
        }
    }
}

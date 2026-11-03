using Meth.Models;
using Meth.Validation;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Meth.Tests
{
    /// <summary>
    /// Verification for issue #19: the migration applies to a clean database and a test with
    /// several ordered questions and topics can be saved and reloaded; the database enforces
    /// the ordering, score-range and foreign-key rules.
    /// </summary>
    public class TestsQuestionsTopicsPersistenceTests(SqlServerDatabaseFixture fixture)
        : IClassFixture<SqlServerDatabaseFixture>
    {
        [Fact]
        public void Migrations_apply_to_a_clean_database_and_match_the_model()
        {
            using var db = fixture.CreateContext();

            Assert.Empty(db.Database.GetPendingMigrations());
            Assert.Contains("20261103080000_AddTestsQuestionsTopics", db.Database.GetAppliedMigrations());
            Assert.False(db.Database.HasPendingModelChanges(),
                "The model differs from the migration snapshot: run 'dotnet ef migrations add'.");

            // Identity tables from the earlier migration are still there.
            Assert.Equal(0, db.Users.Count());
        }

        [Fact]
        public void Test_with_ordered_questions_and_topics_round_trips()
        {
            int testId;
            using (var db = fixture.CreateContext())
            {
                var algebra = new Topic { Name = Unique("Linear equations") };
                var fractions = new Topic { Name = Unique("Fractions") };

                var q1 = SingleChoice(algebra, maxPoints: 1);
                var q2 = Numeric(fractions, maxPoints: 2);
                var q3 = ShortText(algebra, maxPoints: 3);

                var test = new Test
                {
                    Title = "DZI warm-up",
                    Description = "Three short questions.",
                    Difficulty = Difficulty.Easy,
                    IsPublished = true
                };

                // Added out of order on purpose: Position, not insertion order, decides the order.
                test.Questions.Add(new TestQuestion { Question = q3, Position = 3 });
                test.Questions.Add(new TestQuestion { Question = q1, Position = 1 });
                test.Questions.Add(new TestQuestion { Question = q2, Position = 2 });

                db.Tests.Add(test);
                db.SaveChanges();
                testId = test.Id;
            }

            using (var db = fixture.CreateContext())
            {
                var test = db.Tests
                    .AsNoTracking()
                    .Include(t => t.Questions.OrderBy(tq => tq.Position))
                        .ThenInclude(tq => tq.Question)
                            .ThenInclude(q => q.Topic)
                    .Single(t => t.Id == testId);

                Assert.Equal("DZI warm-up", test.Title);
                Assert.True(test.IsPublished);
                Assert.NotEqual(default(DateTime), test.CreatedAtUtc);

                Assert.Equal(new[] { 1, 2, 3 }, test.Questions.Select(tq => tq.Position));
                Assert.Equal(
                    new[] { QuestionType.SingleChoice, QuestionType.Numeric, QuestionType.ShortText },
                    test.Questions.Select(tq => tq.Question.Type));
                Assert.Equal(new[] { 1, 2, 3 }, test.Questions.Select(tq => tq.Question.MaxPoints));
                Assert.Equal(6, test.Questions.Sum(tq => tq.Question.MaxPoints));

                var topics = test.Questions.Select(tq => tq.Question.Topic.Name).Distinct().ToList();
                Assert.Equal(2, topics.Count);
                Assert.StartsWith("Linear equations", topics[0]);
                Assert.StartsWith("Fractions", topics[1]);

                var numeric = test.Questions.ElementAt(1).Question;
                Assert.Equal("""{"values":["3/4"],"tolerance":"0"}""", numeric.AnswerConfig);
                Assert.Equal("""["Write 0.75 as a fraction."]""", numeric.Hints);
                Assert.Equal(QuestionContract.CurrentVersion, numeric.ContractVersion);
            }
        }

        [Fact]
        public void Two_questions_cannot_share_a_position_in_a_test()
        {
            using var db = fixture.CreateContext();
            var topic = new Topic { Name = Unique("Positions") };
            var test = new Test { Title = "Duplicate position" };
            test.Questions.Add(new TestQuestion { Question = Numeric(topic, 1), Position = 1 });
            test.Questions.Add(new TestQuestion { Question = ShortText(topic, 1), Position = 1 });
            db.Tests.Add(test);

            var error = Assert.Throws<DbUpdateException>(() => db.SaveChanges());
            Assert.Contains("IX_TestQuestions_TestId_Position", error.InnerException!.Message);
        }

        [Fact]
        public void Position_must_start_at_one()
        {
            using var db = fixture.CreateContext();
            var test = new Test { Title = "Zero position" };
            test.Questions.Add(new TestQuestion { Question = Numeric(new Topic { Name = Unique("Zero") }, 1), Position = 0 });
            db.Tests.Add(test);

            var error = Assert.Throws<DbUpdateException>(() => db.SaveChanges());
            Assert.Contains("CK_TestQuestions_Position", error.InnerException!.Message);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(21)]
        public void MaxPoints_outside_1_to_20_is_rejected_on_save(int maxPoints)
        {
            using var db = fixture.CreateContext();
            db.Questions.Add(Numeric(new Topic { Name = Unique("Points") }, maxPoints));

            var error = Assert.Throws<QuestionContentException>(() => db.SaveChanges());
            Assert.Contains(error.Errors, e => e.StartsWith("MaxPoints"));
        }

        [Theory]
        [InlineData("[MaxPoints]", "21", "CK_Questions_MaxPoints")]
        [InlineData("[Difficulty]", "4", "CK_Questions_Difficulty")]
        [InlineData("[Type]", "N'Essay'", "CK_Questions_Type")]
        [InlineData("[Solution]", "N'[]'", "CK_Questions_Solution_Json")]
        [InlineData("[AnswerConfig]", "N'not json'", "CK_Questions_AnswerConfig_Json")]
        public void Database_rejects_invalid_rows_written_outside_EF(string column, string value, string constraint)
        {
            using var db = fixture.CreateContext();
            var topic = new Topic { Name = Unique("Raw SQL") };
            db.Topics.Add(topic);
            db.SaveChanges();

            var values = new Dictionary<string, string>
            {
                ["[TopicId]"] = topic.Id.ToString(),
                ["[Type]"] = "N'Numeric'",
                ["[Text]"] = "N'What is $1+1$?'",
                ["[Difficulty]"] = "1",
                ["[MaxPoints]"] = "1",
                ["[AnswerConfig]"] = """N'{"values":["2"]}'""",
                ["[Solution]"] = """N'["$1+1=2$"]'"""
            };
            values[column] = value;

            var sql = $"INSERT INTO [Questions] ({string.Join(", ", values.Keys)}) VALUES ({string.Join(", ", values.Values)})";
            var error = Assert.Throws<SqlException>(() => ExecuteSql(sql));
            Assert.Contains(constraint, error.Message);
        }

        [Fact]
        public void Question_needs_an_existing_topic()
        {
            var error = Assert.Throws<SqlException>(() => ExecuteSql(
                """
                INSERT INTO [Questions] ([TopicId], [Type], [Text], [Difficulty], [MaxPoints], [AnswerConfig], [Solution])
                VALUES (-1, N'Numeric', N'x', 1, 1, N'{"values":["2"]}', N'["2"]')
                """));
            Assert.Contains("FK_Questions_Topics_TopicId", error.Message);
        }

        [Fact]
        public void Topic_and_question_in_use_cannot_be_deleted_but_deleting_a_test_removes_its_links()
        {
            int topicId, questionId, testId;
            using (var db = fixture.CreateContext())
            {
                var topic = new Topic { Name = Unique("Delete rules") };
                var question = Numeric(topic, 1);
                var test = new Test { Title = "Delete rules" };
                test.Questions.Add(new TestQuestion { Question = question, Position = 1 });
                db.Tests.Add(test);
                db.SaveChanges();
                (topicId, questionId, testId) = (topic.Id, question.Id, test.Id);
            }

            using (var db = fixture.CreateContext())
            {
                var error = Assert.Throws<SqlException>(() =>
                    db.Database.ExecuteSqlRaw("DELETE FROM [Topics] WHERE [Id] = {0}", topicId));
                Assert.Contains("FK_Questions_Topics_TopicId", error.Message);

                error = Assert.Throws<SqlException>(() =>
                    db.Database.ExecuteSqlRaw("DELETE FROM [Questions] WHERE [Id] = {0}", questionId));
                Assert.Contains("FK_TestQuestions_Questions_QuestionId", error.Message);

                db.Database.ExecuteSqlRaw("DELETE FROM [Tests] WHERE [Id] = {0}", testId);
                Assert.False(db.TestQuestions.Any(tq => tq.TestId == testId));
                Assert.True(db.Questions.Any(q => q.Id == questionId)); // the reusable question survives
            }
        }

        [Fact]
        public void Topic_names_are_unique()
        {
            using var db = fixture.CreateContext();
            var name = Unique("Same name");
            db.Topics.AddRange(new Topic { Name = name }, new Topic { Name = name });

            var error = Assert.Throws<DbUpdateException>(() => db.SaveChanges());
            Assert.Contains("IX_Topics_Name", error.InnerException!.Message);
        }

        // ------------------------------------------------------------ helpers

        /// <summary>
        /// Runs SQL directly over ADO.NET, bypassing EF (and its content validator) the way a
        /// script or another tool would. Not ExecuteSqlRaw: that formats the text and would trip over JSON braces.
        /// </summary>
        private void ExecuteSql(string sql)
        {
            using var connection = new SqlConnection(fixture.ConnectionString);
            connection.Open();
            using var command = new SqlCommand(sql, connection);
            command.ExecuteNonQuery();
        }

        // ------------------------------------------------------------ sample content (contract §9)

        private static string Unique(string name) => $"{name} {Guid.NewGuid():N}"[..Math.Min(name.Length + 33, QuestionContract.TopicNameMaxLength)];

        private static Question SingleChoice(Topic topic, int maxPoints) => new()
        {
            Topic = topic,
            Type = QuestionType.SingleChoice,
            Text = "Solve $2x + 3 = 11$.",
            Difficulty = Difficulty.Easy,
            MaxPoints = maxPoints,
            AnswerConfig = """{"options":[{"key":"A","text":"$x=3$"},{"key":"B","text":"$x=4$"},{"key":"C","text":"$x=7$"}],"correct":"B"}""",
            Hints = """["Subtract 3 from both sides first."]""",
            Solution = """["$2x = 11 - 3 = 8$","$x = 8 / 2 = 4$, so the answer is B."]"""
        };

        private static Question Numeric(Topic topic, int maxPoints) => new()
        {
            Topic = topic,
            Type = QuestionType.Numeric,
            Text = "Write $0.75$ as a number.",
            Difficulty = Difficulty.Medium,
            MaxPoints = maxPoints,
            AnswerConfig = """{"values":["3/4"],"tolerance":"0"}""",
            Hints = """["Write 0.75 as a fraction."]""",
            Solution = """["$0.75 = \\frac{75}{100} = \\frac{3}{4}$"]"""
        };

        private static Question ShortText(Topic topic, int maxPoints) => new()
        {
            Topic = topic,
            Type = QuestionType.ShortText,
            Text = "Simplify $(x^2 - 1)/(x - 1)$ for $x \\neq 1$.",
            Difficulty = Difficulty.Hard,
            MaxPoints = maxPoints,
            AnswerConfig = """{"answers":["x+1","1+x"],"caseSensitive":false}""",
            Solution = """["$x^2 - 1 = (x - 1)(x + 1)$","Cancel $x - 1$: the answer is $x + 1$."]"""
        };
    }
}

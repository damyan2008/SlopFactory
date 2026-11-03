using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Meth.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTestsQuestionsTopics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Tests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Difficulty = table.Column<int>(type: "int", nullable: false),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tests", x => x.Id);
                    table.CheckConstraint("CK_Tests_Difficulty", "[Difficulty] BETWEEN 1 AND 3");
                    table.CheckConstraint("CK_Tests_Title_NotBlank", "LEN([Title]) > 0");
                });

            migrationBuilder.CreateTable(
                name: "Topics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Topics", x => x.Id);
                    table.CheckConstraint("CK_Topics_Name_NotBlank", "LEN([Name]) > 0");
                });

            migrationBuilder.CreateTable(
                name: "Questions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TopicId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Text = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Difficulty = table.Column<int>(type: "int", nullable: false),
                    MaxPoints = table.Column<int>(type: "int", nullable: false),
                    AnswerConfig = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Hints = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "[]"),
                    Solution = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContractVersion = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Questions", x => x.Id);
                    table.CheckConstraint("CK_Questions_AnswerConfig_Json", "CASE WHEN ISJSON([AnswerConfig]) <> 1 THEN 0 WHEN LEFT(LTRIM([AnswerConfig]), 1) <> N'{' THEN 0 ELSE 1 END = 1");
                    table.CheckConstraint("CK_Questions_ContractVersion", "[ContractVersion] >= 1");
                    table.CheckConstraint("CK_Questions_Difficulty", "[Difficulty] BETWEEN 1 AND 3");
                    table.CheckConstraint("CK_Questions_Hints_Json", "CASE WHEN ISJSON([Hints]) <> 1 THEN 0 WHEN LEFT(LTRIM([Hints]), 1) <> N'[' THEN 0 WHEN JSON_VALUE([Hints], '$[3]') IS NOT NULL THEN 0 ELSE 1 END = 1");
                    table.CheckConstraint("CK_Questions_MaxPoints", "[MaxPoints] BETWEEN 1 AND 20");
                    table.CheckConstraint("CK_Questions_Solution_Json", "CASE WHEN ISJSON([Solution]) <> 1 THEN 0 WHEN LEFT(LTRIM([Solution]), 1) <> N'[' THEN 0 WHEN JSON_VALUE([Solution], '$[0]') IS NULL THEN 0 WHEN JSON_VALUE([Solution], '$[10]') IS NOT NULL THEN 0 ELSE 1 END = 1");
                    table.CheckConstraint("CK_Questions_Text_NotBlank", "LEN([Text]) > 0");
                    table.CheckConstraint("CK_Questions_Type", "[Type] IN (N'SingleChoice', N'Numeric', N'ShortText')");
                    table.ForeignKey(
                        name: "FK_Questions_Topics_TopicId",
                        column: x => x.TopicId,
                        principalTable: "Topics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TestQuestions",
                columns: table => new
                {
                    TestId = table.Column<int>(type: "int", nullable: false),
                    QuestionId = table.Column<int>(type: "int", nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestQuestions", x => new { x.TestId, x.QuestionId });
                    table.CheckConstraint("CK_TestQuestions_Position", "[Position] >= 1");
                    table.ForeignKey(
                        name: "FK_TestQuestions_Questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "Questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TestQuestions_Tests_TestId",
                        column: x => x.TestId,
                        principalTable: "Tests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Questions_TopicId_Difficulty",
                table: "Questions",
                columns: new[] { "TopicId", "Difficulty" });

            migrationBuilder.CreateIndex(
                name: "IX_TestQuestions_QuestionId",
                table: "TestQuestions",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_TestQuestions_TestId_Position",
                table: "TestQuestions",
                columns: new[] { "TestId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tests_IsPublished_Difficulty",
                table: "Tests",
                columns: new[] { "IsPublished", "Difficulty" });

            migrationBuilder.CreateIndex(
                name: "IX_Topics_Name",
                table: "Topics",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TestQuestions");

            migrationBuilder.DropTable(
                name: "Questions");

            migrationBuilder.DropTable(
                name: "Tests");

            migrationBuilder.DropTable(
                name: "Topics");
        }
    }
}

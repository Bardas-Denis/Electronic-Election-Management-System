using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Electronic_Election_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class AddFreeTextQuestions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "OptionId",
                table: "Votes",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "TEXT");

            migrationBuilder.AddColumn<string>(
                name: "AnswerText",
                table: "Votes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "QuestionId",
                table: "Votes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QuestionType",
                table: "ElectionQuestions",
                type: "TEXT",
                nullable: false,
                // Existing questions predate this feature and were always Choice questions.
                // The scaffolded default here comes out as "" because the QuestionType ->
                // string conversion isn't applied to the CLR default by the migrations
                // scaffolder - "" is not a valid QuestionType and would throw when the
                // enum converter parses it back, so this is corrected by hand.
                defaultValue: "Choice");

            migrationBuilder.CreateIndex(
                name: "IX_Votes_QuestionId",
                table: "Votes",
                column: "QuestionId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Votes_ExactlyOneAnswerKind",
                table: "Votes",
                sql: "((OptionId IS NOT NULL AND QuestionId IS NULL AND AnswerText IS NULL) OR (OptionId IS NULL AND QuestionId IS NOT NULL AND AnswerText IS NOT NULL))");

            migrationBuilder.AddForeignKey(
                name: "FK_Votes_ElectionQuestions_QuestionId",
                table: "Votes",
                column: "QuestionId",
                principalTable: "ElectionQuestions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Votes_ElectionQuestions_QuestionId",
                table: "Votes");

            migrationBuilder.DropIndex(
                name: "IX_Votes_QuestionId",
                table: "Votes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Votes_ExactlyOneAnswerKind",
                table: "Votes");

            migrationBuilder.DropColumn(
                name: "AnswerText",
                table: "Votes");

            migrationBuilder.DropColumn(
                name: "QuestionId",
                table: "Votes");

            migrationBuilder.DropColumn(
                name: "QuestionType",
                table: "ElectionQuestions");

            migrationBuilder.AlterColumn<Guid>(
                name: "OptionId",
                table: "Votes",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "TEXT",
                oldNullable: true);
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Electronic_Election_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class UpdateCheckConstraintQuoting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Votes_ExactlyOneAnswerKind",
                table: "Votes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Votes_ExactlyOneVoterIdentity",
                table: "Votes");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Votes_ExactlyOneAnswerKind",
                table: "Votes",
                sql: "((\"OptionId\" IS NOT NULL AND \"QuestionId\" IS NULL AND \"AnswerText\" IS NULL) OR (\"OptionId\" IS NULL AND \"QuestionId\" IS NOT NULL AND \"AnswerText\" IS NOT NULL))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Votes_ExactlyOneVoterIdentity",
                table: "Votes",
                sql: "((\"VoteTokenId\" IS NOT NULL AND \"UserId\" IS NULL) OR (\"VoteTokenId\" IS NULL AND \"UserId\" IS NOT NULL))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Votes_ExactlyOneAnswerKind",
                table: "Votes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Votes_ExactlyOneVoterIdentity",
                table: "Votes");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Votes_ExactlyOneAnswerKind",
                table: "Votes",
                sql: "((OptionId IS NOT NULL AND QuestionId IS NULL AND AnswerText IS NULL) OR (OptionId IS NULL AND QuestionId IS NOT NULL AND AnswerText IS NOT NULL))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Votes_ExactlyOneVoterIdentity",
                table: "Votes",
                sql: "((VoteTokenId IS NOT NULL AND UserId IS NULL) OR (VoteTokenId IS NULL AND UserId IS NOT NULL))");
        }
    }
}

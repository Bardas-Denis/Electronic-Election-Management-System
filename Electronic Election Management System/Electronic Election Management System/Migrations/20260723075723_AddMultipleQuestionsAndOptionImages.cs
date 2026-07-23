using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Electronic_Election_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class AddMultipleQuestionsAndOptionImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Votes_VoteTokenId",
                table: "Votes");

            migrationBuilder.AddColumn<string>(
                name: "ImageDataUrl",
                table: "Options",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "QuestionId",
                table: "Options",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ElectionQuestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ElectionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ElectionQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ElectionQuestions_Elections_ElectionId",
                        column: x => x.ElectionId,
                        principalTable: "Elections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Votes_VoteTokenId",
                table: "Votes",
                column: "VoteTokenId");

            migrationBuilder.CreateIndex(
                name: "IX_Options_QuestionId",
                table: "Options",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_ElectionQuestions_ElectionId",
                table: "ElectionQuestions",
                column: "ElectionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Options_ElectionQuestions_QuestionId",
                table: "Options",
                column: "QuestionId",
                principalTable: "ElectionQuestions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Options_ElectionQuestions_QuestionId",
                table: "Options");

            migrationBuilder.DropTable(
                name: "ElectionQuestions");

            migrationBuilder.DropIndex(
                name: "IX_Votes_VoteTokenId",
                table: "Votes");

            migrationBuilder.DropIndex(
                name: "IX_Options_QuestionId",
                table: "Options");

            migrationBuilder.DropColumn(
                name: "ImageDataUrl",
                table: "Options");

            migrationBuilder.DropColumn(
                name: "QuestionId",
                table: "Options");

            migrationBuilder.CreateIndex(
                name: "IX_Votes_VoteTokenId",
                table: "Votes",
                column: "VoteTokenId",
                unique: true);
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Electronic_Election_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class AddScoringSchemes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ScoringSchemeId",
                table: "ElectionQuestions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ScoringSchemes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Points = table.Column<string>(type: "TEXT", nullable: false),
                    IsLinear = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsPredefined = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScoringSchemes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScoringSchemes_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ElectionQuestions_ScoringSchemeId",
                table: "ElectionQuestions",
                column: "ScoringSchemeId");

            migrationBuilder.CreateIndex(
                name: "IX_ScoringSchemes_CreatedByUserId",
                table: "ScoringSchemes",
                column: "CreatedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ElectionQuestions_ScoringSchemes_ScoringSchemeId",
                table: "ElectionQuestions",
                column: "ScoringSchemeId",
                principalTable: "ScoringSchemes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ElectionQuestions_ScoringSchemes_ScoringSchemeId",
                table: "ElectionQuestions");

            migrationBuilder.DropTable(
                name: "ScoringSchemes");

            migrationBuilder.DropIndex(
                name: "IX_ElectionQuestions_ScoringSchemeId",
                table: "ElectionQuestions");

            migrationBuilder.DropColumn(
                name: "ScoringSchemeId",
                table: "ElectionQuestions");
        }
    }
}

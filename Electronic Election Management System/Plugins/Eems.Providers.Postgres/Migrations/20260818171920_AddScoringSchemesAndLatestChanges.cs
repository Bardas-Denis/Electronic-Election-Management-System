using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Electronic_Election_Management_System.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class AddScoringSchemesAndLatestChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ScoringSchemeId",
                table: "ElectionQuestions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ScoringSchemes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Points = table.Column<string>(type: "text", nullable: false),
                    IsLinear = table.Column<bool>(type: "boolean", nullable: false),
                    IsPredefined = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
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

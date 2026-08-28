using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Electronic_Election_Management_System.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class AddScoringSchemePluginKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PluginKey",
                table: "ScoringSchemes",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScoringSchemes_PluginKey",
                table: "ScoringSchemes",
                column: "PluginKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ScoringSchemes_PluginKey",
                table: "ScoringSchemes");

            migrationBuilder.DropColumn(
                name: "PluginKey",
                table: "ScoringSchemes");
        }
    }
}

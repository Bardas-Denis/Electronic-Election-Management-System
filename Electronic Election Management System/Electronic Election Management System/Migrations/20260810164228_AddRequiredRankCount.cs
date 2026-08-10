using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Electronic_Election_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class AddRequiredRankCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RequiredRankCount",
                table: "ElectionQuestions",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequiredRankCount",
                table: "ElectionQuestions");
        }
    }
}

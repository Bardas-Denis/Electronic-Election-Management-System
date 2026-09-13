using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eems.Providers.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddLabelKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "Labels",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Kind",
                table: "Labels");
        }
    }
}

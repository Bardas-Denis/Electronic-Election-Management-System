using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eems.Providers.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueCountryNameIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Labels_CountryName",
                table: "Labels",
                column: "Name",
                unique: true,
                filter: "\"ParentId\" IS NULL AND \"Category\" = 'country'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Labels_CountryName",
                table: "Labels");
        }
    }
}

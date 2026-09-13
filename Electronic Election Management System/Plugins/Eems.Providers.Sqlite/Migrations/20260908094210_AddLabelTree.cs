using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Electronic_Election_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class AddLabelTree : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Labels_Name",
                table: "Labels");

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "Labels",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ParentId",
                table: "Labels",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Labels_Code",
                table: "Labels",
                column: "Code",
                unique: true,
                filter: "\"Code\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Labels_ParentId_Name",
                table: "Labels",
                columns: new[] { "ParentId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Labels_Labels_ParentId",
                table: "Labels",
                column: "ParentId",
                principalTable: "Labels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Labels_Labels_ParentId",
                table: "Labels");

            migrationBuilder.DropIndex(
                name: "IX_Labels_Code",
                table: "Labels");

            migrationBuilder.DropIndex(
                name: "IX_Labels_ParentId_Name",
                table: "Labels");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "Labels");

            migrationBuilder.DropColumn(
                name: "ParentId",
                table: "Labels");

            migrationBuilder.CreateIndex(
                name: "IX_Labels_Name",
                table: "Labels",
                column: "Name",
                unique: true);
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Electronic_Election_Management_System.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class AddElectionImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ImageId",
                table: "Options",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ImageId",
                table: "ElectionQuestions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ElectionImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ElectionId = table.Column<Guid>(type: "uuid", nullable: true),
                    UploadedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ByteSize = table.Column<int>(type: "integer", nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: false),
                    Height = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<byte[]>(type: "bytea", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ElectionImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ElectionImages_Elections_ElectionId",
                        column: x => x.ElectionId,
                        principalTable: "Elections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ElectionImages_Users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Options_ImageId",
                table: "Options",
                column: "ImageId");

            migrationBuilder.CreateIndex(
                name: "IX_ElectionQuestions_ImageId",
                table: "ElectionQuestions",
                column: "ImageId");

            migrationBuilder.CreateIndex(
                name: "IX_ElectionImages_ElectionId",
                table: "ElectionImages",
                column: "ElectionId");

            migrationBuilder.CreateIndex(
                name: "IX_ElectionImages_UploadedByUserId",
                table: "ElectionImages",
                column: "UploadedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ElectionQuestions_ElectionImages_ImageId",
                table: "ElectionQuestions",
                column: "ImageId",
                principalTable: "ElectionImages",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Options_ElectionImages_ImageId",
                table: "Options",
                column: "ImageId",
                principalTable: "ElectionImages",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // bytea defaults to the EXTENDED storage strategy, which tries to compress a value
            // before moving it out of line. Content always holds an already-compressed image,
            // so that pass reclaims nothing and only costs CPU on every write. EXTERNAL keeps
            // the out-of-line storage and skips the compression attempt.
            migrationBuilder.Sql(
                "ALTER TABLE \"ElectionImages\" ALTER COLUMN \"Content\" SET STORAGE EXTERNAL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ElectionQuestions_ElectionImages_ImageId",
                table: "ElectionQuestions");

            migrationBuilder.DropForeignKey(
                name: "FK_Options_ElectionImages_ImageId",
                table: "Options");

            migrationBuilder.DropTable(
                name: "ElectionImages");

            migrationBuilder.DropIndex(
                name: "IX_Options_ImageId",
                table: "Options");

            migrationBuilder.DropIndex(
                name: "IX_ElectionQuestions_ImageId",
                table: "ElectionQuestions");

            migrationBuilder.DropColumn(
                name: "ImageId",
                table: "Options");

            migrationBuilder.DropColumn(
                name: "ImageId",
                table: "ElectionQuestions");
        }
    }
}

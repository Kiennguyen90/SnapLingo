using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SnapLingo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBilingualLessonFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NativeLanguage",
                table: "lessons",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExampleVi",
                table: "lesson_vocabularies",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_lessons_UserId_PhotoId_Emotion_CefrLevel_Status",
                table: "lessons",
                columns: new[] { "UserId", "PhotoId", "Emotion", "CefrLevel", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_lessons_UserId_PhotoId_Emotion_CefrLevel_Status",
                table: "lessons");

            migrationBuilder.DropColumn(
                name: "NativeLanguage",
                table: "lessons");

            migrationBuilder.DropColumn(
                name: "ExampleVi",
                table: "lesson_vocabularies");
        }
    }
}

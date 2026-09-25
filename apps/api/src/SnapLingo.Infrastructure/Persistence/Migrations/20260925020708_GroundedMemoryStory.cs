using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SnapLingo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GroundedMemoryStory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Lessons created before this migration had neither memory nor verification, so
            // they are recorded as the "None" experiment condition rather than an empty string.
            migrationBuilder.AddColumn<bool>(
                name: "PersonLabelingEnabled",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ResearchConsentAt",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SavedAt",
                table: "user_vocabulary_progress",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SavedFromLessonId",
                table: "user_vocabulary_progress",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActivityTag",
                table: "photos",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CapturedAt",
                table: "photos",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "photos",
                type: "character varying(280)",
                maxLength: 280,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UtcOffsetMinutes",
                table: "photos",
                type: "integer",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Emotion",
                table: "lessons",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AddColumn<string>(
                name: "MemoryMode",
                table: "lessons",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "None");

            migrationBuilder.AddColumn<string>(
                name: "VerifierMode",
                table: "lessons",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "None");

            migrationBuilder.AddColumn<string>(
                name: "EvidenceJson",
                table: "lesson_content",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GroundingJson",
                table: "lesson_content",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PatternsJson",
                table: "lesson_content",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "lesson_feedback",
                columns: table => new
                {
                    LessonId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Satisfaction = table.Column<int>(type: "integer", nullable: true),
                    Usefulness = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lesson_feedback", x => x.LessonId);
                    table.CheckConstraint("ck_lesson_feedback_satisfaction", "\"Satisfaction\" IS NULL OR \"Satisfaction\" BETWEEN 1 AND 5");
                    table.CheckConstraint("ck_lesson_feedback_usefulness", "\"Usefulness\" IS NULL OR \"Usefulness\" BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "FK_lesson_feedback_lessons_LessonId",
                        column: x => x.LessonId,
                        principalTable: "lessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "people",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Relation = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_people", x => x.Id);
                    table.ForeignKey(
                        name: "FK_people_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "photo_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PhotoId = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LocalDayOfWeek = table.Column<int>(type: "integer", nullable: false),
                    LocalMinuteOfDay = table.Column<int>(type: "integer", nullable: false),
                    Scene = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PlaceKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ObjectsJson = table.Column<string>(type: "jsonb", nullable: false),
                    ActivitiesJson = table.Column<string>(type: "jsonb", nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    PeopleCount = table.Column<int>(type: "integer", nullable: false),
                    Confidence = table.Column<double>(type: "double precision", nullable: false),
                    Embedding = table.Column<float[]>(type: "real[]", nullable: false),
                    EmbeddingModel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ExtractorModel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_photo_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_photo_events_photos_PhotoId",
                        column: x => x.PhotoId,
                        principalTable: "photos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "photo_people",
                columns: table => new
                {
                    PhotoId = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_photo_people", x => new { x.PhotoId, x.PersonId });
                    table.ForeignKey(
                        name: "FK_photo_people_people_PersonId",
                        column: x => x.PersonId,
                        principalTable: "people",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_photo_people_photos_PhotoId",
                        column: x => x.PhotoId,
                        principalTable: "photos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_vocabulary_progress_SavedFromLessonId",
                table: "user_vocabulary_progress",
                column: "SavedFromLessonId");

            migrationBuilder.CreateIndex(
                name: "IX_lesson_feedback_UserId",
                table: "lesson_feedback",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_people_UserId",
                table: "people",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_photo_events_PhotoId",
                table: "photo_events",
                column: "PhotoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_photo_events_UserId_OccurredAt",
                table: "photo_events",
                columns: new[] { "UserId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_photo_events_UserId_PlaceKey",
                table: "photo_events",
                columns: new[] { "UserId", "PlaceKey" });

            migrationBuilder.CreateIndex(
                name: "IX_photo_people_PersonId",
                table: "photo_people",
                column: "PersonId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lesson_feedback");

            migrationBuilder.DropTable(
                name: "photo_events");

            migrationBuilder.DropTable(
                name: "photo_people");

            migrationBuilder.DropTable(
                name: "people");

            migrationBuilder.DropIndex(
                name: "IX_user_vocabulary_progress_SavedFromLessonId",
                table: "user_vocabulary_progress");

            migrationBuilder.DropColumn(
                name: "PersonLabelingEnabled",
                table: "users");

            migrationBuilder.DropColumn(
                name: "ResearchConsentAt",
                table: "users");

            migrationBuilder.DropColumn(
                name: "SavedAt",
                table: "user_vocabulary_progress");

            migrationBuilder.DropColumn(
                name: "SavedFromLessonId",
                table: "user_vocabulary_progress");

            migrationBuilder.DropColumn(
                name: "ActivityTag",
                table: "photos");

            migrationBuilder.DropColumn(
                name: "CapturedAt",
                table: "photos");

            migrationBuilder.DropColumn(
                name: "Note",
                table: "photos");

            migrationBuilder.DropColumn(
                name: "UtcOffsetMinutes",
                table: "photos");

            migrationBuilder.DropColumn(
                name: "MemoryMode",
                table: "lessons");

            migrationBuilder.DropColumn(
                name: "VerifierMode",
                table: "lessons");

            migrationBuilder.DropColumn(
                name: "EvidenceJson",
                table: "lesson_content");

            migrationBuilder.DropColumn(
                name: "GroundingJson",
                table: "lesson_content");

            migrationBuilder.DropColumn(
                name: "PatternsJson",
                table: "lesson_content");

            migrationBuilder.AlterColumn<string>(
                name: "Emotion",
                table: "lessons",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);
        }
    }
}

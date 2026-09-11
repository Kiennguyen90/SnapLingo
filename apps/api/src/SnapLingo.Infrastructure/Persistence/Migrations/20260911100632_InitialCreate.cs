using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SnapLingo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CefrLevel = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    NativeLanguage = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "vocabularies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Language = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NormalizedWord = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Ipa = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PartOfSpeech = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vocabularies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "photos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ObjectKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    MimeType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TakenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_photos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_photos_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_vocabulary_progress",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    VocabularyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Strength = table.Column<int>(type: "integer", nullable: false),
                    Repetitions = table.Column<int>(type: "integer", nullable: false),
                    NextReviewAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_vocabulary_progress", x => new { x.UserId, x.VocabularyId });
                    table.ForeignKey(
                        name: "FK_user_vocabulary_progress_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_vocabulary_progress_vocabularies_VocabularyId",
                        column: x => x.VocabularyId,
                        principalTable: "vocabularies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lessons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PhotoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Emotion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CefrLevel = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    PromptVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ModelName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lessons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_lessons_photos_PhotoId",
                        column: x => x.PhotoId,
                        principalTable: "photos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_lessons_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ai_generation_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LessonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PromptVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    LatencyMs = table.Column<int>(type: "integer", nullable: false),
                    InputTokens = table.Column<int>(type: "integer", nullable: true),
                    OutputTokens = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ErrorCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_generation_logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ai_generation_logs_lessons_LessonId",
                        column: x => x.LessonId,
                        principalTable: "lessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "audio_assets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LessonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ObjectKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    TextHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Voice = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DurationMs = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audio_assets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_audio_assets_lessons_LessonId",
                        column: x => x.LessonId,
                        principalTable: "lessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "lesson_content",
                columns: table => new
                {
                    LessonId = table.Column<Guid>(type: "uuid", nullable: false),
                    SchemaVersion = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SceneJson = table.Column<string>(type: "jsonb", nullable: true),
                    StoryJson = table.Column<string>(type: "jsonb", nullable: true),
                    DialogueJson = table.Column<string>(type: "jsonb", nullable: true),
                    QuizJson = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lesson_content", x => x.LessonId);
                    table.ForeignKey(
                        name: "FK_lesson_content_lessons_LessonId",
                        column: x => x.LessonId,
                        principalTable: "lessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "lesson_vocabularies",
                columns: table => new
                {
                    LessonId = table.Column<Guid>(type: "uuid", nullable: false),
                    VocabularyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Meaning = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Example = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    VisualAnchor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lesson_vocabularies", x => new { x.LessonId, x.VocabularyId });
                    table.ForeignKey(
                        name: "FK_lesson_vocabularies_lessons_LessonId",
                        column: x => x.LessonId,
                        principalTable: "lessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_lesson_vocabularies_vocabularies_VocabularyId",
                        column: x => x.VocabularyId,
                        principalTable: "vocabularies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "review_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    VocabularyId = table.Column<Guid>(type: "uuid", nullable: false),
                    LessonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Score = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_review_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_review_events_lessons_LessonId",
                        column: x => x.LessonId,
                        principalTable: "lessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_review_events_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_review_events_vocabularies_VocabularyId",
                        column: x => x.VocabularyId,
                        principalTable: "vocabularies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_generation_logs_LessonId_CreatedAt",
                table: "ai_generation_logs",
                columns: new[] { "LessonId", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_audio_assets_LessonId",
                table: "audio_assets",
                column: "LessonId");

            migrationBuilder.CreateIndex(
                name: "IX_audio_assets_TextHash_Voice_Model",
                table: "audio_assets",
                columns: new[] { "TextHash", "Voice", "Model" });

            migrationBuilder.CreateIndex(
                name: "IX_lesson_vocabularies_VocabularyId",
                table: "lesson_vocabularies",
                column: "VocabularyId");

            migrationBuilder.CreateIndex(
                name: "IX_lessons_PhotoId",
                table: "lessons",
                column: "PhotoId");

            migrationBuilder.CreateIndex(
                name: "IX_lessons_UserId_CreatedAt",
                table: "lessons",
                columns: new[] { "UserId", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_photos_UserId_CreatedAt",
                table: "photos",
                columns: new[] { "UserId", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_review_events_LessonId",
                table: "review_events",
                column: "LessonId");

            migrationBuilder.CreateIndex(
                name: "IX_review_events_UserId_ReviewedAt",
                table: "review_events",
                columns: new[] { "UserId", "ReviewedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_review_events_VocabularyId",
                table: "review_events",
                column: "VocabularyId");

            migrationBuilder.CreateIndex(
                name: "IX_user_vocabulary_progress_UserId_NextReviewAt",
                table: "user_vocabulary_progress",
                columns: new[] { "UserId", "NextReviewAt" });

            migrationBuilder.CreateIndex(
                name: "IX_user_vocabulary_progress_VocabularyId",
                table: "user_vocabulary_progress",
                column: "VocabularyId");

            migrationBuilder.CreateIndex(
                name: "IX_users_Email",
                table: "users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vocabularies_Language_NormalizedWord",
                table: "vocabularies",
                columns: new[] { "Language", "NormalizedWord" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_generation_logs");

            migrationBuilder.DropTable(
                name: "audio_assets");

            migrationBuilder.DropTable(
                name: "lesson_content");

            migrationBuilder.DropTable(
                name: "lesson_vocabularies");

            migrationBuilder.DropTable(
                name: "review_events");

            migrationBuilder.DropTable(
                name: "user_vocabulary_progress");

            migrationBuilder.DropTable(
                name: "lessons");

            migrationBuilder.DropTable(
                name: "vocabularies");

            migrationBuilder.DropTable(
                name: "photos");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}

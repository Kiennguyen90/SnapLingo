using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SnapLingo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonGender : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Gender",
                table: "people",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Gender",
                table: "people");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SnapLingo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIsProToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPro",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPro",
                table: "users");
        }
    }
}

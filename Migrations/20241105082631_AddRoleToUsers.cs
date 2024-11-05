using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UserAuthApp.Migrations
{
    /// <inheritdoc />
    public partial class AddRoleToUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Rfid",
                table: "Users",
                newName: "rfid");

            migrationBuilder.AddColumn<string>(
                name: "role",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "role",
                table: "Users");

            migrationBuilder.RenameColumn(
                name: "rfid",
                table: "Users",
                newName: "Rfid");
        }
    }
}

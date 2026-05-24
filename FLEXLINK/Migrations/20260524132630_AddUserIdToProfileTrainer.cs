using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FLEXLINK.Migrations
{
    /// <inheritdoc />
    public partial class AddUserIdToProfileTrainer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "ProfileTrainer",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UserId",
                table: "ProfileTrainer");
        }
    }
}

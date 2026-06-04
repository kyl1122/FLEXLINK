using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FLEXLINK.Migrations
{
    /// <inheritdoc />
    public partial class AddExpertiseToProfileTrainer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Expertise",
                table: "ProfileTrainer",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Expertise",
                table: "ProfileTrainer");
        }
    }
}

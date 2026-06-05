using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FLEXLINK.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingFieldsToTrainerSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsBooked",
                table: "TrainerSchedule",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "BookedByUserId",
                table: "TrainerSchedule",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BookedByName",
                table: "TrainerSchedule",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "BookedAt",
                table: "TrainerSchedule",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsBooked",
                table: "TrainerSchedule");

            migrationBuilder.DropColumn(
                name: "BookedByUserId",
                table: "TrainerSchedule");

            migrationBuilder.DropColumn(
                name: "BookedByName",
                table: "TrainerSchedule");

            migrationBuilder.DropColumn(
                name: "BookedAt",
                table: "TrainerSchedule");
        }
    }
}

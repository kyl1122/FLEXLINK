using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FLEXLINK.Migrations
{
    /// <inheritdoc />
    public partial class AddStatusAndReviewedAtToUserMembership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAt",
                table: "UserMembership",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "UserMembership");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoatSpotFinder.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMarinaAdminRevokedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RevokedAt",
                table: "MarinaAdmins",
                type: "datetimeoffset",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RevokedAt",
                table: "MarinaAdmins");
        }
    }
}

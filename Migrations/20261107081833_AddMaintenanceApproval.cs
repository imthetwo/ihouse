using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ihouse.Migrations
{
    /// <inheritdoc />
    public partial class AddMaintenanceApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovalDecidedAt",
                table: "MaintenanceRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ApprovalStatus",
                table: "MaintenanceRequests",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApprovalDecidedAt",
                table: "MaintenanceRequests");

            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                table: "MaintenanceRequests");
        }
    }
}

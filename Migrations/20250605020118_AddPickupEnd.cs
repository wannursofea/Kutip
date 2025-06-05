using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kutip.Migrations
{
    /// <inheritdoc />
    public partial class AddPickupEnd : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeSpan>(
                name: "s_PickupEnd",
                table: "Schedules",
                type: "time",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "s_PickupEnd",
                table: "Schedules");
        }
    }
}

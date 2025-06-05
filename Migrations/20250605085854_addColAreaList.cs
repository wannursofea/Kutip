using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kutip.Migrations
{
    /// <inheritdoc />
    public partial class addColAreaList : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Binb_ID",
                table: "Schedules",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "l_ColArea",
                table: "Locations",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Schedules_Binb_ID",
                table: "Schedules",
                column: "Binb_ID");

            migrationBuilder.AddForeignKey(
                name: "FK_Schedules_Bins_Binb_ID",
                table: "Schedules",
                column: "Binb_ID",
                principalTable: "Bins",
                principalColumn: "b_ID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Schedules_Bins_Binb_ID",
                table: "Schedules");

            migrationBuilder.DropIndex(
                name: "IX_Schedules_Binb_ID",
                table: "Schedules");

            migrationBuilder.DropColumn(
                name: "Binb_ID",
                table: "Schedules");

            migrationBuilder.DropColumn(
                name: "l_ColArea",
                table: "Locations");
        }
    }
}

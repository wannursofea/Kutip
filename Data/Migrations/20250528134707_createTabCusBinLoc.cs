using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kutip.Data.Migrations
{
    /// <inheritdoc />
    public partial class createTabCusBinLoc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    c_ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    c_Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    c_ContactNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    c_Email = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.c_ID);
                });

            migrationBuilder.CreateTable(
                name: "Locations",
                columns: table => new
                {
                    l_ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    l_Address1 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    l_Address2 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    l_Postcode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    l_District = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    l_State = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Locations", x => x.l_ID);
                });

            migrationBuilder.CreateTable(
                name: "Bins",
                columns: table => new
                {
                    b_ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    b_PlateNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    c_ID = table.Column<int>(type: "int", nullable: false),
                    l_ID = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bins", x => x.b_ID);
                    table.ForeignKey(
                        name: "FK_Bins_Customers_c_ID",
                        column: x => x.c_ID,
                        principalTable: "Customers",
                        principalColumn: "c_ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Bins_Locations_l_ID",
                        column: x => x.l_ID,
                        principalTable: "Locations",
                        principalColumn: "l_ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Bins_c_ID",
                table: "Bins",
                column: "c_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Bins_l_ID",
                table: "Bins",
                column: "l_ID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Bins");

            migrationBuilder.DropTable(
                name: "Customers");

            migrationBuilder.DropTable(
                name: "Locations");
        }
    }
}

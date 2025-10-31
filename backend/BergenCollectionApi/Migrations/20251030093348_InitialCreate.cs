using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BergenCollectionApi.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Stops",
                columns: table => new
                {
                    StopId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    StopName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StopLat = table.Column<double>(type: "float", nullable: false),
                    StopLon = table.Column<double>(type: "float", nullable: false),
                    StopDesc = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LocationType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ParentStation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WheelchairBoarding = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VehicleType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PlatformCode = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stops", x => x.StopId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Stops");
        }
    }
}

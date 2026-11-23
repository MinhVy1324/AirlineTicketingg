using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AirlineTicketing.DAL.Migrations
{
    /// <inheritdoc />
    public partial class BookingPromotions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "GiamGia",
                table: "DatVes",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "MaKhuyenMai",
                table: "DatVes",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_DatVe_GiamGia",
                table: "DatVes",
                sql: "\"GiamGia\" BETWEEN 0 AND \"PhiDichVu\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_DatVe_GiamGia",
                table: "DatVes");

            migrationBuilder.DropColumn(
                name: "GiamGia",
                table: "DatVes");

            migrationBuilder.DropColumn(
                name: "MaKhuyenMai",
                table: "DatVes");
        }
    }
}

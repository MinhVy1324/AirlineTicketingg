using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AirlineTicketing.DAL.Migrations
{
    /// <inheritdoc />
    public partial class DemoAfterSales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_HoanTiens_YeuCauDoiHoanId",
                table: "HoanTiens");

            migrationBuilder.AddColumn<int>(
                name: "ChuyenBayGocId",
                table: "YeuCauDoiHoans",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GiaVeMoi",
                table: "YeuCauDoiHoans",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PhiDoiDaThu",
                table: "DatVes",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_HoanTiens_YeuCauDoiHoanId_ThanhToanId",
                table: "HoanTiens",
                columns: new[] { "YeuCauDoiHoanId", "ThanhToanId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_HoanTiens_YeuCauDoiHoanId_ThanhToanId",
                table: "HoanTiens");

            migrationBuilder.DropColumn(
                name: "ChuyenBayGocId",
                table: "YeuCauDoiHoans");

            migrationBuilder.DropColumn(
                name: "GiaVeMoi",
                table: "YeuCauDoiHoans");

            migrationBuilder.DropColumn(
                name: "PhiDoiDaThu",
                table: "DatVes");

            migrationBuilder.CreateIndex(
                name: "IX_HoanTiens_YeuCauDoiHoanId",
                table: "HoanTiens",
                column: "YeuCauDoiHoanId",
                unique: true);
        }
    }
}

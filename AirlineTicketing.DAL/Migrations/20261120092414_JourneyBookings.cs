using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AirlineTicketing.DAL.Migrations
{
    /// <inheritdoc />
    public partial class JourneyBookings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Ves_ChuyenBayId_SoGhe",
                table: "Ves");

            migrationBuilder.AddColumn<string>(
                name: "LoaiHanhKhach",
                table: "Ves",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "NguoiLon");

            migrationBuilder.AddColumn<int>(
                name: "NguoiDiKemId",
                table: "Ves",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PhiChoNgoi",
                table: "Ves",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PhiDichVu",
                table: "Ves",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PhiHanhLy",
                table: "Ves",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ThuePhi",
                table: "Ves",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "TenLienHe",
                table: "DatVes",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ves_ChuyenBayId_SoGhe",
                table: "Ves",
                columns: new[] { "ChuyenBayId", "SoGhe" },
                unique: true,
                filter: "\"SoGhe\" <> '' AND \"TrangThai\" IN ('TamGiu', 'DaPhatHanh')");

            migrationBuilder.CreateIndex(
                name: "IX_Ves_NguoiDiKemId",
                table: "Ves",
                column: "NguoiDiKemId");

            migrationBuilder.AddForeignKey(
                name: "FK_Ves_HanhKhachs_NguoiDiKemId",
                table: "Ves",
                column: "NguoiDiKemId",
                principalTable: "HanhKhachs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Ves_HanhKhachs_NguoiDiKemId",
                table: "Ves");

            migrationBuilder.DropIndex(
                name: "IX_Ves_ChuyenBayId_SoGhe",
                table: "Ves");

            migrationBuilder.DropIndex(
                name: "IX_Ves_NguoiDiKemId",
                table: "Ves");

            migrationBuilder.DropColumn(
                name: "LoaiHanhKhach",
                table: "Ves");

            migrationBuilder.DropColumn(
                name: "NguoiDiKemId",
                table: "Ves");

            migrationBuilder.DropColumn(
                name: "PhiChoNgoi",
                table: "Ves");

            migrationBuilder.DropColumn(
                name: "PhiDichVu",
                table: "Ves");

            migrationBuilder.DropColumn(
                name: "PhiHanhLy",
                table: "Ves");

            migrationBuilder.DropColumn(
                name: "ThuePhi",
                table: "Ves");

            migrationBuilder.DropColumn(
                name: "TenLienHe",
                table: "DatVes");

            migrationBuilder.CreateIndex(
                name: "IX_Ves_ChuyenBayId_SoGhe",
                table: "Ves",
                columns: new[] { "ChuyenBayId", "SoGhe" },
                unique: true,
                filter: "\"TrangThai\" IN ('TamGiu', 'DaPhatHanh')");
        }
    }
}

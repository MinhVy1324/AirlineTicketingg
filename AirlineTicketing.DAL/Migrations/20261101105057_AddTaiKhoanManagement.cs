using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AirlineTicketing.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddTaiKhoanManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TaiKhoans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenDangNhap = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    MatKhauHash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    HoTen = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    SoDienThoai = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    VaiTro = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TrangThai = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NgayTao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    LanDangNhapCuoi = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    KhachHangId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaiKhoans", x => x.Id);
                    table.CheckConstraint("CK_TaiKhoan_TrangThai", "\"TrangThai\" IN ('HoatDong', 'BiKhoa')");
                    table.CheckConstraint("CK_TaiKhoan_VaiTro", "\"VaiTro\" IN ('Admin', 'NhanVien', 'KhachHang')");
                    table.ForeignKey(
                        name: "FK_TaiKhoans_KhachHangs_KhachHangId",
                        column: x => x.KhachHangId,
                        principalTable: "KhachHangs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TaiKhoans_Email",
                table: "TaiKhoans",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaiKhoans_KhachHangId",
                table: "TaiKhoans",
                column: "KhachHangId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaiKhoans_TenDangNhap",
                table: "TaiKhoans",
                column: "TenDangNhap",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaiKhoans_VaiTro",
                table: "TaiKhoans",
                column: "VaiTro");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TaiKhoans");
        }
    }
}

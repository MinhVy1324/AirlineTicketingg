using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AirlineTicketing.DAL.Migrations
{
    /// <inheritdoc />
    public partial class NghiepVuCoBan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "HanhLyKyGuiKg",
                table: "Ves",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "HanhLyXachTayKg",
                table: "Ves",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NgayPhatHanh",
                table: "Ves",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PhiDoi",
                table: "Ves",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PhiHoan",
                table: "Ves",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "MuiGio",
                table: "SanBays",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "UTC+7");

            migrationBuilder.AddColumn<int>(
                name: "NamKhaiThac",
                table: "MayBays",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "HanhKhachs",
                type: "character varying(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GioiTinh",
                table: "HanhKhachs",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LoaiHanhKhach",
                table: "HanhKhachs",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SoDienThoai",
                table: "HanhKhachs",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmailLienHe",
                table: "DatVes",
                type: "character varying(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GhiChu",
                table: "DatVes",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "HanGiuCho",
                table: "DatVes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NgayXacNhan",
                table: "DatVes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PhiDichVu",
                table: "DatVes",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PhiHanhLy",
                table: "DatVes",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "SoDienThoaiLienHe",
                table: "DatVes",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ThuePhi",
                table: "DatVes",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "HangHangKhongId",
                table: "ChuyenBays",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "CauHinhHeThongs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Khoa = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    GiaTri = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Nhom = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    MoTa = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    KieuDuLieu = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    NgayCapNhat = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    NguoiCapNhat = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CauHinhHeThongs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HangHangKhongS",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MaHang = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    TenHang = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    QuocGia = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MaIcao = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    DangHoatDong = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HangHangKhongS", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HangVes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChuyenBayId = table.Column<int>(type: "integer", nullable: false),
                    MaHang = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    TenHang = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NhomHang = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    GiaCoBan = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    SoGhe = table.Column<int>(type: "integer", nullable: false),
                    SoGheDaBan = table.Column<int>(type: "integer", nullable: false),
                    HanhLyXachTayKg = table.Column<int>(type: "integer", nullable: false),
                    HanhLyKyGuiKg = table.Column<int>(type: "integer", nullable: false),
                    PhiDoi = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    PhiHoan = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    DangMoBan = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HangVes", x => x.Id);
                    table.CheckConstraint("CK_HangVe_Cho", "\"SoGhe\" > 0 AND \"SoGheDaBan\" >= 0 AND \"SoGheDaBan\" <= \"SoGhe\"");
                    table.CheckConstraint("CK_HangVe_Gia", "\"GiaCoBan\" >= 0");
                    table.CheckConstraint("CK_HangVe_HanhLy", "\"HanhLyXachTayKg\" >= 0 AND \"HanhLyKyGuiKg\" >= 0");
                    table.CheckConstraint("CK_HangVe_Phi", "\"PhiDoi\" >= 0 AND \"PhiHoan\" >= 0");
                    table.ForeignKey(
                        name: "FK_HangVes_ChuyenBays_ChuyenBayId",
                        column: x => x.ChuyenBayId,
                        principalTable: "ChuyenBays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TaiKhoans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenDangNhap = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    MatKhauHash = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    HoTen = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    SoDienThoai = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    VaiTro = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TrangThai = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MaNhanSu = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    NgayTao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    LanDangNhapCuoi = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    GhiChu = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    KhachHangId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaiKhoans", x => x.Id);
                    table.CheckConstraint("CK_TaiKhoan_TrangThai", "\"TrangThai\" IN ('HoatDong', 'BiKhoa')");
                    table.CheckConstraint("CK_TaiKhoan_VaiTro", "\"VaiTro\" IN ('Admin', 'Manager', 'KhachHang')");
                    table.ForeignKey(
                        name: "FK_TaiKhoans_KhachHangs_KhachHangId",
                        column: x => x.KhachHangId,
                        principalTable: "KhachHangs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "NhatKyHoatDongs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MaNhatKy = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TaiKhoanId = table.Column<int>(type: "integer", nullable: true),
                    TenNguoiDung = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    VaiTro = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    HanhDong = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DoiTuong = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    KetQua = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ChiTiet = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DiaChiIp = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ThoiGian = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NhatKyHoatDongs", x => x.Id);
                    table.CheckConstraint("CK_NhatKy_KetQua", "\"KetQua\" IN ('ThanhCong', 'ThatBai')");
                    table.ForeignKey(
                        name: "FK_NhatKyHoatDongs_TaiKhoans_TaiKhoanId",
                        column: x => x.TaiKhoanId,
                        principalTable: "TaiKhoans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "YeuCauDoiHoans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MaYeuCau = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DatVeId = table.Column<int>(type: "integer", nullable: false),
                    LoaiYeuCau = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    LyDo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ThongTinBoSung = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    TrangThai = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PhiDoi = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    PhiHoan = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    PhiDichVuKhongHoan = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    PhiHanhLyKhongHoan = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    SoTienHoan = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    PhuongThucHoan = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ChuyenBayMoiId = table.Column<int>(type: "integer", nullable: true),
                    NgayTao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    NgayXuLy = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NguoiXuLyId = table.Column<int>(type: "integer", nullable: true),
                    GhiChuXuLy = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YeuCauDoiHoans", x => x.Id);
                    table.CheckConstraint("CK_YeuCau_Loai", "\"LoaiYeuCau\" IN ('DoiVe', 'HoanVe')");
                    table.CheckConstraint("CK_YeuCau_Phi", "\"PhiDoi\" >= 0 AND \"PhiHoan\" >= 0 AND \"PhiDichVuKhongHoan\" >= 0 AND \"PhiHanhLyKhongHoan\" >= 0 AND \"SoTienHoan\" >= 0");
                    table.CheckConstraint("CK_YeuCau_TrangThai", "\"TrangThai\" IN ('ChoDuyet', 'DaDuyet', 'TuChoi', 'HoanTat')");
                    table.ForeignKey(
                        name: "FK_YeuCauDoiHoans_ChuyenBays_ChuyenBayMoiId",
                        column: x => x.ChuyenBayMoiId,
                        principalTable: "ChuyenBays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_YeuCauDoiHoans_DatVes_DatVeId",
                        column: x => x.DatVeId,
                        principalTable: "DatVes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_YeuCauDoiHoans_TaiKhoans_NguoiXuLyId",
                        column: x => x.NguoiXuLyId,
                        principalTable: "TaiKhoans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Ve_HanhLy",
                table: "Ves",
                sql: "\"HanhLyXachTayKg\" >= 0 AND \"HanhLyKyGuiKg\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Ve_Phi",
                table: "Ves",
                sql: "\"PhiDoi\" >= 0 AND \"PhiHoan\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MayBay_NamKhaiThac",
                table: "MayBays",
                sql: "\"NamKhaiThac\" IS NULL OR \"NamKhaiThac\" >= 1950");

            migrationBuilder.CreateIndex(
                name: "IX_DatVes_HanGiuCho",
                table: "DatVes",
                column: "HanGiuCho");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DatVe_Phi",
                table: "DatVes",
                sql: "\"ThuePhi\" >= 0 AND \"PhiDichVu\" >= 0 AND \"PhiHanhLy\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_ChuyenBays_HangHangKhongId_GioKhoiHanh",
                table: "ChuyenBays",
                columns: new[] { "HangHangKhongId", "GioKhoiHanh" });

            migrationBuilder.CreateIndex(
                name: "IX_CauHinhHeThongs_Khoa",
                table: "CauHinhHeThongs",
                column: "Khoa",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CauHinhHeThongs_Nhom",
                table: "CauHinhHeThongs",
                column: "Nhom");

            migrationBuilder.CreateIndex(
                name: "IX_HangHangKhongS_MaHang",
                table: "HangHangKhongS",
                column: "MaHang",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HangVes_ChuyenBayId_MaHang",
                table: "HangVes",
                columns: new[] { "ChuyenBayId", "MaHang" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NhatKyHoatDongs_MaNhatKy",
                table: "NhatKyHoatDongs",
                column: "MaNhatKy",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NhatKyHoatDongs_TaiKhoanId",
                table: "NhatKyHoatDongs",
                column: "TaiKhoanId");

            migrationBuilder.CreateIndex(
                name: "IX_NhatKyHoatDongs_ThoiGian",
                table: "NhatKyHoatDongs",
                column: "ThoiGian");

            migrationBuilder.CreateIndex(
                name: "IX_NhatKyHoatDongs_VaiTro_ThoiGian",
                table: "NhatKyHoatDongs",
                columns: new[] { "VaiTro", "ThoiGian" });

            migrationBuilder.CreateIndex(
                name: "IX_TaiKhoans_Email",
                table: "TaiKhoans",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaiKhoans_KhachHangId",
                table: "TaiKhoans",
                column: "KhachHangId");

            migrationBuilder.CreateIndex(
                name: "IX_TaiKhoans_TenDangNhap",
                table: "TaiKhoans",
                column: "TenDangNhap",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_YeuCauDoiHoans_ChuyenBayMoiId",
                table: "YeuCauDoiHoans",
                column: "ChuyenBayMoiId");

            migrationBuilder.CreateIndex(
                name: "IX_YeuCauDoiHoans_DatVeId_LoaiYeuCau",
                table: "YeuCauDoiHoans",
                columns: new[] { "DatVeId", "LoaiYeuCau" });

            migrationBuilder.CreateIndex(
                name: "IX_YeuCauDoiHoans_MaYeuCau",
                table: "YeuCauDoiHoans",
                column: "MaYeuCau",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_YeuCauDoiHoans_NguoiXuLyId",
                table: "YeuCauDoiHoans",
                column: "NguoiXuLyId");

            migrationBuilder.CreateIndex(
                name: "IX_YeuCauDoiHoans_TrangThai_NgayTao",
                table: "YeuCauDoiHoans",
                columns: new[] { "TrangThai", "NgayTao" });

            migrationBuilder.AddForeignKey(
                name: "FK_ChuyenBays_HangHangKhongS_HangHangKhongId",
                table: "ChuyenBays",
                column: "HangHangKhongId",
                principalTable: "HangHangKhongS",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChuyenBays_HangHangKhongS_HangHangKhongId",
                table: "ChuyenBays");

            migrationBuilder.DropTable(
                name: "CauHinhHeThongs");

            migrationBuilder.DropTable(
                name: "HangHangKhongS");

            migrationBuilder.DropTable(
                name: "HangVes");

            migrationBuilder.DropTable(
                name: "NhatKyHoatDongs");

            migrationBuilder.DropTable(
                name: "YeuCauDoiHoans");

            migrationBuilder.DropTable(
                name: "TaiKhoans");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Ve_HanhLy",
                table: "Ves");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Ve_Phi",
                table: "Ves");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MayBay_NamKhaiThac",
                table: "MayBays");

            migrationBuilder.DropIndex(
                name: "IX_DatVes_HanGiuCho",
                table: "DatVes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DatVe_Phi",
                table: "DatVes");

            migrationBuilder.DropIndex(
                name: "IX_ChuyenBays_HangHangKhongId_GioKhoiHanh",
                table: "ChuyenBays");

            migrationBuilder.DropColumn(
                name: "HanhLyKyGuiKg",
                table: "Ves");

            migrationBuilder.DropColumn(
                name: "HanhLyXachTayKg",
                table: "Ves");

            migrationBuilder.DropColumn(
                name: "NgayPhatHanh",
                table: "Ves");

            migrationBuilder.DropColumn(
                name: "PhiDoi",
                table: "Ves");

            migrationBuilder.DropColumn(
                name: "PhiHoan",
                table: "Ves");

            migrationBuilder.DropColumn(
                name: "MuiGio",
                table: "SanBays");

            migrationBuilder.DropColumn(
                name: "NamKhaiThac",
                table: "MayBays");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "HanhKhachs");

            migrationBuilder.DropColumn(
                name: "GioiTinh",
                table: "HanhKhachs");

            migrationBuilder.DropColumn(
                name: "LoaiHanhKhach",
                table: "HanhKhachs");

            migrationBuilder.DropColumn(
                name: "SoDienThoai",
                table: "HanhKhachs");

            migrationBuilder.DropColumn(
                name: "EmailLienHe",
                table: "DatVes");

            migrationBuilder.DropColumn(
                name: "GhiChu",
                table: "DatVes");

            migrationBuilder.DropColumn(
                name: "HanGiuCho",
                table: "DatVes");

            migrationBuilder.DropColumn(
                name: "NgayXacNhan",
                table: "DatVes");

            migrationBuilder.DropColumn(
                name: "PhiDichVu",
                table: "DatVes");

            migrationBuilder.DropColumn(
                name: "PhiHanhLy",
                table: "DatVes");

            migrationBuilder.DropColumn(
                name: "SoDienThoaiLienHe",
                table: "DatVes");

            migrationBuilder.DropColumn(
                name: "ThuePhi",
                table: "DatVes");

            migrationBuilder.DropColumn(
                name: "HangHangKhongId",
                table: "ChuyenBays");
        }
    }
}

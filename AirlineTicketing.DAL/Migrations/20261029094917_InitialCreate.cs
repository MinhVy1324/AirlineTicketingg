using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AirlineTicketing.DAL.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HanhKhachs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HoTen = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    NgaySinh = table.Column<DateOnly>(type: "date", nullable: true),
                    QuocTich = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SoHoChieu = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HanhKhachs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KhachHangs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HoTen = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    SoDienThoai = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NgayTao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KhachHangs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MayBays",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MaMayBay = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TenMayBay = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    HangSanXuat = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SoGhe = table.Column<int>(type: "integer", nullable: false),
                    DangHoatDong = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MayBays", x => x.Id);
                    table.CheckConstraint("CK_MayBay_SoGhe", "\"SoGhe\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "SanBays",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MaSanBay = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    TenSanBay = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ThanhPho = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    QuocGia = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DangHoatDong = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SanBays", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DatVes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MaDatVe = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    KhachHangId = table.Column<int>(type: "integer", nullable: false),
                    NgayDat = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    TrangThai = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TongTien = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    DonViTienTe = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DatVes", x => x.Id);
                    table.CheckConstraint("CK_DatVe_TongTien", "\"TongTien\" >= 0");
                    table.ForeignKey(
                        name: "FK_DatVes_KhachHangs_KhachHangId",
                        column: x => x.KhachHangId,
                        principalTable: "KhachHangs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TuyenBays",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SanBayDiId = table.Column<int>(type: "integer", nullable: false),
                    SanBayDenId = table.Column<int>(type: "integer", nullable: false),
                    ThoiLuongDuKienPhut = table.Column<int>(type: "integer", nullable: true),
                    DangKhaiThac = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TuyenBays", x => x.Id);
                    table.CheckConstraint("CK_TuyenBay_AirportsDiffer", "\"SanBayDiId\" <> \"SanBayDenId\"");
                    table.CheckConstraint("CK_TuyenBay_Duration", "\"ThoiLuongDuKienPhut\" IS NULL OR \"ThoiLuongDuKienPhut\" > 0");
                    table.ForeignKey(
                        name: "FK_TuyenBays_SanBays_SanBayDenId",
                        column: x => x.SanBayDenId,
                        principalTable: "SanBays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TuyenBays_SanBays_SanBayDiId",
                        column: x => x.SanBayDiId,
                        principalTable: "SanBays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ThanhToans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DatVeId = table.Column<int>(type: "integer", nullable: false),
                    SoTien = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    DonViTienTe = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    PhuongThuc = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TrangThai = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MaGiaoDich = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    NgayTao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    NgayHoanTat = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThanhToans", x => x.Id);
                    table.CheckConstraint("CK_ThanhToan_SoTien", "\"SoTien\" >= 0");
                    table.ForeignKey(
                        name: "FK_ThanhToans_DatVes_DatVeId",
                        column: x => x.DatVeId,
                        principalTable: "DatVes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ChuyenBays",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SoHieu = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TuyenBayId = table.Column<int>(type: "integer", nullable: false),
                    MayBayId = table.Column<int>(type: "integer", nullable: false),
                    GioKhoiHanh = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    GioHaCanh = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    GiaCoBan = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    TrangThai = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChuyenBays", x => x.Id);
                    table.CheckConstraint("CK_ChuyenBay_GiaCoBan", "\"GiaCoBan\" >= 0");
                    table.CheckConstraint("CK_ChuyenBay_TimeRange", "\"GioHaCanh\" > \"GioKhoiHanh\"");
                    table.ForeignKey(
                        name: "FK_ChuyenBays_MayBays_MayBayId",
                        column: x => x.MayBayId,
                        principalTable: "MayBays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChuyenBays_TuyenBays_TuyenBayId",
                        column: x => x.TuyenBayId,
                        principalTable: "TuyenBays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Ves",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MaVe = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ChuyenBayId = table.Column<int>(type: "integer", nullable: false),
                    DatVeId = table.Column<int>(type: "integer", nullable: false),
                    HanhKhachId = table.Column<int>(type: "integer", nullable: false),
                    SoGhe = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    HangVe = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    GiaVe = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    TrangThai = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ves", x => x.Id);
                    table.CheckConstraint("CK_Ve_GiaVe", "\"GiaVe\" >= 0");
                    table.ForeignKey(
                        name: "FK_Ves_ChuyenBays_ChuyenBayId",
                        column: x => x.ChuyenBayId,
                        principalTable: "ChuyenBays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Ves_DatVes_DatVeId",
                        column: x => x.DatVeId,
                        principalTable: "DatVes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Ves_HanhKhachs_HanhKhachId",
                        column: x => x.HanhKhachId,
                        principalTable: "HanhKhachs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChuyenBays_MayBayId_GioKhoiHanh",
                table: "ChuyenBays",
                columns: new[] { "MayBayId", "GioKhoiHanh" });

            migrationBuilder.CreateIndex(
                name: "IX_ChuyenBays_SoHieu_GioKhoiHanh",
                table: "ChuyenBays",
                columns: new[] { "SoHieu", "GioKhoiHanh" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChuyenBays_TuyenBayId_GioKhoiHanh",
                table: "ChuyenBays",
                columns: new[] { "TuyenBayId", "GioKhoiHanh" });

            migrationBuilder.CreateIndex(
                name: "IX_DatVes_KhachHangId_NgayDat",
                table: "DatVes",
                columns: new[] { "KhachHangId", "NgayDat" });

            migrationBuilder.CreateIndex(
                name: "IX_DatVes_MaDatVe",
                table: "DatVes",
                column: "MaDatVe",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HanhKhachs_SoHoChieu",
                table: "HanhKhachs",
                column: "SoHoChieu",
                unique: true,
                filter: "\"SoHoChieu\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_KhachHangs_Email",
                table: "KhachHangs",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_KhachHangs_SoDienThoai",
                table: "KhachHangs",
                column: "SoDienThoai");

            migrationBuilder.CreateIndex(
                name: "IX_MayBays_MaMayBay",
                table: "MayBays",
                column: "MaMayBay",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SanBays_MaSanBay",
                table: "SanBays",
                column: "MaSanBay",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ThanhToans_DatVeId_NgayTao",
                table: "ThanhToans",
                columns: new[] { "DatVeId", "NgayTao" });

            migrationBuilder.CreateIndex(
                name: "IX_ThanhToans_MaGiaoDich",
                table: "ThanhToans",
                column: "MaGiaoDich",
                unique: true,
                filter: "\"MaGiaoDich\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TuyenBays_SanBayDenId",
                table: "TuyenBays",
                column: "SanBayDenId");

            migrationBuilder.CreateIndex(
                name: "IX_TuyenBays_SanBayDiId_SanBayDenId",
                table: "TuyenBays",
                columns: new[] { "SanBayDiId", "SanBayDenId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ves_ChuyenBayId_HanhKhachId",
                table: "Ves",
                columns: new[] { "ChuyenBayId", "HanhKhachId" },
                unique: true,
                filter: "\"TrangThai\" IN ('TamGiu', 'DaPhatHanh')");

            migrationBuilder.CreateIndex(
                name: "IX_Ves_ChuyenBayId_SoGhe",
                table: "Ves",
                columns: new[] { "ChuyenBayId", "SoGhe" },
                unique: true,
                filter: "\"TrangThai\" IN ('TamGiu', 'DaPhatHanh')");

            migrationBuilder.CreateIndex(
                name: "IX_Ves_DatVeId_TrangThai",
                table: "Ves",
                columns: new[] { "DatVeId", "TrangThai" });

            migrationBuilder.CreateIndex(
                name: "IX_Ves_HanhKhachId",
                table: "Ves",
                column: "HanhKhachId");

            migrationBuilder.CreateIndex(
                name: "IX_Ves_MaVe",
                table: "Ves",
                column: "MaVe",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ThanhToans");

            migrationBuilder.DropTable(
                name: "Ves");

            migrationBuilder.DropTable(
                name: "ChuyenBays");

            migrationBuilder.DropTable(
                name: "DatVes");

            migrationBuilder.DropTable(
                name: "HanhKhachs");

            migrationBuilder.DropTable(
                name: "MayBays");

            migrationBuilder.DropTable(
                name: "TuyenBays");

            migrationBuilder.DropTable(
                name: "KhachHangs");

            migrationBuilder.DropTable(
                name: "SanBays");
        }
    }
}

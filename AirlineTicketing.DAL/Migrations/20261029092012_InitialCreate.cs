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
                name: "DatVes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MaDatVe = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    HoTenKhachHang = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    SoDienThoai = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NgayDat = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TrangThai = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DatVes", x => x.Id);
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
                    SoGhe = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MayBays", x => x.Id);
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
                    QuocGia = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SanBays", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChuyenBays",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SoHieu = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SanBayDiId = table.Column<int>(type: "integer", nullable: false),
                    SanBayDenId = table.Column<int>(type: "integer", nullable: false),
                    MayBayId = table.Column<int>(type: "integer", nullable: false),
                    GioKhoiHanh = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GioHaCanh = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GiaCoBan = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChuyenBays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChuyenBays_MayBays_MayBayId",
                        column: x => x.MayBayId,
                        principalTable: "MayBays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChuyenBays_SanBays_SanBayDenId",
                        column: x => x.SanBayDenId,
                        principalTable: "SanBays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChuyenBays_SanBays_SanBayDiId",
                        column: x => x.SanBayDiId,
                        principalTable: "SanBays",
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
                    SoGhe = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    HangVe = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    GiaVe = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TrangThai = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ves", x => x.Id);
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
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChuyenBays_MayBayId",
                table: "ChuyenBays",
                column: "MayBayId");

            migrationBuilder.CreateIndex(
                name: "IX_ChuyenBays_SanBayDenId",
                table: "ChuyenBays",
                column: "SanBayDenId");

            migrationBuilder.CreateIndex(
                name: "IX_ChuyenBays_SanBayDiId",
                table: "ChuyenBays",
                column: "SanBayDiId");

            migrationBuilder.CreateIndex(
                name: "IX_ChuyenBays_SoHieu_GioKhoiHanh",
                table: "ChuyenBays",
                columns: new[] { "SoHieu", "GioKhoiHanh" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DatVes_MaDatVe",
                table: "DatVes",
                column: "MaDatVe",
                unique: true);

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
                name: "IX_Ves_ChuyenBayId_SoGhe",
                table: "Ves",
                columns: new[] { "ChuyenBayId", "SoGhe" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ves_DatVeId",
                table: "Ves",
                column: "DatVeId");

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
                name: "Ves");

            migrationBuilder.DropTable(
                name: "ChuyenBays");

            migrationBuilder.DropTable(
                name: "DatVes");

            migrationBuilder.DropTable(
                name: "MayBays");

            migrationBuilder.DropTable(
                name: "SanBays");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AirlineTicketing.DAL.Migrations
{
    /// <inheritdoc />
    public partial class FlightDisruptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BienDongDatVes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DatVeId = table.Column<int>(type: "integer", nullable: false),
                    ChuyenBayId = table.Column<int>(type: "integer", nullable: false),
                    SoHieuCu = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SoHieuMoi = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    KhoiHanhCu = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    HaCanhCu = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    KhoiHanhMoi = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    HaCanhMoi = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NgayTao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    NgayXuLy = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    KetQua = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BienDongDatVes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BienDongDatVes_ChuyenBays_ChuyenBayId",
                        column: x => x.ChuyenBayId,
                        principalTable: "ChuyenBays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BienDongDatVes_DatVes_DatVeId",
                        column: x => x.DatVeId,
                        principalTable: "DatVes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BienDongDatVes_ChuyenBayId",
                table: "BienDongDatVes",
                column: "ChuyenBayId");

            migrationBuilder.CreateIndex(
                name: "IX_BienDongDatVes_DatVeId_NgayXuLy",
                table: "BienDongDatVes",
                columns: new[] { "DatVeId", "NgayXuLy" });

            migrationBuilder.CreateIndex(
                name: "IX_BienDongDatVes_NgayXuLy_NgayTao",
                table: "BienDongDatVes",
                columns: new[] { "NgayXuLy", "NgayTao" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BienDongDatVes");
        }
    }
}

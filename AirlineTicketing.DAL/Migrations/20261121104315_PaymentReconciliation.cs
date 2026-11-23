using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AirlineTicketing.DAL.Migrations
{
    /// <inheritdoc />
    public partial class PaymentReconciliation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChungTuDoiSoats",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ThanhToanId = table.Column<int>(type: "integer", nullable: false),
                    HoanTienId = table.Column<int>(type: "integer", nullable: true),
                    NhaCungCap = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    MaLenh = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MaChungTu = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    KetQua = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    SoTien = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    NguoiDoiSoat = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ThoiDiem = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChungTuDoiSoats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChungTuDoiSoats_HoanTiens_HoanTienId",
                        column: x => x.HoanTienId,
                        principalTable: "HoanTiens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChungTuDoiSoats_ThanhToans_ThanhToanId",
                        column: x => x.ThanhToanId,
                        principalTable: "ThanhToans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChungTuDoiSoats_HoanTienId",
                table: "ChungTuDoiSoats",
                column: "HoanTienId");

            migrationBuilder.CreateIndex(
                name: "IX_ChungTuDoiSoats_NhaCungCap_MaChungTu",
                table: "ChungTuDoiSoats",
                columns: new[] { "NhaCungCap", "MaChungTu" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChungTuDoiSoats_ThanhToanId",
                table: "ChungTuDoiSoats",
                column: "ThanhToanId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChungTuDoiSoats");
        }
    }
}

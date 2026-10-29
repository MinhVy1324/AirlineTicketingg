using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AirlineTicketing.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddRefundLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ThanhToans_MaGiaoDich",
                table: "ThanhToans");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ThanhToan_SoTien",
                table: "ThanhToans");

            migrationBuilder.AddColumn<string>(
                name: "NhaCungCap",
                table: "ThanhToans",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "HoanTiens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ThanhToanId = table.Column<int>(type: "integer", nullable: false),
                    SoTien = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    DonViTienTe = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    NhaCungCap = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    MaGiaoDich = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LyDo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    TrangThai = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NgayTao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    NgayHoanTat = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HoanTiens", x => x.Id);
                    table.CheckConstraint("CK_HoanTien_SoTien", "\"SoTien\" > 0");
                    table.ForeignKey(
                        name: "FK_HoanTiens_ThanhToans_ThanhToanId",
                        column: x => x.ThanhToanId,
                        principalTable: "ThanhToans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ThanhToans_NhaCungCap_MaGiaoDich",
                table: "ThanhToans",
                columns: new[] { "NhaCungCap", "MaGiaoDich" },
                unique: true,
                filter: "\"MaGiaoDich\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ThanhToan_SoTien",
                table: "ThanhToans",
                sql: "\"SoTien\" > 0");

            migrationBuilder.CreateIndex(
                name: "IX_HoanTiens_NhaCungCap_MaGiaoDich",
                table: "HoanTiens",
                columns: new[] { "NhaCungCap", "MaGiaoDich" },
                unique: true,
                filter: "\"MaGiaoDich\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_HoanTiens_ThanhToanId_NgayTao",
                table: "HoanTiens",
                columns: new[] { "ThanhToanId", "NgayTao" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HoanTiens");

            migrationBuilder.DropIndex(
                name: "IX_ThanhToans_NhaCungCap_MaGiaoDich",
                table: "ThanhToans");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ThanhToan_SoTien",
                table: "ThanhToans");

            migrationBuilder.DropColumn(
                name: "NhaCungCap",
                table: "ThanhToans");

            migrationBuilder.CreateIndex(
                name: "IX_ThanhToans_MaGiaoDich",
                table: "ThanhToans",
                column: "MaGiaoDich",
                unique: true,
                filter: "\"MaGiaoDich\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ThanhToan_SoTien",
                table: "ThanhToans",
                sql: "\"SoTien\" >= 0");
        }
    }
}

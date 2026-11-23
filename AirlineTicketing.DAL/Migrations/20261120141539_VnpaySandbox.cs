using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AirlineTicketing.DAL.Migrations
{
    /// <inheritdoc />
    public partial class VnpaySandbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "KetQuaNhaCungCap",
                table: "ThanhToans",
                type: "character varying(24)",
                maxLength: 24,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MaDiemThanhToan",
                table: "ThanhToans",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MaGiaoDichNhaCungCap",
                table: "ThanhToans",
                type: "character varying(15)",
                maxLength: 15,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KetQuaNhaCungCap",
                table: "HoanTiens",
                type: "character varying(24)",
                maxLength: 24,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MaGiaoDichNhaCungCap",
                table: "HoanTiens",
                type: "character varying(15)",
                maxLength: 15,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NgayGuiCongThanhToan",
                table: "HoanTiens",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "YeuCauDoiHoanId",
                table: "HoanTiens",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ThanhToans_MaDiemThanhToan_MaGiaoDichNhaCungCap",
                table: "ThanhToans",
                columns: new[] { "MaDiemThanhToan", "MaGiaoDichNhaCungCap" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HoanTiens_YeuCauDoiHoanId",
                table: "HoanTiens",
                column: "YeuCauDoiHoanId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_HoanTiens_YeuCauDoiHoans_YeuCauDoiHoanId",
                table: "HoanTiens",
                column: "YeuCauDoiHoanId",
                principalTable: "YeuCauDoiHoans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HoanTiens_YeuCauDoiHoans_YeuCauDoiHoanId",
                table: "HoanTiens");

            migrationBuilder.DropIndex(
                name: "IX_ThanhToans_MaDiemThanhToan_MaGiaoDichNhaCungCap",
                table: "ThanhToans");

            migrationBuilder.DropIndex(
                name: "IX_HoanTiens_YeuCauDoiHoanId",
                table: "HoanTiens");

            migrationBuilder.DropColumn(
                name: "KetQuaNhaCungCap",
                table: "ThanhToans");

            migrationBuilder.DropColumn(
                name: "MaDiemThanhToan",
                table: "ThanhToans");

            migrationBuilder.DropColumn(
                name: "MaGiaoDichNhaCungCap",
                table: "ThanhToans");

            migrationBuilder.DropColumn(
                name: "KetQuaNhaCungCap",
                table: "HoanTiens");

            migrationBuilder.DropColumn(
                name: "MaGiaoDichNhaCungCap",
                table: "HoanTiens");

            migrationBuilder.DropColumn(
                name: "NgayGuiCongThanhToan",
                table: "HoanTiens");

            migrationBuilder.DropColumn(
                name: "YeuCauDoiHoanId",
                table: "HoanTiens");
        }
    }
}

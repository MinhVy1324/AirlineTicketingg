using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AirlineTicketing.DAL.Migrations
{
    /// <inheritdoc />
    public partial class DongBoSchemaSauMerge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DatVes_HanGiuCho",
                table: "DatVes");

            migrationBuilder.AddColumn<string>(
                name: "LyDoHuy",
                table: "Ves",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NgayHuy",
                table: "Ves",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "HanGiuCho",
                table: "DatVes",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "LichSuDatVes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DatVeId = table.Column<int>(type: "integer", nullable: false),
                    VeId = table.Column<int>(type: "integer", nullable: true),
                    HanhDong = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TrangThaiTruoc = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    TrangThaiSau = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    LyDo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    NguoiThucHien = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    DuLieuBoSung = table.Column<string>(type: "jsonb", nullable: true),
                    ThoiDiem = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LichSuDatVes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LichSuDatVes_DatVes_DatVeId",
                        column: x => x.DatVeId,
                        principalTable: "DatVes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LichSuDatVes_Ves_VeId",
                        column: x => x.VeId,
                        principalTable: "Ves",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DatVes_TrangThai_HanGiuCho",
                table: "DatVes",
                columns: new[] { "TrangThai", "HanGiuCho" });

            migrationBuilder.CreateIndex(
                name: "IX_LichSuDatVes_DatVeId_ThoiDiem",
                table: "LichSuDatVes",
                columns: new[] { "DatVeId", "ThoiDiem" });

            migrationBuilder.CreateIndex(
                name: "IX_LichSuDatVes_VeId",
                table: "LichSuDatVes",
                column: "VeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LichSuDatVes");

            migrationBuilder.DropIndex(
                name: "IX_DatVes_TrangThai_HanGiuCho",
                table: "DatVes");

            migrationBuilder.DropColumn(
                name: "LyDoHuy",
                table: "Ves");

            migrationBuilder.DropColumn(
                name: "NgayHuy",
                table: "Ves");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "HanGiuCho",
                table: "DatVes",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.CreateIndex(
                name: "IX_DatVes_HanGiuCho",
                table: "DatVes",
                column: "HanGiuCho");
        }
    }
}

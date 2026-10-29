using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AirlineTicketing.DAL.Migrations
{
    /// <inheritdoc />
    public partial class EnforceDatabaseConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<bool>(
                name: "DangKhaiThac",
                table: "TuyenBays",
                type: "boolean",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<string>(
                name: "NhaCungCap",
                table: "ThanhToans",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "NoiBo",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<bool>(
                name: "DangHoatDong",
                table: "SanBays",
                type: "boolean",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<bool>(
                name: "DangHoatDong",
                table: "MayBays",
                type: "boolean",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<string>(
                name: "NhaCungCap",
                table: "HoanTiens",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "NoiBo",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Ve_TrangThai",
                table: "Ves",
                sql: "\"TrangThai\" IN ('TamGiu', 'DaPhatHanh', 'DaHuy', 'DaHoanTien')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ThanhToan_DonViTienTe",
                table: "ThanhToans",
                sql: "\"DonViTienTe\" ~ '^[A-Z]{3}$'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ThanhToan_TrangThai",
                table: "ThanhToans",
                sql: "\"TrangThai\" IN ('ChoXuLy', 'ThanhCong', 'ThatBai')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_HoanTien_DonViTienTe",
                table: "HoanTiens",
                sql: "\"DonViTienTe\" ~ '^[A-Z]{3}$'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_HoanTien_TrangThai",
                table: "HoanTiens",
                sql: "\"TrangThai\" IN ('ChoXuLy', 'ThanhCong', 'ThatBai')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DatVe_DonViTienTe",
                table: "DatVes",
                sql: "\"DonViTienTe\" ~ '^[A-Z]{3}$'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DatVe_TrangThai",
                table: "DatVes",
                sql: "\"TrangThai\" IN ('ChoThanhToan', 'DaXacNhan', 'DaHuy', 'HetHan', 'HoanTien')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ChuyenBay_TrangThai",
                table: "ChuyenBays",
                sql: "\"TrangThai\" IN ('DuKien', 'DangBay', 'HoanTat', 'BiHuy')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Ve_TrangThai",
                table: "Ves");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ThanhToan_DonViTienTe",
                table: "ThanhToans");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ThanhToan_TrangThai",
                table: "ThanhToans");

            migrationBuilder.DropCheckConstraint(
                name: "CK_HoanTien_DonViTienTe",
                table: "HoanTiens");

            migrationBuilder.DropCheckConstraint(
                name: "CK_HoanTien_TrangThai",
                table: "HoanTiens");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DatVe_DonViTienTe",
                table: "DatVes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DatVe_TrangThai",
                table: "DatVes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ChuyenBay_TrangThai",
                table: "ChuyenBays");

            migrationBuilder.AlterColumn<bool>(
                name: "DangKhaiThac",
                table: "TuyenBays",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: true);

            migrationBuilder.AlterColumn<string>(
                name: "NhaCungCap",
                table: "ThanhToans",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldDefaultValue: "NoiBo");

            migrationBuilder.AlterColumn<bool>(
                name: "DangHoatDong",
                table: "SanBays",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: true);

            migrationBuilder.AlterColumn<bool>(
                name: "DangHoatDong",
                table: "MayBays",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: true);

            migrationBuilder.AlterColumn<string>(
                name: "NhaCungCap",
                table: "HoanTiens",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldDefaultValue: "NoiBo");
        }
    }
}

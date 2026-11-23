using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AirlineTicketing.DAL.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261123120000_AccountContactGmail")]
public sealed class AccountContactGmail : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddColumn<string>("GmailLienHe", "TaiKhoans", type: "character varying(254)", maxLength: 254, nullable: true);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn("GmailLienHe", "TaiKhoans");
}

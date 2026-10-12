using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AcxiomCRM.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerModifiedDateAndEmailCollation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Customers",
                type: "TEXT",
                maxLength: 150,
                nullable: false,
                collation: "NOCASE",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 150);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedDate",
                table: "Customers",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ModifiedDate",
                table: "Customers");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Customers",
                type: "TEXT",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 150,
                oldCollation: "NOCASE");
        }
    }
}

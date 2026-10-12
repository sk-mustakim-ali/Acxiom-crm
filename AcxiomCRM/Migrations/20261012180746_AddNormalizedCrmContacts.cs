using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AcxiomCRM.Migrations
{
    /// <inheritdoc />
    public partial class AddNormalizedCrmContacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Customers_Phone",
                table: "Customers");

            migrationBuilder.AddColumn<string>(
                name: "NormalizedEmail",
                table: "Leads",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedPhone",
                table: "Leads",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedEmail",
                table: "Customers",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NormalizedPhone",
                table: "Customers",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("UPDATE Customers SET NormalizedEmail = upper(trim(Email)), NormalizedPhone = replace(replace(replace(replace(replace(replace(trim(Phone), '+', ''), ' ', ''), '-', ''), '(', ''), ')', ''), '.', '');");
            migrationBuilder.Sql("UPDATE Leads SET NormalizedEmail = CASE WHEN Email IS NULL OR trim(Email) = '' THEN NULL ELSE upper(trim(Email)) END, NormalizedPhone = CASE WHEN Phone IS NULL OR trim(Phone) = '' THEN NULL ELSE replace(replace(replace(replace(replace(replace(trim(Phone), '+', ''), ' ', ''), '-', ''), '(', ''), ')', ''), '.', '') END;");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_NormalizedEmail",
                table: "Leads",
                column: "NormalizedEmail",
                unique: true,
                filter: "\"NormalizedEmail\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_NormalizedPhone",
                table: "Leads",
                column: "NormalizedPhone",
                unique: true,
                filter: "\"NormalizedPhone\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_NormalizedEmail",
                table: "Customers",
                column: "NormalizedEmail",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Customers_NormalizedPhone",
                table: "Customers",
                column: "NormalizedPhone",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Leads_NormalizedEmail",
                table: "Leads");

            migrationBuilder.DropIndex(
                name: "IX_Leads_NormalizedPhone",
                table: "Leads");

            migrationBuilder.DropIndex(
                name: "IX_Customers_NormalizedEmail",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Customers_NormalizedPhone",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "NormalizedEmail",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "NormalizedPhone",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "NormalizedEmail",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "NormalizedPhone",
                table: "Customers");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_Phone",
                table: "Customers",
                column: "Phone",
                unique: true);
        }
    }
}

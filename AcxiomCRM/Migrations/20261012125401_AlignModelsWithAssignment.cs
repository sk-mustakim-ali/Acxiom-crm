using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AcxiomCRM.Migrations
{
    /// <inheritdoc />
    public partial class AlignModelsWithAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Id",
                table: "Opportunities",
                newName: "OpportunityId");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "Leads",
                newName: "LeadId");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "FollowUps",
                newName: "FollowUpId");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "Customers",
                newName: "CustomerId");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "AuditLogs",
                newName: "AuditLogId");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "Activities",
                newName: "ActivityId");

            migrationBuilder.AddColumn<string>(
                name: "LeadCode",
                table: "Leads",
                type: "TEXT",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Remarks",
                table: "FollowUps",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerCode",
                table: "Customers",
                type: "TEXT",
                maxLength: 30,
                nullable: false,
                defaultValue: "");
            // Backfill unique codes for existing records.
            migrationBuilder.Sql("UPDATE Customers SET CustomerCode = 'CUS-' || printf('%06d', CustomerId);");
            migrationBuilder.Sql("UPDATE Leads SET LeadCode = 'LEAD-' || printf('%06d', LeadId);");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_CustomerCode",
                table: "Customers",
                column: "CustomerCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Leads_LeadCode",
                table: "Leads",
                column: "LeadCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Customers_CustomerCode",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Leads_LeadCode",
                table: "Leads");
            migrationBuilder.DropColumn(
                name: "LeadCode",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "Remarks",
                table: "FollowUps");

            migrationBuilder.DropColumn(
                name: "CustomerCode",
                table: "Customers");

            migrationBuilder.RenameColumn(
                name: "OpportunityId",
                table: "Opportunities",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "LeadId",
                table: "Leads",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "FollowUpId",
                table: "FollowUps",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "CustomerId",
                table: "Customers",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "AuditLogId",
                table: "AuditLogs",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "ActivityId",
                table: "Activities",
                newName: "Id");
        }
    }
}


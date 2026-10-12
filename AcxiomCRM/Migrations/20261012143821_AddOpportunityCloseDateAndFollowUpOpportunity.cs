using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AcxiomCRM.Migrations
{
    /// <inheritdoc />
    public partial class AddOpportunityCloseDateAndFollowUpOpportunity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ClosedDate",
                table: "Opportunities",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OpportunityId",
                table: "FollowUps",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.Sql("UPDATE Opportunities SET ClosedDate = CreatedDate WHERE Status IN ('Won', 'Lost');");

            migrationBuilder.CreateIndex(
                name: "IX_FollowUps_OpportunityId",
                table: "FollowUps",
                column: "OpportunityId");

            migrationBuilder.AddForeignKey(
                name: "FK_FollowUps_Opportunities_OpportunityId",
                table: "FollowUps",
                column: "OpportunityId",
                principalTable: "Opportunities",
                principalColumn: "OpportunityId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FollowUps_Opportunities_OpportunityId",
                table: "FollowUps");

            migrationBuilder.DropIndex(
                name: "IX_FollowUps_OpportunityId",
                table: "FollowUps");

            migrationBuilder.DropColumn(
                name: "ClosedDate",
                table: "Opportunities");

            migrationBuilder.DropColumn(
                name: "OpportunityId",
                table: "FollowUps");
        }
    }
}

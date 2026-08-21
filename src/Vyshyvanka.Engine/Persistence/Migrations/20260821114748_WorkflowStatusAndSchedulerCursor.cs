using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vyshyvanka.Engine.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WorkflowStatusAndSchedulerCursor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Scheduler cursor for cron/interval dispatch.
            migrationBuilder.AddColumn<DateTime>(
                name: "LastScheduledFireAt",
                table: "Workflows",
                nullable: true);

            // New activation state column. Add with a default first so existing rows are valid,
            // then backfill from the old IsActive flag before dropping it (no data loss).
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Workflows",
                maxLength: 20,
                nullable: false,
                defaultValue: "Draft");

            // Backfill: previously-active workflows become Active; everything else stays Draft.
            migrationBuilder.Sql(
                "UPDATE \"Workflows\" SET \"Status\" = 'Active' WHERE \"IsActive\" = TRUE;");

            migrationBuilder.DropIndex(
                name: "IX_Workflows_IsActive",
                table: "Workflows");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Workflows");

            migrationBuilder.CreateIndex(
                name: "IX_Workflows_Status",
                table: "Workflows",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Re-add the boolean flag and backfill from Status before dropping the new columns.
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Workflows",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(
                "UPDATE \"Workflows\" SET \"IsActive\" = TRUE WHERE \"Status\" = 'Active';");

            migrationBuilder.DropIndex(
                name: "IX_Workflows_Status",
                table: "Workflows");

            migrationBuilder.DropColumn(
                name: "LastScheduledFireAt",
                table: "Workflows");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Workflows");

            migrationBuilder.CreateIndex(
                name: "IX_Workflows_IsActive",
                table: "Workflows",
                column: "IsActive");
        }
    }
}

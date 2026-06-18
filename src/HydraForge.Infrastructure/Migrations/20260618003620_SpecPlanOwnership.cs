using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HydraForge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SpecPlanOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_cards_PlanId",
                table: "cards");

            migrationBuilder.DropIndex(
                name: "IX_cards_SpecId",
                table: "cards");

            migrationBuilder.DropColumn(
                name: "PlanId",
                table: "cards");

            migrationBuilder.DropColumn(
                name: "SpecId",
                table: "cards");

            migrationBuilder.AddColumn<Guid>(
                name: "card_id",
                table: "specs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "card_id",
                table: "plans",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "spec_id",
                table: "plans",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_specs_card_id",
                table: "specs",
                column: "card_id");

            migrationBuilder.CreateIndex(
                name: "IX_spec_versions_SpecId_Version",
                table: "spec_versions",
                columns: new[] { "SpecId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_plans_card_id",
                table: "plans",
                column: "card_id");

            migrationBuilder.CreateIndex(
                name: "ix_plans_spec_id",
                table: "plans",
                column: "spec_id");

            migrationBuilder.CreateIndex(
                name: "IX_plan_versions_PlanId_Version",
                table: "plan_versions",
                columns: new[] { "PlanId", "Version" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_plan_versions_plans_PlanId",
                table: "plan_versions",
                column: "PlanId",
                principalTable: "plans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_plans_cards_card_id",
                table: "plans",
                column: "card_id",
                principalTable: "cards",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_plans_specs_spec_id",
                table: "plans",
                column: "spec_id",
                principalTable: "specs",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_spec_versions_specs_SpecId",
                table: "spec_versions",
                column: "SpecId",
                principalTable: "specs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_specs_cards_card_id",
                table: "specs",
                column: "card_id",
                principalTable: "cards",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_plan_versions_plans_PlanId",
                table: "plan_versions");

            migrationBuilder.DropForeignKey(
                name: "FK_plans_cards_card_id",
                table: "plans");

            migrationBuilder.DropForeignKey(
                name: "FK_plans_specs_spec_id",
                table: "plans");

            migrationBuilder.DropForeignKey(
                name: "FK_spec_versions_specs_SpecId",
                table: "spec_versions");

            migrationBuilder.DropForeignKey(
                name: "FK_specs_cards_card_id",
                table: "specs");

            migrationBuilder.DropIndex(
                name: "ix_specs_card_id",
                table: "specs");

            migrationBuilder.DropIndex(
                name: "IX_spec_versions_SpecId_Version",
                table: "spec_versions");

            migrationBuilder.DropIndex(
                name: "ix_plans_card_id",
                table: "plans");

            migrationBuilder.DropIndex(
                name: "ix_plans_spec_id",
                table: "plans");

            migrationBuilder.DropIndex(
                name: "IX_plan_versions_PlanId_Version",
                table: "plan_versions");

            migrationBuilder.DropColumn(
                name: "card_id",
                table: "specs");

            migrationBuilder.DropColumn(
                name: "card_id",
                table: "plans");

            migrationBuilder.DropColumn(
                name: "spec_id",
                table: "plans");

            migrationBuilder.AddColumn<Guid>(
                name: "PlanId",
                table: "cards",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SpecId",
                table: "cards",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_cards_PlanId",
                table: "cards",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_cards_SpecId",
                table: "cards",
                column: "SpecId");
        }
    }
}

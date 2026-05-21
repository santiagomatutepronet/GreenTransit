using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GreenTransit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── WasteMoves ────────────────────────────────────────────────────────
            migrationBuilder.CreateIndex(
                name: "IX_WasteMoves_OwnerId_PlannedPickupStart",
                table: "WasteMoves",
                columns: new[] { "OwnerId", "PlannedPickupStart" });

            migrationBuilder.CreateIndex(
                name: "IX_WasteMoves_IdSource",
                table: "WasteMoves",
                column: "IdSource");

            migrationBuilder.CreateIndex(
                name: "IX_WasteMoves_IdDestination",
                table: "WasteMoves",
                column: "IdDestination");

            migrationBuilder.CreateIndex(
                name: "IX_WasteMoves_ServiceOrderId",
                table: "WasteMoves",
                column: "ServiceOrderId",
                filter: "[ServiceOrderId] IS NOT NULL");

            // ── WasteMoveResidues ─────────────────────────────────────────────────
            // IdLerCode — filtros de flujo (corriente) en compliance y dashboard
            migrationBuilder.CreateIndex(
                name: "IX_WasteMoveResidues_IdLerCode",
                table: "WasteMoveResidues",
                column: "IdLerCode");

            // ── EntryCACs ─────────────────────────────────────────────────────────
            migrationBuilder.CreateIndex(
                name: "IX_EntryCACs_OwnerId",
                table: "EntryCACs",
                column: "OwnerId");

            // CACEntryDate — filtros por fecha de entrada en CAC
            migrationBuilder.CreateIndex(
                name: "IX_EntryCACs_CACEntryDate",
                table: "EntryCACs",
                column: "CACEntryDate");

            // ── EntryCACResidues ──────────────────────────────────────────────────
            migrationBuilder.CreateIndex(
                name: "IX_EntryCACResidues_IdEntryCAC",
                table: "EntryCACResidues",
                column: "IdEntryCAC");

            // ── EntryPlants ───────────────────────────────────────────────────────
            migrationBuilder.CreateIndex(
                name: "IX_EntryPlants_OwnerId",
                table: "EntryPlants",
                column: "OwnerId");

            // ── EntryPlantResidues ────────────────────────────────────────────────
            migrationBuilder.CreateIndex(
                name: "IX_EntryPlantResidues_IdEntryPlant",
                table: "EntryPlantResidues",
                column: "IdEntryPlant");

            // ── TreatmentPlantResidues ────────────────────────────────────────────
            migrationBuilder.CreateIndex(
                name: "IX_TreatmentPlantResidues_IdTreatmentPlant",
                table: "TreatmentPlantResidues",
                column: "IdTreatmentPlant");

            // ── Incidents ─────────────────────────────────────────────────────────
            migrationBuilder.CreateIndex(
                name: "IX_Incidents_OwnerId",
                table: "Incidents",
                column: "OwnerId");

            // ── ServiceOrders ─────────────────────────────────────────────────────
            migrationBuilder.CreateIndex(
                name: "IX_ServiceOrders_PlannedPickupStart",
                table: "ServiceOrders",
                column: "PlannedPickupStart");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceOrders_IdPickupPoint",
                table: "ServiceOrders",
                column: "IdPickupPoint");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceOrders_Status",
                table: "ServiceOrders",
                column: "Status");

            // ── Agreements ────────────────────────────────────────────────────────
            migrationBuilder.CreateIndex(
                name: "IX_Agreements_IdCoordinator",
                table: "Agreements",
                column: "IdCoordinator");

            migrationBuilder.CreateIndex(
                name: "IX_Agreements_OwnerId",
                table: "Agreements",
                column: "OwnerId");

            // ── MarketShares ──────────────────────────────────────────────────────
            // OwnerId + Year + Period — filtros de período en dashboard compliance
            migrationBuilder.CreateIndex(
                name: "IX_MarketShares_OwnerId_Period",
                table: "MarketShares",
                columns: new[] { "OwnerId", "Year", "Period" });

            // ── Entities ──────────────────────────────────────────────────────────
            migrationBuilder.CreateIndex(
                name: "IX_Entities_Name",
                table: "Entities",
                column: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_WasteMoves_OwnerId_PlannedPickupStart",      table: "WasteMoves");
            migrationBuilder.DropIndex(name: "IX_WasteMoves_IdSource",                         table: "WasteMoves");
            migrationBuilder.DropIndex(name: "IX_WasteMoves_IdDestination",                    table: "WasteMoves");
            migrationBuilder.DropIndex(name: "IX_WasteMoves_ServiceOrderId",                   table: "WasteMoves");
            migrationBuilder.DropIndex(name: "IX_WasteMoveResidues_IdLerCode",                 table: "WasteMoveResidues");
            migrationBuilder.DropIndex(name: "IX_EntryCACs_OwnerId",                           table: "EntryCACs");
            migrationBuilder.DropIndex(name: "IX_EntryCACs_CACEntryDate",                      table: "EntryCACs");
            migrationBuilder.DropIndex(name: "IX_EntryCACResidues_IdEntryCAC",                 table: "EntryCACResidues");
            migrationBuilder.DropIndex(name: "IX_EntryPlants_OwnerId",                         table: "EntryPlants");
            migrationBuilder.DropIndex(name: "IX_EntryPlantResidues_IdEntryPlant",             table: "EntryPlantResidues");
            migrationBuilder.DropIndex(name: "IX_TreatmentPlantResidues_IdTreatmentPlant",     table: "TreatmentPlantResidues");
            migrationBuilder.DropIndex(name: "IX_Incidents_OwnerId",                           table: "Incidents");
            migrationBuilder.DropIndex(name: "IX_ServiceOrders_PlannedPickupStart",            table: "ServiceOrders");
            migrationBuilder.DropIndex(name: "IX_ServiceOrders_IdPickupPoint",                 table: "ServiceOrders");
            migrationBuilder.DropIndex(name: "IX_ServiceOrders_Status",                        table: "ServiceOrders");
            migrationBuilder.DropIndex(name: "IX_Agreements_IdCoordinator",                    table: "Agreements");
            migrationBuilder.DropIndex(name: "IX_Agreements_OwnerId",                          table: "Agreements");
            migrationBuilder.DropIndex(name: "IX_MarketShares_OwnerId_Period",                 table: "MarketShares");
            migrationBuilder.DropIndex(name: "IX_Entities_Name",                               table: "Entities");
        }
    }
}

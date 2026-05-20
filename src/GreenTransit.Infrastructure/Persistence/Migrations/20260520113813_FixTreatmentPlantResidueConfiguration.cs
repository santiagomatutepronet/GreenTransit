using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GreenTransit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixTreatmentPlantResidueConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_WasteMoves_ActualPickupStart",
                table: "WasteMoves",
                column: "ActualPickupStart");

            migrationBuilder.CreateIndex(
                name: "IX_WasteMoves_OwnerId",
                table: "WasteMoves",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_WasteMoves_OwnerId_Status",
                table: "WasteMoves",
                columns: new[] { "OwnerId", "ServiceStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_WasteMoves_PlannedPickupStart",
                table: "WasteMoves",
                column: "PlannedPickupStart");

            migrationBuilder.CreateIndex(
                name: "IX_WasteMoves_ServiceStatus",
                table: "WasteMoves",
                column: "ServiceStatus");

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentPlants_OwnerId",
                table: "TreatmentPlants",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentPlants_OwnerId_TreatmentDate",
                table: "TreatmentPlants",
                columns: new[] { "OwnerId", "PlantTreatmentDate" });

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentPlants_PlantTreatmentDate",
                table: "TreatmentPlants",
                column: "PlantTreatmentDate");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceOrders_OwnerId",
                table: "ServiceOrders",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceOrders_OwnerId_IssuedBy",
                table: "ServiceOrders",
                columns: new[] { "OwnerId", "IdIssuedBy" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WasteMoves_ActualPickupStart",
                table: "WasteMoves");

            migrationBuilder.DropIndex(
                name: "IX_WasteMoves_OwnerId",
                table: "WasteMoves");

            migrationBuilder.DropIndex(
                name: "IX_WasteMoves_OwnerId_Status",
                table: "WasteMoves");

            migrationBuilder.DropIndex(
                name: "IX_WasteMoves_PlannedPickupStart",
                table: "WasteMoves");

            migrationBuilder.DropIndex(
                name: "IX_WasteMoves_ServiceStatus",
                table: "WasteMoves");

            migrationBuilder.DropIndex(
                name: "IX_TreatmentPlants_OwnerId",
                table: "TreatmentPlants");

            migrationBuilder.DropIndex(
                name: "IX_TreatmentPlants_OwnerId_TreatmentDate",
                table: "TreatmentPlants");

            migrationBuilder.DropIndex(
                name: "IX_TreatmentPlants_PlantTreatmentDate",
                table: "TreatmentPlants");

            migrationBuilder.DropIndex(
                name: "IX_ServiceOrders_OwnerId",
                table: "ServiceOrders");

            migrationBuilder.DropIndex(
                name: "IX_ServiceOrders_OwnerId_IssuedBy",
                table: "ServiceOrders");
        }
    }
}

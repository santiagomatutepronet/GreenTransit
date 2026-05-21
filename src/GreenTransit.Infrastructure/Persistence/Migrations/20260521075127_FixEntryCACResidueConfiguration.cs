using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GreenTransit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixEntryCACResidueConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Solo actualiza el snapshot del modelo EF para registrar EntryCACResidueConfiguration.
            // La columna IdEntryCAC ya existe en la BD con el nombre correcto.
        }

        private void _OriginalUp_NotUsed(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PagePermissions_Profiles_IdProfile",
                table: "PagePermissions");

            migrationBuilder.DropIndex(
                name: "IX_WasteMoves_ServiceOrderId",
                table: "WasteMoves");

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateModifiedSys",
                table: "WasteMoves",
                type: "datetime2(0)",
                nullable: true,
                defaultValueSql: "SYSUTCDATETIME()",
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateCreateSys",
                table: "WasteMoves",
                type: "datetime2(0)",
                nullable: true,
                defaultValueSql: "SYSUTCDATETIME()",
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Weight",
                table: "WasteMoveResidues",
                type: "decimal(18,3)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "UnitPriceKg",
                table: "WasteMoveResidues",
                type: "decimal(18,6)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                table: "UserSharePointCredentials",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "ClientSecret",
                table: "UserSharePointCredentials",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "ClientId",
                table: "UserSharePointCredentials",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateModifiedSys",
                table: "TreatmentPlants",
                type: "datetime2(0)",
                nullable: true,
                defaultValueSql: "SYSUTCDATETIME()",
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateCreateSys",
                table: "TreatmentPlants",
                type: "datetime2(0)",
                nullable: true,
                defaultValueSql: "SYSUTCDATETIME()",
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Price",
                table: "Products",
                type: "decimal(18,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,0)",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateModifiedSys",
                table: "ProductDeclaration",
                type: "datetime2(0)",
                nullable: true,
                defaultValueSql: "SYSUTCDATETIME()",
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateEmit",
                table: "ProductDeclaration",
                type: "datetime2(0)",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateCreateSys",
                table: "ProductDeclaration",
                type: "datetime2(0)",
                nullable: true,
                defaultValueSql: "SYSUTCDATETIME()",
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateCreate",
                table: "ProductDeclaration",
                type: "datetime2(0)",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateModifiedSys",
                table: "EntryPlants",
                type: "datetime2(0)",
                nullable: true,
                defaultValueSql: "SYSUTCDATETIME()",
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateCreateSys",
                table: "EntryPlants",
                type: "datetime2(0)",
                nullable: true,
                defaultValueSql: "SYSUTCDATETIME()",
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateModifiedSys",
                table: "EntryCACs",
                type: "datetime2(0)",
                nullable: true,
                defaultValueSql: "SYSUTCDATETIME()",
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateCreateSys",
                table: "EntryCACs",
                type: "datetime2(0)",
                nullable: true,
                defaultValueSql: "SYSUTCDATETIME()",
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "DocStates",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "IdRef",
                table: "DocStates",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "CODE_ISO3",
                table: "Country",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WasteMoves_OwnerId_PlannedPickupStart",
                table: "WasteMoves",
                columns: new[] { "OwnerId", "PlannedPickupStart" });

            migrationBuilder.CreateIndex(
                name: "IX_WasteMoves_ServiceOrderId",
                table: "WasteMoves",
                column: "ServiceOrderId",
                filter: "[ServiceOrderId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WasteMoveResidues_IdLerCode",
                table: "WasteMoveResidues",
                column: "IdLerCode");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceOrders_PlannedPickupStart",
                table: "ServiceOrders",
                column: "PlannedPickupStart");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceOrders_Status",
                table: "ServiceOrders",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_MarketShares_OwnerId_Period",
                table: "MarketShares",
                columns: new[] { "OwnerId", "Year", "Period" });

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_OwnerId",
                table: "Incidents",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_EntryPlants_OwnerId",
                table: "EntryPlants",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_EntryCACs_CACEntryDate",
                table: "EntryCACs",
                column: "CACEntryDate");

            migrationBuilder.CreateIndex(
                name: "IX_EntryCACs_OwnerId",
                table: "EntryCACs",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Entities_Name",
                table: "Entities",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Agreements_OwnerId",
                table: "Agreements",
                column: "OwnerId");

            migrationBuilder.AddForeignKey(
                name: "FK_PagePermissions_Profiles_IdProfile",
                table: "PagePermissions",
                column: "IdProfile",
                principalTable: "Profiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op: Up() no realizó cambios en la BD.
        }

        private void _OriginalDown_NotUsed(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PagePermissions_Profiles_IdProfile",
                table: "PagePermissions");

            migrationBuilder.DropIndex(
                name: "IX_WasteMoves_OwnerId_PlannedPickupStart",
                table: "WasteMoves");

            migrationBuilder.DropIndex(
                name: "IX_WasteMoves_ServiceOrderId",
                table: "WasteMoves");

            migrationBuilder.DropIndex(
                name: "IX_WasteMoveResidues_IdLerCode",
                table: "WasteMoveResidues");

            migrationBuilder.DropIndex(
                name: "IX_ServiceOrders_PlannedPickupStart",
                table: "ServiceOrders");

            migrationBuilder.DropIndex(
                name: "IX_ServiceOrders_Status",
                table: "ServiceOrders");

            migrationBuilder.DropIndex(
                name: "IX_MarketShares_OwnerId_Period",
                table: "MarketShares");

            migrationBuilder.DropIndex(
                name: "IX_Incidents_OwnerId",
                table: "Incidents");

            migrationBuilder.DropIndex(
                name: "IX_EntryPlants_OwnerId",
                table: "EntryPlants");

            migrationBuilder.DropIndex(
                name: "IX_EntryCACs_CACEntryDate",
                table: "EntryCACs");

            migrationBuilder.DropIndex(
                name: "IX_EntryCACs_OwnerId",
                table: "EntryCACs");

            migrationBuilder.DropIndex(
                name: "IX_Entities_Name",
                table: "Entities");

            migrationBuilder.DropIndex(
                name: "IX_Agreements_OwnerId",
                table: "Agreements");

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateModifiedSys",
                table: "WasteMoves",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2(0)",
                oldNullable: true,
                oldDefaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateCreateSys",
                table: "WasteMoves",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2(0)",
                oldNullable: true,
                oldDefaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.AlterColumn<decimal>(
                name: "Weight",
                table: "WasteMoveResidues",
                type: "decimal(18,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,3)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "UnitPriceKg",
                table: "WasteMoveResidues",
                type: "decimal(18,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,6)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                table: "UserSharePointCredentials",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "ClientSecret",
                table: "UserSharePointCredentials",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(512)",
                oldMaxLength: 512);

            migrationBuilder.AlterColumn<string>(
                name: "ClientId",
                table: "UserSharePointCredentials",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateModifiedSys",
                table: "TreatmentPlants",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2(0)",
                oldNullable: true,
                oldDefaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateCreateSys",
                table: "TreatmentPlants",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2(0)",
                oldNullable: true,
                oldDefaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.AlterColumn<decimal>(
                name: "Price",
                table: "Products",
                type: "decimal(18,0)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateModifiedSys",
                table: "ProductDeclaration",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2(0)",
                oldNullable: true,
                oldDefaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateEmit",
                table: "ProductDeclaration",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2(0)",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateCreateSys",
                table: "ProductDeclaration",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2(0)",
                oldNullable: true,
                oldDefaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateCreate",
                table: "ProductDeclaration",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2(0)",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateModifiedSys",
                table: "EntryPlants",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2(0)",
                oldNullable: true,
                oldDefaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateCreateSys",
                table: "EntryPlants",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2(0)",
                oldNullable: true,
                oldDefaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateModifiedSys",
                table: "EntryCACs",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2(0)",
                oldNullable: true,
                oldDefaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateCreateSys",
                table: "EntryCACs",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2(0)",
                oldNullable: true,
                oldDefaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "DocStates",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "IdRef",
                table: "DocStates",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "CODE_ISO3",
                table: "Country",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(3)",
                oldMaxLength: 3,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WasteMoves_ServiceOrderId",
                table: "WasteMoves",
                column: "ServiceOrderId");

            migrationBuilder.AddForeignKey(
                name: "FK_PagePermissions_Profiles_IdProfile",
                table: "PagePermissions",
                column: "IdProfile",
                principalTable: "Profiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

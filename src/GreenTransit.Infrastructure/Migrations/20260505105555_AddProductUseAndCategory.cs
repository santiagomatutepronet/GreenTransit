using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GreenTransit.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductUseAndCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Añadir ProductCategory a Products si no existe
            migrationBuilder.Sql(
                "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'ProductCategory') " +
                "BEGIN ALTER TABLE [Products] ADD [ProductCategory] nvarchar(256) NULL; END");

            // Añadir ProductUse a Products si no existe
            migrationBuilder.Sql(
                "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'ProductUse') " +
                "BEGIN ALTER TABLE [Products] ADD [ProductUse] nvarchar(128) NULL; END");

            // Renombrar tablas DicProductDeclaration* solo si la versión en mayúscula todavía existe
            migrationBuilder.Sql(
                "IF OBJECT_ID('DicProductDeclarationProducts') IS NOT NULL AND OBJECT_ID('dicProductDeclarationProducts') IS NULL " +
                "EXEC sp_rename 'DicProductDeclarationProducts', 'dicProductDeclarationProducts';");

            migrationBuilder.Sql(
                "IF OBJECT_ID('DicProductDeclarationPeriods') IS NOT NULL AND OBJECT_ID('dicProductDeclarationPeriods') IS NULL " +
                "EXEC sp_rename 'DicProductDeclarationPeriods', 'dicProductDeclarationPeriods';");

            migrationBuilder.Sql(
                "IF OBJECT_ID('DicProductDeclarationUses') IS NOT NULL AND OBJECT_ID('dicProductDeclarationUse') IS NULL " +
                "EXEC sp_rename 'DicProductDeclarationUses', 'dicProductDeclarationUse';");

            migrationBuilder.Sql(
                "IF OBJECT_ID('DicProductDeclarationTypes') IS NOT NULL AND OBJECT_ID('dicProductDeclarationType') IS NULL " +
                "EXEC sp_rename 'DicProductDeclarationTypes', 'dicProductDeclarationType';");

            migrationBuilder.Sql(
                "IF OBJECT_ID('DicProductDeclarationSources') IS NOT NULL AND OBJECT_ID('dicProductDeclarationSource') IS NULL " +
                "EXEC sp_rename 'DicProductDeclarationSources', 'dicProductDeclarationSource';");

            migrationBuilder.Sql(
                "IF OBJECT_ID('DicProductDeclarationCategories') IS NOT NULL AND OBJECT_ID('dicProductDeclarationCategory') IS NULL " +
                "EXEC sp_rename 'DicProductDeclarationCategories', 'dicProductDeclarationCategory';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'ProductUse') " +
                "BEGIN ALTER TABLE [Products] DROP COLUMN [ProductUse]; END");

            migrationBuilder.Sql(
                "IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'ProductCategory') " +
                "BEGIN ALTER TABLE [Products] DROP COLUMN [ProductCategory]; END");
        }
    }
}

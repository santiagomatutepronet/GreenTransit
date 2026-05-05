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
            migrationBuilder.DropForeignKey(
                name: "FK_DicProductDeclarationProducts_DicProductDeclarationCategories_CategoryId",
                table: "DicProductDeclarationProducts");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DicProductDeclarationProducts",
                table: "DicProductDeclarationProducts");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DicProductDeclarationPeriods",
                table: "DicProductDeclarationPeriods");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DicProductDeclarationUses",
                table: "DicProductDeclarationUses");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DicProductDeclarationTypes",
                table: "DicProductDeclarationTypes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DicProductDeclarationSources",
                table: "DicProductDeclarationSources");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DicProductDeclarationCategories",
                table: "DicProductDeclarationCategories");

            migrationBuilder.RenameTable(
                name: "DicProductDeclarationProducts",
                newName: "dicProductDeclarationProducts");

            migrationBuilder.RenameTable(
                name: "DicProductDeclarationPeriods",
                newName: "dicProductDeclarationPeriods");

            migrationBuilder.RenameTable(
                name: "DicProductDeclarationUses",
                newName: "dicProductDeclarationUse");

            migrationBuilder.RenameTable(
                name: "DicProductDeclarationTypes",
                newName: "dicProductDeclarationType");

            migrationBuilder.RenameTable(
                name: "DicProductDeclarationSources",
                newName: "dicProductDeclarationSource");

            migrationBuilder.RenameTable(
                name: "DicProductDeclarationCategories",
                newName: "dicProductDeclarationCategory");

            migrationBuilder.RenameIndex(
                name: "IX_DicProductDeclarationProducts_CategoryId",
                table: "dicProductDeclarationProducts",
                newName: "IX_dicProductDeclarationProducts_CategoryId");

            migrationBuilder.AlterColumn<string>(
                name: "Source",
                table: "Products",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductCategory",
                table: "Products",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductUse",
                table: "Products",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Ref",
                table: "dicProductDeclarationProducts",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Ref",
                table: "dicProductDeclarationPeriods",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Ref",
                table: "dicProductDeclarationUse",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Ref",
                table: "dicProductDeclarationType",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Ref",
                table: "dicProductDeclarationSource",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Ref",
                table: "dicProductDeclarationCategory",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddPrimaryKey(
                name: "PK_dicProductDeclarationProducts",
                table: "dicProductDeclarationProducts",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_dicProductDeclarationPeriods",
                table: "dicProductDeclarationPeriods",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_dicProductDeclarationUse",
                table: "dicProductDeclarationUse",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_dicProductDeclarationType",
                table: "dicProductDeclarationType",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_dicProductDeclarationSource",
                table: "dicProductDeclarationSource",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_dicProductDeclarationCategory",
                table: "dicProductDeclarationCategory",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_dicProductDeclarationProducts_dicProductDeclarationCategory_CategoryId",
                table: "dicProductDeclarationProducts",
                column: "CategoryId",
                principalTable: "dicProductDeclarationCategory",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_dicProductDeclarationProducts_dicProductDeclarationCategory_CategoryId",
                table: "dicProductDeclarationProducts");

            migrationBuilder.DropPrimaryKey(
                name: "PK_dicProductDeclarationProducts",
                table: "dicProductDeclarationProducts");

            migrationBuilder.DropPrimaryKey(
                name: "PK_dicProductDeclarationPeriods",
                table: "dicProductDeclarationPeriods");

            migrationBuilder.DropPrimaryKey(
                name: "PK_dicProductDeclarationUse",
                table: "dicProductDeclarationUse");

            migrationBuilder.DropPrimaryKey(
                name: "PK_dicProductDeclarationType",
                table: "dicProductDeclarationType");

            migrationBuilder.DropPrimaryKey(
                name: "PK_dicProductDeclarationSource",
                table: "dicProductDeclarationSource");

            migrationBuilder.DropPrimaryKey(
                name: "PK_dicProductDeclarationCategory",
                table: "dicProductDeclarationCategory");

            migrationBuilder.DropColumn(
                name: "ProductCategory",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ProductUse",
                table: "Products");

            migrationBuilder.RenameTable(
                name: "dicProductDeclarationProducts",
                newName: "DicProductDeclarationProducts");

            migrationBuilder.RenameTable(
                name: "dicProductDeclarationPeriods",
                newName: "DicProductDeclarationPeriods");

            migrationBuilder.RenameTable(
                name: "dicProductDeclarationUse",
                newName: "DicProductDeclarationUses");

            migrationBuilder.RenameTable(
                name: "dicProductDeclarationType",
                newName: "DicProductDeclarationTypes");

            migrationBuilder.RenameTable(
                name: "dicProductDeclarationSource",
                newName: "DicProductDeclarationSources");

            migrationBuilder.RenameTable(
                name: "dicProductDeclarationCategory",
                newName: "DicProductDeclarationCategories");

            migrationBuilder.RenameIndex(
                name: "IX_dicProductDeclarationProducts_CategoryId",
                table: "DicProductDeclarationProducts",
                newName: "IX_DicProductDeclarationProducts_CategoryId");

            migrationBuilder.AlterColumn<string>(
                name: "Source",
                table: "Products",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Ref",
                table: "DicProductDeclarationProducts",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "Ref",
                table: "DicProductDeclarationPeriods",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "Ref",
                table: "DicProductDeclarationUses",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "Ref",
                table: "DicProductDeclarationTypes",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "Ref",
                table: "DicProductDeclarationSources",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "Ref",
                table: "DicProductDeclarationCategories",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128);

            migrationBuilder.AddPrimaryKey(
                name: "PK_DicProductDeclarationProducts",
                table: "DicProductDeclarationProducts",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DicProductDeclarationPeriods",
                table: "DicProductDeclarationPeriods",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DicProductDeclarationUses",
                table: "DicProductDeclarationUses",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DicProductDeclarationTypes",
                table: "DicProductDeclarationTypes",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DicProductDeclarationSources",
                table: "DicProductDeclarationSources",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DicProductDeclarationCategories",
                table: "DicProductDeclarationCategories",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_DicProductDeclarationProducts_DicProductDeclarationCategories_CategoryId",
                table: "DicProductDeclarationProducts",
                column: "CategoryId",
                principalTable: "DicProductDeclarationCategories",
                principalColumn: "Id");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ArkJsonValidator.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Templates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Tags = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Templates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FieldRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TemplateId = table.Column<int>(type: "INTEGER", nullable: false),
                    FieldPath = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    ValidatorType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ParametersJson = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "{}"),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    IsRequired = table.Column<bool>(type: "INTEGER", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FieldRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FieldRules_Templates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "Templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GroupRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TemplateId = table.Column<int>(type: "INTEGER", nullable: false),
                    GroupName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    GroupValidatorType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    FieldPaths = table.Column<string>(type: "TEXT", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupRules_Templates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "Templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Templates",
                columns: new[] { "Id", "CreatedAt", "Description", "Name", "Tags", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Validates SAP OData purchase order payload fields", "SAP Purchase Order", "sap,odata,purchase-order", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Validates user registration form fields", "User Registration", "user,registration,common", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "FieldRules",
                columns: new[] { "Id", "ErrorMessage", "FieldPath", "IsRequired", "Order", "ParametersJson", "TemplateId", "ValidatorType" },
                values: new object[,]
                {
                    { 1, "PurchaseOrder must be a valid SAP document number (up to 10 digits)", "PurchaseOrder", true, 1, "{}", 1, "sap_document_number" },
                    { 2, "CompanyCode must be a 4-character alphanumeric value", "CompanyCode", true, 2, "{}", 1, "sap_company_code" },
                    { 3, "Vendor is required", "Vendor", true, 3, "{}", 1, "required" },
                    { 4, "Currency must be a valid ISO 4217 3-letter code", "Currency", true, 4, "{}", 1, "sap_currency_code" },
                    { 5, "PostingDate must be in SAP OData date format /Date(epoch)/", "PostingDate", true, 5, "{}", 1, "sap_odata_date" },
                    { 6, "Plant must be a 4-character SAP plant code", "Plant", true, 6, "{}", 1, "sap_plant_code" },
                    { 7, "Please provide a valid email address", "email", true, 1, "{}", 2, "email" },
                    { 8, "Please provide a valid phone number", "phone", false, 2, "{}", 2, "phone" },
                    { 9, "Username must be at least 3 characters", "username", true, 3, "{\"min\":3}", 2, "min_length" },
                    { 10, "Username must be at most 50 characters", "username", true, 4, "{\"max\":50}", 2, "max_length" },
                    { 11, "User must be at least 18 years old", "age", false, 5, "{\"min\":18}", 2, "min_value" },
                    { 12, "Please provide a valid URL", "website", false, 6, "{}", 2, "url" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_FieldRules_TemplateId",
                table: "FieldRules",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupRules_TemplateId",
                table: "GroupRules",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_Templates_Name",
                table: "Templates",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FieldRules");

            migrationBuilder.DropTable(
                name: "GroupRules");

            migrationBuilder.DropTable(
                name: "Templates");
        }
    }
}

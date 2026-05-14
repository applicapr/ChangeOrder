using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChangeOrder.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "dbo");

            migrationBuilder.CreateTable(
                name: "ChangeOrders",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderNumber = table.Column<string>(type: "varchar(13)", nullable: false),
                    ProgramName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProductionVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    VersionScreenshotPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RequestDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Requester_Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Requester_Position = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Requester_Department = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Requester_Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    WorkDescription = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    RequestDetails = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Justification = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    RequiredAction = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Approval_Requester = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    Approval_DepartmentHead = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    Approval_ItHead = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    Approval_ProgrammingDivision = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Draft"),
                    DeliveryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InitialEvaluationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProductionDeployDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PostDeployScreenshotPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChangeOrders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IdempotencyKeys",
                schema: "dbo",
                columns: table => new
                {
                    Key = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestHash = table.Column<byte[]>(type: "varbinary(32)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdempotencyKeys", x => x.Key);
                    table.ForeignKey(
                        name: "FK_IdempotencyKeys_ChangeOrders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "dbo",
                        principalTable: "ChangeOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChangeOrders_IsDeleted",
                schema: "dbo",
                table: "ChangeOrders",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeOrders_OrderNumber",
                schema: "dbo",
                table: "ChangeOrders",
                column: "OrderNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChangeOrders_RequestDate",
                schema: "dbo",
                table: "ChangeOrders",
                column: "RequestDate");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeOrders_Status",
                schema: "dbo",
                table: "ChangeOrders",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyKeys_CreatedAt",
                schema: "dbo",
                table: "IdempotencyKeys",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyKeys_OrderId",
                schema: "dbo",
                table: "IdempotencyKeys",
                column: "OrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IdempotencyKeys",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ChangeOrders",
                schema: "dbo");
        }
    }
}

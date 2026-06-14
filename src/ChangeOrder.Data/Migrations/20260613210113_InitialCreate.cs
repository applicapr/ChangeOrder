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
            migrationBuilder.CreateTable(
                name: "ChangeOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderNumber = table.Column<string>(type: "nvarchar(13)", maxLength: 13, nullable: false),
                    ProgramName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProductionVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    VersionScreenshotPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RequestDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    WorkDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    RequestDetails = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Justification = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RequiredAction = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Requester_Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Requester_Position = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Requester_Department = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Requester_Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Approval_RequesterApproval = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Approval_DepartmentHeadApproval = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Approval_ItHeadApproval = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Approval_ProgrammingDivisionApproval = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    DeliveryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InitialEvaluationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProductionDeployDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PostDeployScreenshotPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChangeOrders", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChangeOrders_OrderNumber",
                table: "ChangeOrders",
                column: "OrderNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChangeOrders");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Elijah.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeviceTemplate",
                columns: table => new
                {
                    SysCreated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SysModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SysRemoved = table.Column<bool>(type: "boolean", nullable: false),
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ModelId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    NumberOfActive = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceTemplate", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OpenTherm",
                columns: table => new
                {
                    SysCreated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SysModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SysRemoved = table.Column<bool>(type: "boolean", nullable: false),
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Threshold = table.Column<float>(type: "real", nullable: false),
                    IntervalSec = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpenTherm", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Device",
                columns: table => new
                {
                    SysCreated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SysModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SysRemoved = table.Column<bool>(type: "boolean", nullable: false),
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Address = table.Column<string>(type: "text", nullable: false),
                    Subscribed = table.Column<bool>(type: "boolean", nullable: false),
                    DeviceTemplateId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Device", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Device_DeviceTemplate_DeviceTemplateId",
                        column: x => x.DeviceTemplateId,
                        principalTable: "DeviceTemplate",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConfiguredReporting",
                columns: table => new
                {
                    SysCreated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SysModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SysRemoved = table.Column<bool>(type: "boolean", nullable: false),
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Cluster = table.Column<string>(type: "text", nullable: false),
                    Attribute = table.Column<string>(type: "text", nullable: false),
                    MaximumReportInterval = table.Column<string>(type: "text", nullable: false),
                    MinimumReportInterval = table.Column<string>(type: "text", nullable: false),
                    ReportableChange = table.Column<string>(type: "text", nullable: false),
                    Endpoint = table.Column<string>(type: "text", nullable: false),
                    Changed = table.Column<bool>(type: "boolean", nullable: false),
                    IsTemplate = table.Column<bool>(type: "boolean", nullable: false),
                    DeviceId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguredReporting", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConfiguredReporting_Device_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Device",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DeviceFilter",
                columns: table => new
                {
                    SysCreated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SysModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SysRemoved = table.Column<bool>(type: "boolean", nullable: false),
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FilterValue = table.Column<string>(type: "text", nullable: false),
                    DeviceId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceFilter", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeviceFilter_Device_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Device",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Option",
                columns: table => new
                {
                    SysCreated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SysModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SysRemoved = table.Column<bool>(type: "boolean", nullable: false),
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Description = table.Column<string>(type: "text", nullable: false),
                    CurrentValue = table.Column<string>(type: "text", nullable: false),
                    Property = table.Column<string>(type: "text", nullable: false),
                    IsProcessed = table.Column<bool>(type: "boolean", nullable: false),
                    DeviceId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Option", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Option_Device_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Device",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConfiguredReporting_DeviceId",
                table: "ConfiguredReporting",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_Device_Address",
                table: "Device",
                column: "Address",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Device_DeviceTemplateId",
                table: "Device",
                column: "DeviceTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_Device_Name",
                table: "Device",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeviceFilter_DeviceId",
                table: "DeviceFilter",
                column: "DeviceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeviceTemplate_ModelId",
                table: "DeviceTemplate",
                column: "ModelId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeviceTemplate_Name",
                table: "DeviceTemplate",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Option_DeviceId",
                table: "Option",
                column: "DeviceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConfiguredReporting");

            migrationBuilder.DropTable(
                name: "DeviceFilter");

            migrationBuilder.DropTable(
                name: "OpenTherm");

            migrationBuilder.DropTable(
                name: "Option");

            migrationBuilder.DropTable(
                name: "Device");

            migrationBuilder.DropTable(
                name: "DeviceTemplate");
        }
    }
}

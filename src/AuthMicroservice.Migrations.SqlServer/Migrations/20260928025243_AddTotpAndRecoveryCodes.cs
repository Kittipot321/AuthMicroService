using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthMicroservice.Migrations.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddTotpAndRecoveryCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EmailTwoFactorEnabled",
                schema: "auth",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "TotpConfirmedAt",
                schema: "auth",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TotpEnabled",
                schema: "auth",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TotpSecretProtected",
                schema: "auth",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TwoFactorRecoveryCodes",
                schema: "auth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CodeHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Salt = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConsumedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TwoFactorRecoveryCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TwoFactorRecoveryCodes_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "auth",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TwoFactorRecoveryCodes_UserId_ConsumedAt",
                schema: "auth",
                table: "TwoFactorRecoveryCodes",
                columns: new[] { "UserId", "ConsumedAt" });

            // Backfill: existing users with TwoFactorEnabled=true were email-only, preserve that method.
            migrationBuilder.Sql(
                "UPDATE [auth].[Users] SET [EmailTwoFactorEnabled] = 1 WHERE [TwoFactorEnabled] = 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TwoFactorRecoveryCodes",
                schema: "auth");

            migrationBuilder.DropColumn(
                name: "EmailTwoFactorEnabled",
                schema: "auth",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TotpConfirmedAt",
                schema: "auth",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TotpEnabled",
                schema: "auth",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TotpSecretProtected",
                schema: "auth",
                table: "Users");
        }
    }
}

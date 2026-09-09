using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planaffe.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceLogin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "device_login",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_code_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    user_code = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    approved_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    denied_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    redeemed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    issued_token_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_device_login", x => x.id);
                    table.ForeignKey(
                        name: "fk_device_login_user",
                        column: x => x.approved_by_user_id,
                        principalTable: "identity",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "device_login_code",
                table: "device_login",
                column: "device_code_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "device_login_expiry",
                table: "device_login",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "one_live_login_per_code",
                table: "device_login",
                column: "user_code",
                unique: true,
                filter: "approved_at is null and denied_at is null and redeemed_at is null");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "device_login");
        }
    }
}

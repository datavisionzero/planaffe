using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planaffe.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AllowAgentTokenRotation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "token_agent",
                table: "token");

            migrationBuilder.CreateIndex(
                name: "token_agent",
                table: "token",
                column: "identity_id",
                unique: true,
                filter: "kind = 'agent' and revoked_at is null");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "token_agent",
                table: "token");

            migrationBuilder.CreateIndex(
                name: "token_agent",
                table: "token",
                column: "identity_id",
                unique: true,
                filter: "kind = 'agent'");
        }
    }
}

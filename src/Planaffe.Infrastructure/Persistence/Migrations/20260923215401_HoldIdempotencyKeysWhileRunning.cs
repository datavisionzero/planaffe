using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planaffe.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// A write holds its idempotency key while it runs: the row is written
    /// first, with no status, and completed with the answer — so <c>status</c>
    /// may be empty — and the answer's <c>Location</c> and <c>ETag</c> are kept
    /// with it. The purge finds the old rows by <c>created_at</c>, which gets
    /// its index (<c>docs/storage.md</c>, Idempotency).
    /// </summary>
    public partial class HoldIdempotencyKeysWhileRunning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<short>(
                name: "status",
                table: "idempotency",
                type: "smallint",
                nullable: true,
                oldClrType: typeof(short),
                oldType: "smallint");

            migrationBuilder.AddColumn<string>(
                name: "etag",
                table: "idempotency",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "location",
                table: "idempotency",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "idempotency_created_at",
                table: "idempotency",
                column: "created_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idempotency_created_at",
                table: "idempotency");

            migrationBuilder.DropColumn(
                name: "etag",
                table: "idempotency");

            migrationBuilder.DropColumn(
                name: "location",
                table: "idempotency");

            migrationBuilder.AlterColumn<short>(
                name: "status",
                table: "idempotency",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0,
                oldClrType: typeof(short),
                oldType: "smallint",
                oldNullable: true);
        }
    }
}

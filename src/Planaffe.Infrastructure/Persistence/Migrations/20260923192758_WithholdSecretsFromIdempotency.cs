using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planaffe.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// A write whose answer carries a secret shown once keeps only its status
    /// in <c>idempotency</c> from now on (<c>docs/storage.md</c>). The rows
    /// written before kept the whole answer — user tokens, agent tokens,
    /// device codes, access links — and go.
    /// </summary>
    /// <remarks>
    /// Every row goes, not only those that carry a secret: a row does not say
    /// which route it answered, and guessing it from the shape of a body would
    /// be the one filter that must not miss. What is lost is a replay window of
    /// at most a day for writes answered before the upgrade.
    /// </remarks>
    public partial class WithholdSecretsFromIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "withheld",
                table: "idempotency",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("delete from idempotency;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "withheld",
                table: "idempotency");
        }
    }
}

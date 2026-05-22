using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SHPE_api.Migrations
{
    /// <inheritdoc />
    public partial class Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_log",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_person_id = table.Column<Guid>(type: "uuid", nullable: true),
                    action = table.Column<string>(type: "text", nullable: false),
                    target_type = table.Column<string>(type: "text", nullable: true),
                    target_id = table.Column<Guid>(type: "uuid", nullable: true),
                    details = table.Column<string>(type: "text", nullable: true),
                    ip_address = table.Column<string>(type: "text", nullable: true),
                    user_agent = table.Column<string>(type: "text", nullable: true),
                    at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_log", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "auth_account",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "text", nullable: false),
                    provider_user_id = table.Column<string>(type: "text", nullable: false),
                    email_verified = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_auth_account", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "person",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    full_name = table.Column<string>(type: "text", nullable: false),
                    email = table.Column<string>(type: "text", nullable: true),
                    gsu_email = table.Column<string>(type: "text", nullable: true),
                    phone = table.Column<string>(type: "text", nullable: true),
                    grad_year = table.Column<string>(type: "text", nullable: true),
                    linkedin_url = table.Column<string>(type: "text", nullable: true),
                    sms_opt_in = table.Column<bool>(type: "boolean", nullable: false),
                    share_contact = table.Column<bool>(type: "boolean", nullable: false),
                    photo_opt_out = table.Column<bool>(type: "boolean", nullable: false),
                    source = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_person", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "person_role",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "text", nullable: false),
                    assigned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_person_role", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_auth_account_provider_provider_user_id",
                table: "auth_account",
                columns: new[] { "provider", "provider_user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_person_email",
                table: "person",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_person_gsu_email",
                table: "person",
                column: "gsu_email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_person_role_person_id_role",
                table: "person_role",
                columns: new[] { "person_id", "role" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_log");

            migrationBuilder.DropTable(
                name: "auth_account");

            migrationBuilder.DropTable(
                name: "person");

            migrationBuilder.DropTable(
                name: "person_role");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SHPE_api.Migrations
{
    /// <inheritdoc />
    public partial class AddFileRecord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "file_record",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    spaces_key = table.Column<string>(type: "text", nullable: false),
                    cdn_url = table.Column<string>(type: "text", nullable: false),
                    mime_type = table.Column<string>(type: "text", nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    uploaded_by = table.Column<Guid>(type: "uuid", nullable: true),
                    uploaded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_file_record", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "file_record");
        }
    }
}

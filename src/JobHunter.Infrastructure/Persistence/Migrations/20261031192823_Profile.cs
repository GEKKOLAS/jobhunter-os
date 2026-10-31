using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobHunter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Profile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "evidence_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    organization = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    outcome = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    skill_ids = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    links = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_evidence_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_evidence_items_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "profile_skills",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    category = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    level = table.Column<int>(type: "integer", nullable: false),
                    years = table.Column<decimal>(type: "numeric(3,1)", precision: 3, scale: 1, nullable: false),
                    last_used_year = table.Column<int>(type: "integer", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_profile_skills", x => x.id);
                    table.ForeignKey(
                        name: "fk_profile_skills_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    headline = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    location = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    target_roles = table.Column<List<string>>(type: "text[]", nullable: false),
                    seniority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    work_modes = table.Column<List<string>>(type: "text[]", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    languages = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_profiles", x => x.id);
                    table.ForeignKey(
                        name: "fk_profiles_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_evidence_items_user_id",
                table: "evidence_items",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_profile_skills_user_id_key",
                table: "profile_skills",
                columns: new[] { "user_id", "key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_profiles_user_id",
                table: "profiles",
                column: "user_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "evidence_items");

            migrationBuilder.DropTable(
                name: "profile_skills");

            migrationBuilder.DropTable(
                name: "profiles");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobHunter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Opportunities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "companies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "citext", maxLength: 120, nullable: false),
                    website = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    domain = table.Column<string>(type: "character varying(253)", maxLength: 253, nullable: true),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_companies", x => x.id);
                    table.ForeignKey(
                        name: "fk_companies_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "opportunities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    location = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    work_mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    employment_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    salary_min = table.Column<int>(type: "integer", nullable: true),
                    salary_max = table.Column<int>(type: "integer", nullable: true),
                    salary_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    salary_period = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    source_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    source_ref = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    description = table.Column<string>(type: "character varying(50000)", maxLength: 50000, nullable: false),
                    stage = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    previous_stage = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    priority = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    priority_set_by = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    applied_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    first_response_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    archived_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_opportunities", x => x.id);
                    table.ForeignKey(
                        name: "fk_opportunities_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_opportunities_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "opportunity_stage_changes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    opportunity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_stage = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    to_stage = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_opportunity_stage_changes", x => x.id);
                    table.ForeignKey(
                        name: "fk_opportunity_stage_changes_opportunities_opportunity_id",
                        column: x => x.opportunity_id,
                        principalTable: "opportunities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_companies_user_id_domain",
                table: "companies",
                columns: new[] { "user_id", "domain" },
                unique: true,
                filter: "domain IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_companies_user_id_name",
                table: "companies",
                columns: new[] { "user_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_opportunities_company_id",
                table: "opportunities",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ix_opportunities_user_id_source_source_ref",
                table: "opportunities",
                columns: new[] { "user_id", "source", "source_ref" },
                unique: true,
                filter: "source_ref IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_opportunities_user_id_stage",
                table: "opportunities",
                columns: new[] { "user_id", "stage" },
                filter: "archived_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_opportunity_stage_changes_opportunity_id_occurred_at",
                table: "opportunity_stage_changes",
                columns: new[] { "opportunity_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_opportunity_stage_changes_user_id_occurred_at",
                table: "opportunity_stage_changes",
                columns: new[] { "user_id", "occurred_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "opportunity_stage_changes");

            migrationBuilder.DropTable(
                name: "opportunities");

            migrationBuilder.DropTable(
                name: "companies");
        }
    }
}

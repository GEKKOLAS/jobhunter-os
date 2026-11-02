using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobHunter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Matching : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fit_assessments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    opportunity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    score = table.Column<int>(type: "integer", nullable: true),
                    band = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    missing_reason = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    algorithm_version = table.Column<int>(type: "integer", nullable: false),
                    computed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    notes = table.Column<string>(type: "jsonb", nullable: true),
                    outcomes = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fit_assessments", x => x.id);
                    table.ForeignKey(
                        name: "fk_fit_assessments_opportunities_opportunity_id",
                        column: x => x.opportunity_id,
                        principalTable: "opportunities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_fit_assessments_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "opportunity_requirements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    opportunity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    kind = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    skill_key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    years_min = table.Column<int>(type: "integer", nullable: true),
                    origin = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_opportunity_requirements", x => x.id);
                    table.ForeignKey(
                        name: "fk_opportunity_requirements_opportunities_opportunity_id",
                        column: x => x.opportunity_id,
                        principalTable: "opportunities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_opportunity_requirements_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_fit_assessments_opportunity_id",
                table: "fit_assessments",
                column: "opportunity_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_fit_assessments_user_id",
                table: "fit_assessments",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_opportunity_requirements_opportunity_id_position",
                table: "opportunity_requirements",
                columns: new[] { "opportunity_id", "position" });

            migrationBuilder.CreateIndex(
                name: "ix_opportunity_requirements_user_id",
                table: "opportunity_requirements",
                column: "user_id",
                filter: "confirmed");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fit_assessments");

            migrationBuilder.DropTable(
                name: "opportunity_requirements");
        }
    }
}

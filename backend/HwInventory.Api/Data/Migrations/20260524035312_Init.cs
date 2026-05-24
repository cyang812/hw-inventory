using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HwInventory.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "categories",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    slug = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    parent_id = table.Column<int>(type: "INTEGER", nullable: true),
                    icon = table.Column<string>(type: "TEXT", nullable: true),
                    description = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_categories", x => x.id);
                    table.ForeignKey(
                        name: "fk_categories_categories_parent_id",
                        column: x => x.parent_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "hardware",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    manufacturer = table.Column<string>(type: "TEXT", nullable: true),
                    model = table.Column<string>(type: "TEXT", nullable: true),
                    serial_number = table.Column<string>(type: "TEXT", nullable: true),
                    sku = table.Column<string>(type: "TEXT", nullable: true),
                    asset_tag = table.Column<string>(type: "TEXT", nullable: true),
                    revision = table.Column<string>(type: "TEXT", nullable: true),
                    identifiers = table.Column<string>(type: "TEXT", nullable: true),
                    specs = table.Column<string>(type: "TEXT", nullable: true),
                    links = table.Column<string>(type: "TEXT", nullable: true),
                    acquired_at = table.Column<long>(type: "INTEGER", nullable: true),
                    purchased_from = table.Column<string>(type: "TEXT", nullable: true),
                    purchase_url = table.Column<string>(type: "TEXT", nullable: true),
                    cost = table.Column<decimal>(type: "TEXT", nullable: true),
                    currency = table.Column<string>(type: "TEXT", nullable: true),
                    warranty_expires_at = table.Column<long>(type: "INTEGER", nullable: true),
                    location = table.Column<string>(type: "TEXT", nullable: true),
                    condition = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    notes = table.Column<string>(type: "TEXT", nullable: true),
                    last_used_at = table.Column<long>(type: "INTEGER", nullable: true),
                    last_activity_at = table.Column<long>(type: "INTEGER", nullable: true),
                    archived_at = table.Column<long>(type: "INTEGER", nullable: true),
                    created_at = table.Column<long>(type: "INTEGER", nullable: false),
                    updated_at = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_hardware", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "projects",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    slug = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "TEXT", nullable: true),
                    status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    priority = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    started_at = table.Column<long>(type: "INTEGER", nullable: true),
                    target_date = table.Column<long>(type: "INTEGER", nullable: true),
                    completed_at = table.Column<long>(type: "INTEGER", nullable: true),
                    notes = table.Column<string>(type: "TEXT", nullable: true),
                    links = table.Column<string>(type: "TEXT", nullable: true),
                    archived_at = table.Column<long>(type: "INTEGER", nullable: true),
                    created_at = table.Column<long>(type: "INTEGER", nullable: false),
                    updated_at = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_projects", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tags",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    color = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tags", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "hardware_category",
                columns: table => new
                {
                    categories_id = table.Column<int>(type: "INTEGER", nullable: false),
                    hardware_id = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_hardware_category", x => new { x.categories_id, x.hardware_id });
                    table.ForeignKey(
                        name: "fk_hardware_category_categories_categories_id",
                        column: x => x.categories_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_hardware_category_hardware_hardware_id",
                        column: x => x.hardware_id,
                        principalTable: "hardware",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "loans",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    hardware_id = table.Column<int>(type: "INTEGER", nullable: false),
                    loaned_to = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    loaned_at = table.Column<long>(type: "INTEGER", nullable: false),
                    due_at = table.Column<long>(type: "INTEGER", nullable: true),
                    returned_at = table.Column<long>(type: "INTEGER", nullable: true),
                    notes = table.Column<string>(type: "TEXT", nullable: true),
                    created_at = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_loans", x => x.id);
                    table.ForeignKey(
                        name: "fk_loans_hardware_hardware_id",
                        column: x => x.hardware_id,
                        principalTable: "hardware",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "activities",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    hardware_id = table.Column<int>(type: "INTEGER", nullable: false),
                    project_id = table.Column<int>(type: "INTEGER", nullable: true),
                    kind = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    description = table.Column<string>(type: "TEXT", nullable: true),
                    metadata = table.Column<string>(type: "TEXT", nullable: true),
                    occurred_at = table.Column<long>(type: "INTEGER", nullable: false),
                    created_at = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_activities", x => x.id);
                    table.ForeignKey(
                        name: "fk_activities_hardware_hardware_id",
                        column: x => x.hardware_id,
                        principalTable: "hardware",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_activities_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "hardware_projects",
                columns: table => new
                {
                    hardware_id = table.Column<int>(type: "INTEGER", nullable: false),
                    project_id = table.Column<int>(type: "INTEGER", nullable: false),
                    role = table.Column<string>(type: "TEXT", maxLength: 60, nullable: true),
                    created_at = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_hardware_projects", x => new { x.hardware_id, x.project_id });
                    table.ForeignKey(
                        name: "fk_hardware_projects_hardware_hardware_id",
                        column: x => x.hardware_id,
                        principalTable: "hardware",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_hardware_projects_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "hardware_tag",
                columns: table => new
                {
                    hardware_id = table.Column<int>(type: "INTEGER", nullable: false),
                    tags_id = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_hardware_tag", x => new { x.hardware_id, x.tags_id });
                    table.ForeignKey(
                        name: "fk_hardware_tag_hardware_hardware_id",
                        column: x => x.hardware_id,
                        principalTable: "hardware",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_hardware_tag_tags_tags_id",
                        column: x => x.tags_id,
                        principalTable: "tags",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "attachments",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    hardware_id = table.Column<int>(type: "INTEGER", nullable: true),
                    project_id = table.Column<int>(type: "INTEGER", nullable: true),
                    activity_id = table.Column<int>(type: "INTEGER", nullable: true),
                    storage_backend = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    storage_key = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    original_filename = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    mime_type = table.Column<string>(type: "TEXT", nullable: false),
                    size_bytes = table.Column<long>(type: "INTEGER", nullable: false),
                    sha256 = table.Column<string>(type: "TEXT", nullable: true),
                    label = table.Column<string>(type: "TEXT", nullable: true),
                    created_at = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_attachments", x => x.id);
                    table.ForeignKey(
                        name: "fk_attachments_activities_activity_id",
                        column: x => x.activity_id,
                        principalTable: "activities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_attachments_hardware_hardware_id",
                        column: x => x.hardware_id,
                        principalTable: "hardware",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_attachments_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "hardware_configs",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    hardware_id = table.Column<int>(type: "INTEGER", nullable: false),
                    kind = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    version = table.Column<string>(type: "TEXT", nullable: true),
                    notes = table.Column<string>(type: "TEXT", nullable: true),
                    installed_at = table.Column<long>(type: "INTEGER", nullable: true),
                    is_current = table.Column<bool>(type: "INTEGER", nullable: false),
                    activity_id = table.Column<int>(type: "INTEGER", nullable: true),
                    created_at = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_hardware_configs", x => x.id);
                    table.ForeignKey(
                        name: "fk_hardware_configs_activities_activity_id",
                        column: x => x.activity_id,
                        principalTable: "activities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_hardware_configs_hardware_hardware_id",
                        column: x => x.hardware_id,
                        principalTable: "hardware",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_activities_hardware_id_occurred_at",
                table: "activities",
                columns: new[] { "hardware_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_activities_occurred_at",
                table: "activities",
                column: "occurred_at");

            migrationBuilder.CreateIndex(
                name: "ix_activities_project_id",
                table: "activities",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_attachments_activity_id",
                table: "attachments",
                column: "activity_id");

            migrationBuilder.CreateIndex(
                name: "ix_attachments_hardware_id",
                table: "attachments",
                column: "hardware_id");

            migrationBuilder.CreateIndex(
                name: "ix_attachments_project_id",
                table: "attachments",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_categories_parent_id",
                table: "categories",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "ix_categories_slug",
                table: "categories",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_hardware_archived_at",
                table: "hardware",
                column: "archived_at");

            migrationBuilder.CreateIndex(
                name: "ix_hardware_last_activity_at",
                table: "hardware",
                column: "last_activity_at");

            migrationBuilder.CreateIndex(
                name: "ix_hardware_last_used_at",
                table: "hardware",
                column: "last_used_at");

            migrationBuilder.CreateIndex(
                name: "ix_hardware_status",
                table: "hardware",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_hardware_category_hardware_id",
                table: "hardware_category",
                column: "hardware_id");

            migrationBuilder.CreateIndex(
                name: "ix_hardware_configs_activity_id",
                table: "hardware_configs",
                column: "activity_id");

            migrationBuilder.CreateIndex(
                name: "ix_hardware_configs_hardware_id_kind_is_current",
                table: "hardware_configs",
                columns: new[] { "hardware_id", "kind", "is_current" });

            migrationBuilder.CreateIndex(
                name: "ix_hardware_projects_project_id",
                table: "hardware_projects",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_hardware_tag_tags_id",
                table: "hardware_tag",
                column: "tags_id");

            migrationBuilder.CreateIndex(
                name: "ix_loans_due_at",
                table: "loans",
                column: "due_at");

            migrationBuilder.CreateIndex(
                name: "ix_loans_hardware_id",
                table: "loans",
                column: "hardware_id");

            migrationBuilder.CreateIndex(
                name: "ix_loans_returned_at",
                table: "loans",
                column: "returned_at");

            migrationBuilder.CreateIndex(
                name: "ix_projects_archived_at",
                table: "projects",
                column: "archived_at");

            migrationBuilder.CreateIndex(
                name: "ix_projects_slug",
                table: "projects",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_projects_status",
                table: "projects",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_tags_name",
                table: "tags",
                column: "name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "attachments");

            migrationBuilder.DropTable(
                name: "hardware_category");

            migrationBuilder.DropTable(
                name: "hardware_configs");

            migrationBuilder.DropTable(
                name: "hardware_projects");

            migrationBuilder.DropTable(
                name: "hardware_tag");

            migrationBuilder.DropTable(
                name: "loans");

            migrationBuilder.DropTable(
                name: "categories");

            migrationBuilder.DropTable(
                name: "activities");

            migrationBuilder.DropTable(
                name: "tags");

            migrationBuilder.DropTable(
                name: "hardware");

            migrationBuilder.DropTable(
                name: "projects");
        }
    }
}

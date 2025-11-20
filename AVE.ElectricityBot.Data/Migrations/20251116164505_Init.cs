using System;
using AVE.ElectricityBot.Data.Entities;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AVE.ElectricityBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "channel_scraping_states",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    channel_username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    last_processed_message_id = table.Column<long>(type: "bigint", nullable: false),
                    last_processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_scrape_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    total_processed_messages = table.Column<int>(type: "integer", nullable: false),
                    partition_key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_channel_scraping_states", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "schedule_history",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    recorded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    schedule_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    total_outage_minutes = table.Column<int>(type: "integer", nullable: false),
                    outage_periods = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    slots_json = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    group_name = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    partition_key = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_schedule_history", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    telegram_user_id = table.Column<long>(type: "bigint", nullable: false),
                    telegram_chat_id = table.Column<long>(type: "bigint", nullable: false),
                    username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    first_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    last_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    group_name = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    dso_id = table.Column<int>(type: "integer", nullable: true),
                    address = table.Column<UserAddress>(type: "jsonb", nullable: true),
                    settings = table.Column<UserSettings>(type: "jsonb", nullable: true),
                    first_seen_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_active_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    commands_count = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    partition_key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_channel_scraping_states_channel_username",
                table: "channel_scraping_states",
                column: "channel_username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_channel_scraping_states_last_scrape_at",
                table: "channel_scraping_states",
                column: "last_scrape_at");

            migrationBuilder.CreateIndex(
                name: "ix_schedule_history_group_name",
                table: "schedule_history",
                column: "group_name");

            migrationBuilder.CreateIndex(
                name: "ix_schedule_history_recorded_at",
                table: "schedule_history",
                column: "recorded_at");

            migrationBuilder.CreateIndex(
                name: "ix_schedule_history_schedule_date_group_name",
                table: "schedule_history",
                columns: new[] { "schedule_date", "group_name" });

            migrationBuilder.CreateIndex(
                name: "ix_users_dso_id",
                table: "users",
                column: "dso_id");

            migrationBuilder.CreateIndex(
                name: "ix_users_group_name",
                table: "users",
                column: "group_name");

            migrationBuilder.CreateIndex(
                name: "ix_users_is_active",
                table: "users",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_users_last_active_at",
                table: "users",
                column: "last_active_at");

            migrationBuilder.CreateIndex(
                name: "ix_users_telegram_chat_id",
                table: "users",
                column: "telegram_chat_id");

            migrationBuilder.CreateIndex(
                name: "ix_users_telegram_user_id",
                table: "users",
                column: "telegram_user_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "channel_scraping_states");

            migrationBuilder.DropTable(
                name: "schedule_history");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}

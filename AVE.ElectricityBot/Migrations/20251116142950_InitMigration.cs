using System;
using AVE.ElectricityBot.Data.Entities;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AVE.ElectricityBot.Migrations
{
    /// <inheritdoc />
    public partial class InitMigration : Migration
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

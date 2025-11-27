CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    migration_id character varying(150) NOT NULL,
    product_version character varying(32) NOT NULL,
    CONSTRAINT pk___ef_migrations_history PRIMARY KEY (migration_id)
);

START TRANSACTION;
CREATE TABLE channel_scraping_states (
    id text NOT NULL,
    channel_username character varying(100) NOT NULL,
    last_processed_message_id bigint NOT NULL,
    last_processed_at timestamp with time zone NOT NULL,
    last_scrape_at timestamp with time zone NOT NULL,
    total_processed_messages integer NOT NULL,
    partition_key character varying(50) NOT NULL,
    CONSTRAINT pk_channel_scraping_states PRIMARY KEY (id)
);

CREATE TABLE schedule_history (
    id text NOT NULL,
    recorded_at timestamp with time zone NOT NULL,
    schedule_date timestamp with time zone NOT NULL,
    total_outage_minutes integer NOT NULL,
    outage_periods integer NOT NULL,
    status character varying(50) NOT NULL,
    slots_json character varying(2000) NOT NULL,
    group_name character varying(10) NOT NULL,
    partition_key character varying(20) NOT NULL,
    CONSTRAINT pk_schedule_history PRIMARY KEY (id)
);

CREATE TABLE users (
    id text NOT NULL,
    telegram_user_id bigint NOT NULL,
    telegram_chat_id bigint NOT NULL,
    username character varying(100),
    first_name character varying(100),
    last_name character varying(100),
    group_name character varying(10),
    dso_id integer,
    address jsonb,
    settings jsonb,
    first_seen_at timestamp with time zone NOT NULL,
    last_active_at timestamp with time zone NOT NULL,
    commands_count integer NOT NULL,
    is_active boolean NOT NULL,
    partition_key character varying(50) NOT NULL,
    CONSTRAINT pk_users PRIMARY KEY (id)
);

CREATE UNIQUE INDEX ix_channel_scraping_states_channel_username ON channel_scraping_states (channel_username);

CREATE INDEX ix_channel_scraping_states_last_scrape_at ON channel_scraping_states (last_scrape_at);

CREATE INDEX ix_schedule_history_group_name ON schedule_history (group_name);

CREATE INDEX ix_schedule_history_recorded_at ON schedule_history (recorded_at);

CREATE INDEX ix_schedule_history_schedule_date_group_name ON schedule_history (schedule_date, group_name);

CREATE INDEX ix_users_dso_id ON users (dso_id);

CREATE INDEX ix_users_group_name ON users (group_name);

CREATE INDEX ix_users_is_active ON users (is_active);

CREATE INDEX ix_users_last_active_at ON users (last_active_at);

CREATE INDEX ix_users_telegram_chat_id ON users (telegram_chat_id);

CREATE UNIQUE INDEX ix_users_telegram_user_id ON users (telegram_user_id);

INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
VALUES ('20251116164505_Init', '10.0.0');

COMMIT;


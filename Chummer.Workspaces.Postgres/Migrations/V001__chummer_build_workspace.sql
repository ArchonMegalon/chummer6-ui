CREATE SCHEMA IF NOT EXISTS chummer_build;
REVOKE ALL ON SCHEMA chummer_build FROM PUBLIC;

CREATE TABLE chummer_build.workspaces (
    owner_key bytea NOT NULL CHECK (octet_length(owner_key) = 32),
    workspace_id text NOT NULL CHECK (char_length(workspace_id) BETWEEN 1 AND 256),
    document_json jsonb NOT NULL CHECK (jsonb_typeof(document_json) = 'object'),
    document_sha256 bytea NOT NULL CHECK (octet_length(document_sha256) = 32),
    content_revision bigint NOT NULL CHECK (content_revision > 0),
    saved_revision bigint NOT NULL CHECK (
        saved_revision >= 0
        AND saved_revision <= content_revision),
    updated_at_utc timestamptz NOT NULL DEFAULT clock_timestamp(),
    PRIMARY KEY (owner_key, workspace_id)
);

CREATE INDEX ix_chummer_build_workspaces_owner_updated
    ON chummer_build.workspaces(owner_key, updated_at_utc DESC, workspace_id);

REVOKE ALL ON ALL TABLES IN SCHEMA chummer_build FROM PUBLIC;
REVOKE ALL ON ALL SEQUENCES IN SCHEMA chummer_build FROM PUBLIC;

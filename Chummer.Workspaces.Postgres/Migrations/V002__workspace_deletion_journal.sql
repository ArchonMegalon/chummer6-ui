CREATE TABLE chummer_build.workspace_deletion_journal (
    operation_id uuid PRIMARY KEY,
    owner_key bytea NOT NULL
        CONSTRAINT deletion_journal_owner_key_length CHECK (octet_length(owner_key) = 32),
    subject_kind text NOT NULL
        CONSTRAINT deletion_journal_subject_kind CHECK (subject_kind IN ('workspace', 'owner')),
    subject_key bytea NOT NULL
        CONSTRAINT deletion_journal_subject_key_length CHECK (octet_length(subject_key) = 32),
    content_revision bigint NULL
        CONSTRAINT deletion_journal_content_revision CHECK (content_revision IS NULL OR content_revision > 0),
    deleted_at_utc timestamptz NOT NULL,
    replay_expires_at_utc timestamptz NOT NULL
        CONSTRAINT deletion_journal_replay_window CHECK (replay_expires_at_utc > deleted_at_utc),
    audit_expires_at_utc timestamptz NOT NULL
        CONSTRAINT deletion_journal_audit_window CHECK (audit_expires_at_utc > replay_expires_at_utc),
    receipt_sha256 bytea NOT NULL
        CONSTRAINT deletion_journal_receipt_length CHECK (octet_length(receipt_sha256) = 32)
);

CREATE INDEX ix_chummer_build_workspace_deletion_replay
    ON chummer_build.workspace_deletion_journal(
        owner_key,
        replay_expires_at_utc,
        subject_kind,
        subject_key);

CREATE INDEX ix_chummer_build_workspace_deletion_audit_expiry
    ON chummer_build.workspace_deletion_journal(audit_expires_at_utc);

REVOKE ALL ON chummer_build.workspace_deletion_journal FROM PUBLIC;

-- Migration: Add pinned projects support to project_members
-- Date: 2026-04-08

ALTER TABLE project_members
    ADD COLUMN IF NOT EXISTS prm_is_pinned BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS prm_pinned_at TIMESTAMP NULL;

-- Index to quickly fetch pinned projects per user
CREATE INDEX IF NOT EXISTS idx_project_members_pinned
    ON project_members (prm_user_id, prm_is_pinned)
    WHERE prm_is_pinned = TRUE AND prm_is_deleted = FALSE;

-- Fix prm_pinned_at column type: align with all other timestamp columns (TIMESTAMPTZ)
ALTER TABLE project_members
    ALTER COLUMN prm_pinned_at TYPE TIMESTAMPTZ;

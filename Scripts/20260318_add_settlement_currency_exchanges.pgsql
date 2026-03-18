-- Migration: Add sce_settlement_id to split_currency_exchanges
-- Allows settlements to store their amounts in alternative project currencies.
-- Uses the existing XOR pattern: exactly one of (expense, income, settlement) FK must be set.

-- 1. Add nullable FK column
ALTER TABLE split_currency_exchanges
    ADD COLUMN IF NOT EXISTS sce_settlement_id UUID
        REFERENCES partner_settlements(pst_id) ON DELETE CASCADE;

-- 2. Unique filtered index (prevents duplicate currency per settlement)
CREATE UNIQUE INDEX IF NOT EXISTS idx_sce_settlement_currency
    ON split_currency_exchanges (sce_settlement_id, sce_currency_code)
    WHERE sce_settlement_id IS NOT NULL;

-- 3. Drop old XOR constraint (2-way) and replace with 3-way
ALTER TABLE split_currency_exchanges
    DROP CONSTRAINT IF EXISTS chk_sce_mutex;

ALTER TABLE split_currency_exchanges
    DROP CONSTRAINT IF EXISTS split_currency_exchanges_source_check;

ALTER TABLE split_currency_exchanges
    ADD CONSTRAINT split_currency_exchanges_source_check CHECK (
        (sce_expense_split_id IS NOT NULL)::int
        + (sce_income_split_id IS NOT NULL)::int
        + (sce_settlement_id IS NOT NULL)::int = 1
    );

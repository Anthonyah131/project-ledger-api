-- Add active state for expenses and incomes.
-- false means reminder/draft-like transaction that should not be counted in totals.

ALTER TABLE expenses
    ADD COLUMN IF NOT EXISTS exp_is_active BOOLEAN NOT NULL DEFAULT TRUE;

ALTER TABLE incomes
    ADD COLUMN IF NOT EXISTS inc_is_active BOOLEAN NOT NULL DEFAULT TRUE;

CREATE INDEX IF NOT EXISTS idx_expenses_exp_is_active ON expenses(exp_is_active);
CREATE INDEX IF NOT EXISTS idx_incomes_inc_is_active ON incomes(inc_is_active);

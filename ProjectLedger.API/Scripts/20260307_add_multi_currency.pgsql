-- ============================================================
-- Migration: Add multi-currency support
-- Date: 2026-03-07
--
-- Changes:
--   1. Drop legacy single alt-currency columns from expenses
--   2. Create project_alternative_currencies table
--   3. Create incomes table
--   4. Create transaction_currency_exchanges table (FK design, requires incomes)
--   5. Update plan_limits JSONB with new limit keys
-- ============================================================

-- ── 1. Drop legacy alt-currency columns from expenses ───────
-- CockroachDB requires dropping dependent objects explicitly first.

-- 1a. Drop the partial index that references exp_alt_currency
DROP INDEX IF EXISTS idx_exp_alt_currency;

-- 1b. Drop the FK constraint referencing currencies(cur_code)
ALTER TABLE expenses
    DROP CONSTRAINT IF EXISTS expenses_alt_currency_fkey;

-- 1c. Drop the CHECK constraint on the alt-currency triple
ALTER TABLE expenses
    DROP CONSTRAINT IF EXISTS expenses_alt_currency_check;

-- 1d. Drop the columns
ALTER TABLE expenses
    DROP COLUMN IF EXISTS exp_alt_currency,
    DROP COLUMN IF EXISTS exp_alt_exchange_rate,
    DROP COLUMN IF EXISTS exp_alt_amount;

-- ── 2. project_alternative_currencies ───────────────────────
CREATE TABLE IF NOT EXISTS project_alternative_currencies (
    pac_id           UUID        NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    pac_project_id   UUID        NOT NULL REFERENCES projects(prj_id) ON DELETE NO ACTION,
    pac_currency_code VARCHAR(3) NOT NULL REFERENCES currencies(cur_code) ON DELETE NO ACTION,
    pac_created_at   TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uq_pac_project_currency UNIQUE (pac_project_id, pac_currency_code)
);

CREATE INDEX IF NOT EXISTS idx_pac_project_id ON project_alternative_currencies (pac_project_id);

COMMENT ON TABLE project_alternative_currencies IS
    'Monedas alternativas habilitadas por proyecto para visualización multi-divisa.';

-- ── 3. incomes ───────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS incomes (
    inc_id                  UUID          NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    inc_project_id          UUID          NOT NULL REFERENCES projects(prj_id)        ON DELETE NO ACTION,
    inc_category_id         UUID          NOT NULL REFERENCES categories(cat_id)      ON DELETE NO ACTION,
    inc_payment_method_id   UUID          NOT NULL REFERENCES payment_methods(pmt_id) ON DELETE NO ACTION,
    inc_created_by_user_id  UUID          NOT NULL REFERENCES users(usr_id)           ON DELETE NO ACTION,

    inc_original_amount     NUMERIC(14,2) NOT NULL,
    inc_original_currency   VARCHAR(3)    NOT NULL REFERENCES currencies(cur_code)    ON DELETE NO ACTION,
    inc_exchange_rate       NUMERIC(18,6) NOT NULL DEFAULT 1.000000,
    inc_converted_amount    NUMERIC(14,2) NOT NULL,

    inc_title               VARCHAR(255)  NOT NULL,
    inc_description         TEXT          NULL,
    inc_income_date         DATE          NOT NULL,
    inc_receipt_number      VARCHAR(100)  NULL,
    inc_notes               TEXT          NULL,

    inc_created_at          TIMESTAMPTZ   NOT NULL DEFAULT now(),
    inc_updated_at          TIMESTAMPTZ   NOT NULL DEFAULT now(),
    inc_is_deleted          BOOLEAN       NOT NULL DEFAULT FALSE,
    inc_deleted_at          TIMESTAMPTZ   NULL,
    inc_deleted_by_user_id  UUID          NULL REFERENCES users(usr_id) ON DELETE NO ACTION
);

CREATE INDEX IF NOT EXISTS idx_inc_project_id         ON incomes (inc_project_id);
CREATE INDEX IF NOT EXISTS idx_inc_category_id        ON incomes (inc_category_id);
CREATE INDEX IF NOT EXISTS idx_inc_payment_method_id  ON incomes (inc_payment_method_id);
CREATE INDEX IF NOT EXISTS idx_inc_created_by_user_id ON incomes (inc_created_by_user_id);
CREATE INDEX IF NOT EXISTS idx_inc_income_date        ON incomes (inc_income_date);
CREATE INDEX IF NOT EXISTS idx_inc_is_deleted         ON incomes (inc_is_deleted);

COMMENT ON TABLE incomes IS 'Ingresos financieros registrados por proyecto.';

-- ── 4. transaction_currency_exchanges ───────────────────────
-- Must be created AFTER both expenses and incomes exist.
CREATE TABLE IF NOT EXISTS transaction_currency_exchanges (
    tce_id               UUID           NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    tce_expense_id       UUID           NULL REFERENCES expenses(exp_id) ON DELETE CASCADE,
    tce_income_id        UUID           NULL REFERENCES incomes(inc_id)  ON DELETE CASCADE,
    tce_currency_code    VARCHAR(3)     NOT NULL REFERENCES currencies(cur_code) ON DELETE NO ACTION,
    tce_exchange_rate    NUMERIC(18,6)  NOT NULL,
    tce_converted_amount NUMERIC(14,2)  NOT NULL,
    tce_created_at       TIMESTAMPTZ    NOT NULL DEFAULT now(),

    -- Exactly one parent must be set
    CONSTRAINT chk_tce_one_parent CHECK (
        (tce_expense_id IS NOT NULL)::int + (tce_income_id IS NOT NULL)::int = 1
    ),

    -- One conversion per currency per transaction
    CONSTRAINT uq_tce_expense_currency
        UNIQUE (tce_expense_id, tce_currency_code),
    CONSTRAINT uq_tce_income_currency
        UNIQUE (tce_income_id, tce_currency_code)
);

CREATE INDEX IF NOT EXISTS idx_tce_expense_id ON transaction_currency_exchanges (tce_expense_id)
    WHERE tce_expense_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_tce_income_id  ON transaction_currency_exchanges (tce_income_id)
    WHERE tce_income_id IS NOT NULL;

COMMENT ON TABLE transaction_currency_exchanges IS
    'Conversiones de gastos/ingresos a monedas alternativas del proyecto.';

-- ── 5. Add new plan limits to existing plans ─────────────────
-- Adds keys max_alternative_currencies_per_project and max_incomes_per_month
-- to every plan that doesn't already have them.
-- Values: Free=3/10, Basic=10/100, Pro & Enterprise=-1 (unlimited)

UPDATE plans
SET pln_limits = pln_limits
    || CASE pln_name
           WHEN 'Free'       THEN '{"max_alternative_currencies_per_project": 3,  "max_incomes_per_month": 10}'
           WHEN 'Basic'      THEN '{"max_alternative_currencies_per_project": 10, "max_incomes_per_month": 100}'
           WHEN 'Pro'        THEN '{"max_alternative_currencies_per_project": -1, "max_incomes_per_month": -1}'
           WHEN 'Enterprise' THEN '{"max_alternative_currencies_per_project": -1, "max_incomes_per_month": -1}'
           ELSE               '{"max_alternative_currencies_per_project": -1, "max_incomes_per_month": -1}'
       END::jsonb
WHERE pln_limits IS NOT NULL;

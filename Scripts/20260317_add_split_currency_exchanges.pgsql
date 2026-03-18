-- ============================================================
-- Migration: add split_currency_exchanges table
-- Date: 2026-03-17
-- Purpose: Persist alternative-currency equivalencies for each
--          expense split and income split, proportional to the
--          parent transaction's currency exchanges.
-- ============================================================

CREATE TABLE IF NOT EXISTS split_currency_exchanges (
    sce_id                  UUID            NOT NULL DEFAULT gen_random_uuid(),
    sce_expense_split_id    UUID            NULL,
    sce_income_split_id     UUID            NULL,
    sce_currency_code       VARCHAR(10)     NOT NULL,
    sce_exchange_rate       NUMERIC(18, 6)  NOT NULL,
    sce_converted_amount    NUMERIC(18, 4)  NOT NULL,
    sce_created_at          TIMESTAMPTZ     NOT NULL DEFAULT NOW(),

    CONSTRAINT pk_split_currency_exchanges PRIMARY KEY (sce_id),

    -- Exactly one of the two FKs must be set (mutex constraint)
    CONSTRAINT chk_sce_mutex CHECK (
        (sce_expense_split_id IS NOT NULL AND sce_income_split_id IS NULL)
        OR
        (sce_expense_split_id IS NULL AND sce_income_split_id IS NOT NULL)
    ),

    CONSTRAINT fk_sce_expense_split
        FOREIGN KEY (sce_expense_split_id)
        REFERENCES expense_splits (exs_id)
        ON DELETE CASCADE,

    CONSTRAINT fk_sce_income_split
        FOREIGN KEY (sce_income_split_id)
        REFERENCES income_splits (ins_id)
        ON DELETE CASCADE,

    CONSTRAINT fk_sce_currency
        FOREIGN KEY (sce_currency_code)
        REFERENCES currencies (cur_code)
        ON DELETE NO ACTION
);

-- One exchange per currency per expense split
CREATE UNIQUE INDEX IF NOT EXISTS uix_sce_expense_split_currency
    ON split_currency_exchanges (sce_expense_split_id, sce_currency_code)
    WHERE sce_expense_split_id IS NOT NULL;

-- One exchange per currency per income split
CREATE UNIQUE INDEX IF NOT EXISTS uix_sce_income_split_currency
    ON split_currency_exchanges (sce_income_split_id, sce_currency_code)
    WHERE sce_income_split_id IS NOT NULL;

-- Index for fast lookups when deleting by parent transaction
CREATE INDEX IF NOT EXISTS ix_sce_expense_split_id
    ON split_currency_exchanges (sce_expense_split_id)
    WHERE sce_expense_split_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_sce_income_split_id
    ON split_currency_exchanges (sce_income_split_id)
    WHERE sce_income_split_id IS NOT NULL;

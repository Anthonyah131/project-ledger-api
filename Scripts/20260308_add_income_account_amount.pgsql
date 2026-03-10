-- ============================================================
-- Migration: Persist income destination account amount/currency
-- Date: 2026-03-08
-- ============================================================
-- Goal:
--  - Keep project-level accounting in project currency (inc_converted_amount)
--  - Also persist where the money actually landed (account currency/amount)
--
-- Notes:
--  - inc_account_amount may remain NULL for legacy rows that cannot be inferred.
--  - New API writes enforce this for new/updated incomes.
--  - For CockroachDB, run the backfill in a separate statement/script after this DDL.
-- ============================================================

ALTER TABLE public.incomes
    ADD COLUMN IF NOT EXISTS inc_account_amount NUMERIC(14,2);

ALTER TABLE public.incomes
    ADD COLUMN IF NOT EXISTS inc_account_currency VARCHAR(3);

-- ============================================================
-- Backfill: income destination account amount/currency
-- Date: 2026-03-08
-- ============================================================
-- Run this after:
--   20260308_add_income_account_amount.pgsql
--
-- Why separate file:
--   In CockroachDB, executing DDL + UPDATE in the same multi-statement batch
--   may validate the UPDATE before new columns are visible.
-- ============================================================

UPDATE public.incomes i
SET
    inc_account_currency = pm.pmt_currency,
    inc_account_amount = CASE
        WHEN pm.pmt_currency = i.inc_original_currency THEN i.inc_original_amount
        WHEN pm.pmt_currency = p.prj_currency_code THEN i.inc_converted_amount
        ELSE i.inc_account_amount
    END
FROM public.payment_methods pm,
     public.projects p
WHERE p.prj_id = i.inc_project_id
  AND i.inc_payment_method_id = pm.pmt_id
  AND (i.inc_account_currency IS NULL OR i.inc_account_amount IS NULL);

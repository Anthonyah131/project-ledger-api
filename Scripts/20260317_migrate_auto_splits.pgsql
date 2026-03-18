-- Fase 3a: Migración de splits para movimientos existentes
-- Crea un split 100% al partner dueño de la cuenta para cada gasto/ingreso sin split

-- Splits de gastos existentes: 100% al partner dueño de la cuenta
INSERT INTO expense_splits
  (exs_expense_id, exs_partner_id, exs_split_type, exs_split_value, exs_resolved_amount)
SELECT
  e.exp_id,
  pm.pmt_owner_partner_id,
  'percentage',
  100,
  e.exp_original_amount
FROM expenses e
JOIN payment_methods pm ON pm.pmt_id = e.exp_payment_method_id
WHERE e.exp_is_deleted = false
  AND pm.pmt_owner_partner_id IS NOT NULL
  AND e.exp_id NOT IN (SELECT DISTINCT exs_expense_id FROM expense_splits);

-- Splits de ingresos existentes: 100% al partner dueño de la cuenta
INSERT INTO income_splits
  (ins_income_id, ins_partner_id, ins_split_type, ins_split_value, ins_resolved_amount)
SELECT
  i.inc_id,
  pm.pmt_owner_partner_id,
  'percentage',
  100,
  i.inc_original_amount
FROM incomes i
JOIN payment_methods pm ON pm.pmt_id = i.inc_payment_method_id
WHERE i.inc_is_deleted = false
  AND pm.pmt_owner_partner_id IS NOT NULL
  AND i.inc_id NOT IN (SELECT DISTINCT ins_income_id FROM income_splits);

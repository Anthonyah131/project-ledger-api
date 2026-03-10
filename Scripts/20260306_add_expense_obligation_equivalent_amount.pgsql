-- Adds optional equivalent amount in obligation currency for cross-currency debt payments.
ALTER TABLE expenses
ADD COLUMN IF NOT EXISTS exp_obligation_equivalent_amount numeric(14,2) NULL;

COMMENT ON COLUMN expenses.exp_obligation_equivalent_amount IS
'Amount paid expressed in obligation currency for cross-currency obligation payments.';

-- Fix sce_currency_code column length: should be VARCHAR(3) like all other currency_code columns (ISO 4217)
ALTER TABLE split_currency_exchanges
    ALTER COLUMN sce_currency_code TYPE VARCHAR(3);

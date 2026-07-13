ALTER TABLE assistance_items ADD COLUMN IF NOT EXISTS transfer_account_number character varying(34);
ALTER TABLE assistance_items ADD COLUMN IF NOT EXISTS transfer_bank_number character varying(10);
ALTER TABLE assistance_items ADD COLUMN IF NOT EXISTS transfer_branch_number character varying(10);
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260629000000_AddAssistanceItemTransferBank', '8.0.11')
ON CONFLICT DO NOTHING;

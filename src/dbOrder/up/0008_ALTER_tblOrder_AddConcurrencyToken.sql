-- Changes on every status change of the order; the optimistic concurrency token, so two requests can never both
-- move the same order on (for example, both retry its payment after a provider error).
ALTER TABLE tblOrder ADD [ConcurrencyToken] UNIQUEIDENTIFIER NOT NULL
	CONSTRAINT DF_Order_ConcurrencyToken DEFAULT NEWID()

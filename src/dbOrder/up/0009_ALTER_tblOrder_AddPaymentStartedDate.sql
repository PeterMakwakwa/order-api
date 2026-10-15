-- When the current payment attempt started (the order's creation, or its latest retry). An order still PendingPayment long
-- after this was abandoned mid-charge, and the reconciliation job charges it again with the same provider idempotency key.
ALTER TABLE tblOrder ADD [PaymentStartedDate] DATETIMEOFFSET NULL
GO

UPDATE tblOrder SET [PaymentStartedDate] = [CreatedDate] WHERE [PaymentStartedDate] IS NULL
GO

ALTER TABLE tblOrder ALTER COLUMN [PaymentStartedDate] DATETIMEOFFSET NOT NULL
GO

-- Reconciliation: the orders still PendingPayment, oldest attempt first. Deliberately not a filtered index: those make every
-- insert and update of tblOrder fail on sessions without QUOTED_IDENTIFIER ON (such as sqlcmd's default).
CREATE NONCLUSTERED INDEX IX_Order_Status_PaymentStartedDate ON tblOrder ([Status], [PaymentStartedDate])

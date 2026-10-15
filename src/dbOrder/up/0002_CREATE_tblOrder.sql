CREATE TABLE tblOrder
(
	[Id] BIGINT IDENTITY(1, 1) NOT NULL,
	[UniqueId] UNIQUEIDENTIFIER NOT NULL,
	[CorrelationId] VARCHAR(100) NOT NULL,
	[IdempotencyKey] VARCHAR(100) NOT NULL,
	[RequestHash] CHAR(64) NOT NULL,
	[CustomerId] INT NOT NULL,
	[CustomerEmail] NVARCHAR(320) NOT NULL,
	[PaymentMethod] VARCHAR(32) NOT NULL,
	[Status] VARCHAR(32) NOT NULL,
	[PaymentOutcome] VARCHAR(32) NULL,
	[PromotionCode] VARCHAR(50) NULL,
	[DiscountRate] DECIMAL(5, 4) NOT NULL,
	[Subtotal] DECIMAL(18, 2) NOT NULL,
	[Total] DECIMAL(18, 2) NOT NULL,
	[PaymentFailureReason] NVARCHAR(500) NULL,
	[CreatedDate] DATETIMEOFFSET NOT NULL,

	CONSTRAINT PK_Order PRIMARY KEY (Id),
	CONSTRAINT UC_Order_UniqueId UNIQUE (UniqueId),
	-- One order per customer and Idempotency-Key: a retried request finds the original instead of charging twice.
	CONSTRAINT UC_Order_CustomerId_IdempotencyKey UNIQUE (CustomerId, IdempotencyKey),
	CONSTRAINT CK_Order_DiscountRate CHECK (DiscountRate >= 0 AND DiscountRate < 1),
	CONSTRAINT CK_Order_Discount_RequiresPromotion CHECK (DiscountRate = 0 OR PromotionCode IS NOT NULL),

	-- Order history: a customer's orders, newest first.
	INDEX IX_Order_CustomerId_CreatedDate NONCLUSTERED (CustomerId, CreatedDate)
)

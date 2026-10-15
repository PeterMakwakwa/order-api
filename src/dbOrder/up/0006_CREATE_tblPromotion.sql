CREATE TABLE tblPromotion
(
	[Id] INT IDENTITY(1, 1) NOT NULL,
	-- Stored upper-case; the API normalises codes before looking them up.
	[Code] VARCHAR(50) NOT NULL,
	[DiscountRate] DECIMAL(5, 4) NOT NULL,
	[IsActive] BIT NOT NULL,
	[ValidFrom] DATETIMEOFFSET NOT NULL,
	-- Exclusive end; NULL for no end date.
	[ValidTo] DATETIMEOFFSET NULL,

	CONSTRAINT PK_Promotion PRIMARY KEY (Id),
	CONSTRAINT UC_Promotion_Code UNIQUE (Code),
	CONSTRAINT CK_Promotion_DiscountRate CHECK (DiscountRate > 0 AND DiscountRate < 1),
	CONSTRAINT CK_Promotion_Validity CHECK (ValidTo IS NULL OR ValidTo > ValidFrom)
)

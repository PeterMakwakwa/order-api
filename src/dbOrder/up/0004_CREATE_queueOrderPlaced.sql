CREATE TABLE queueOrderPlaced
(
	[Id] BIGINT IDENTITY(1, 1) NOT NULL,
	[Version] INT NOT NULL,
	[Message] NVARCHAR(MAX) NOT NULL,
	[CreatedDate] DATETIMEOFFSET NOT NULL,
	[ProcessedDate] DATETIMEOFFSET NULL,
	[Attempts] INT NOT NULL,
	[LastError] NVARCHAR(2000) NULL,
	-- Claim: a processor reserves the message until LockedUntil; LockToken changes on every claim and is the
	-- optimistic concurrency token, so two instances claiming at once cannot both succeed.
	[LockedUntil] DATETIMEOFFSET NULL,
	[LockToken] UNIQUEIDENTIFIER NULL,

	CONSTRAINT PK_OrderPlaced PRIMARY KEY (Id),

	INDEX IX_OrderPlaced_ProcessedDate_CreatedDate NONCLUSTERED (ProcessedDate, CreatedDate)
)

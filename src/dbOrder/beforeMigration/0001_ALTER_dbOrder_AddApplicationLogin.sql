-- Runs on every deployment (grate "beforeMigration" folder), so it also rotates the password.
-- The login name and password are grate user tokens (--ut=AppLogin=... --ut=AppLoginPassword=...),
-- supplied from the environment at deploy time; no credentials are stored in this repository.
-- The application gets read/write on its own database only (no DDL, no server roles).

IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'{{AppLogin}}')
BEGIN
	EXEC (N'CREATE LOGIN [{{AppLogin}}] WITH PASSWORD = N''{{AppLoginPassword}}'', CHECK_POLICY = ON, DEFAULT_DATABASE = [{{DatabaseName}}]')
END
ELSE
BEGIN
	EXEC (N'ALTER LOGIN [{{AppLogin}}] WITH PASSWORD = N''{{AppLoginPassword}}''')
END

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'{{AppLogin}}')
BEGIN
	EXEC (N'CREATE USER [{{AppLogin}}] FOR LOGIN [{{AppLogin}}]')
END

EXEC (N'ALTER ROLE db_datareader ADD MEMBER [{{AppLogin}}]')
EXEC (N'ALTER ROLE db_datawriter ADD MEMBER [{{AppLogin}}]')

IF NOT EXISTS (SELECT * FROM SYS.EXTENDED_PROPERTIES WHERE name = 'Contact Information')
BEGIN
	EXEC sys.sp_addextendedproperty @name = N'Contact Information', @value = N'Order Processing'
END
ELSE
BEGIN
	EXEC sys.sp_updateextendedproperty @name = N'Contact Information', @value = N'Order Processing'
END

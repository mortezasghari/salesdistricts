-- Runs against master. CREATE DATABASE cannot take a parameter, so the name is quoted with QUOTENAME.
IF DB_ID(@name) IS NULL
BEGIN
    DECLARE @sql nvarchar(300) = N'CREATE DATABASE ' + QUOTENAME(@name);
    EXEC (@sql);
END

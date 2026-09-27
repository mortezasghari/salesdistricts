-- Migration journal. Idempotent: the migrator runs this on every connection before reading the journal.
IF OBJECT_ID('dbo.SchemaVersion') IS NULL
CREATE TABLE dbo.SchemaVersion
(
    Version      bigint        NOT NULL PRIMARY KEY,
    Name         nvarchar(200) NOT NULL,
    AppliedAtUtc datetime2(0)  NOT NULL DEFAULT SYSUTCDATETIME()
);

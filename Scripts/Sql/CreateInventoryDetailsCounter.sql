/*
    Creates the aggregate counter for valid Inventory/Details page visits.

    Run this script on the production GTX database before deploying the
    application code. No stored procedure is required; InventoryService uses
    LINQ-to-SQL to insert and update this table.

    This script is safe to run more than once.
*/

SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'dbo.InventoryDetailsCounter', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.InventoryDetailsCounter
    (
        Id BIGINT IDENTITY(1,1) NOT NULL,
        Stock NVARCHAR(10) NOT NULL,
        [Counter] BIGINT NOT NULL CONSTRAINT DF_InventoryDetailsCounter_Counter DEFAULT (0),
        DateUpdated DATETIME2(0) NOT NULL CONSTRAINT DF_InventoryDetailsCounter_DateUpdated DEFAULT (SYSUTCDATETIME()),

        CONSTRAINT PK_InventoryDetailsCounter PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_InventoryDetailsCounter_Stock UNIQUE (Stock),
        CONSTRAINT CK_InventoryDetailsCounter_Counter CHECK ([Counter] >= 0)
    );
END
GO

-- Verification query:
-- SELECT Id, Stock, [Counter], DateUpdated
-- FROM dbo.InventoryDetailsCounter
-- ORDER BY [Counter] DESC, DateUpdated DESC;

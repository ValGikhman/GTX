/*
    Creates inventory import audit tables for GTX inventory loads.

    Expected InventoryVehicles.Status enum values:
      0 = Skip
      1 = Add
      2 = Remove
      3 = Update
*/

SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'dbo.InventoryVehicles', N'U') IS NOT NULL
    AND OBJECT_ID(N'dbo.InventoryLog', N'U') IS NULL
BEGIN
    THROW 51000, 'dbo.InventoryVehicles exists but dbo.InventoryLog does not. Review the schema before running this script.', 1;
END
GO

IF OBJECT_ID(N'dbo.InventoryLog', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.InventoryLog
    (
        Id BIGINT IDENTITY(1,1) NOT NULL,
        DateCreated DATETIME2(0) NOT NULL CONSTRAINT DF_InventoryLog_DateCreated DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_InventoryLog PRIMARY KEY CLUSTERED (Id)
    );
END
GO

IF OBJECT_ID(N'dbo.InventoryVehicles', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.InventoryVehicles
    (
        Id BIGINT IDENTITY(1,1) NOT NULL,
        InventoryLogId BIGINT NOT NULL,
        [Status] INT NOT NULL,

        Stock NVARCHAR(50) NULL,
        [Year] INT NULL,
        Make NVARCHAR(100) NULL,
        Model NVARCHAR(100) NULL,
        VIN NVARCHAR(32) NULL,
        Mileage INT NULL,
        Cylinders INT NULL,
        [Weight] INT NULL,
        Color NVARCHAR(100) NULL,
        Color2 NVARCHAR(100) NULL,
        Features NVARCHAR(MAX) NULL,
        RetailPrice INT NULL,
        WholesalePrice INT NULL,
        InternetPrice INT NULL,
        OtherPrice INT NULL,
        VehicleStyle NVARCHAR(100) NULL,
        DriveTrain NVARCHAR(100) NULL,
        LocationCode NVARCHAR(50) NULL,
        Body NVARCHAR(100) NULL,
        Engine NVARCHAR(100) NULL,
        Transmission NVARCHAR(50) NULL,
        PurchaseDate NVARCHAR(25) NULL,
        ArrivalDate NVARCHAR(25) NULL,
        SetToUpload NVARCHAR(10) NULL,
        VehicleType NVARCHAR(100) NULL,
        FuelType NVARCHAR(100) NULL,
        ReadyToSellDate NVARCHAR(25) NULL,
        TransmissionSpeed INT NULL,

        DateCreated DATETIME2(0) NOT NULL CONSTRAINT DF_InventoryVehicles_DateCreated DEFAULT (SYSUTCDATETIME()),

        CONSTRAINT PK_InventoryVehicles PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_InventoryVehicles_InventoryLog FOREIGN KEY (InventoryLogId) REFERENCES dbo.InventoryLog (Id) ON DELETE CASCADE,
        CONSTRAINT CK_InventoryVehicles_Status CHECK ([Status] IN (0, 1, 2, 3))
    );
END
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE [name] = N'IX_InventoryLog_DateCreated'
      AND [object_id] = OBJECT_ID(N'dbo.InventoryLog', N'U')
)
BEGIN
    CREATE INDEX IX_InventoryLog_DateCreated
        ON dbo.InventoryLog (DateCreated DESC, Id DESC);
END
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE [name] = N'IX_InventoryVehicles_InventoryLogId_Status'
      AND [object_id] = OBJECT_ID(N'dbo.InventoryVehicles', N'U')
)
BEGIN
    CREATE INDEX IX_InventoryVehicles_InventoryLogId_Status
        ON dbo.InventoryVehicles (InventoryLogId, [Status]);
END
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE [name] = N'IX_InventoryVehicles_Stock'
      AND [object_id] = OBJECT_ID(N'dbo.InventoryVehicles', N'U')
)
BEGIN
    CREATE INDEX IX_InventoryVehicles_Stock
        ON dbo.InventoryVehicles (Stock, InventoryLogId)
        INCLUDE ([Status], VIN);
END
GO

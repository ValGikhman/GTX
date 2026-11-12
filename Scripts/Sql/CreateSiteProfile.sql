-- Run against the intended GTX database BEFORE deploying the Profile code.
-- Rerunnable: existing profile values and role permissions are never overwritten.
-- This is one company-wide profile, not a separate profile per role.
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.SiteProfile', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SiteProfile (
        Id tinyint NOT NULL CONSTRAINT PK_SiteProfile PRIMARY KEY,
        CompanyName nvarchar(120) NOT NULL,
        Email nvarchar(254) NOT NULL,
        Phone nvarchar(30) NOT NULL,
        StreetAddress nvarchar(160) NOT NULL,
        AddressLine2 nvarchar(120) NULL,
        City nvarchar(80) NOT NULL,
        State nvarchar(2) NOT NULL,
        ZipCode varchar(5) NOT NULL,
        ResearchRadiusMiles int NOT NULL,
        DocumentaryFee int NOT NULL CONSTRAINT DF_SiteProfile_DocumentaryFee DEFAULT (398),
        Latitude decimal(18,12) NULL,
        Longitude decimal(18,12) NULL,
        UpdatedUtc datetime2(0) NOT NULL CONSTRAINT DF_SiteProfile_UpdatedUtc DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT CK_SiteProfile_Singleton CHECK (Id=1),
        CONSTRAINT CK_SiteProfile_Zip CHECK (LEN(ZipCode)=5 AND ZipCode NOT LIKE '%[^0-9]%'),
        CONSTRAINT CK_SiteProfile_Radius CHECK (ResearchRadiusMiles BETWEEN 1 AND 500),
        CONSTRAINT CK_SiteProfile_Coordinates CHECK (
            (Latitude IS NULL AND Longitude IS NULL) OR
            (Latitude IS NOT NULL AND Longitude IS NOT NULL AND Latitude BETWEEN -90 AND 90 AND Longitude BETWEEN -180 AND 180))
    );
END;

-- Upgrade an earlier Profile installation without replacing any edited values.
IF COL_LENGTH(N'dbo.SiteProfile', N'DocumentaryFee') IS NULL
BEGIN
    ALTER TABLE dbo.SiteProfile ADD DocumentaryFee int NOT NULL
        CONSTRAINT DF_SiteProfile_DocumentaryFee DEFAULT (398) WITH VALUES;
END;
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.SiteProfile') AND name=N'CK_SiteProfile_DocumentaryFee')
    EXEC(N'ALTER TABLE dbo.SiteProfile ADD CONSTRAINT CK_SiteProfile_DocumentaryFee CHECK (DocumentaryFee BETWEEN 0 AND 10000)');

IF NOT EXISTS (SELECT 1 FROM dbo.SiteProfile WITH (UPDLOCK, HOLDLOCK) WHERE Id=1)
BEGIN
    INSERT dbo.SiteProfile (Id, CompanyName, Email, Phone, StreetAddress, AddressLine2, City, State, ZipCode,
        ResearchRadiusMiles, Latitude, Longitude)
    VALUES (1, N'GTX Autogroup', N'gtxagroup@gmail.com', N'(513) 489-2886', N'9516 Princeton Glendale Rd', NULL,
        N'West Chester', N'OH', '45011', 100, 39.320130426346, -84.464008498772);
END;

IF OBJECT_ID(N'dbo.SiteProfileBusinessHours', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SiteProfileBusinessHours (
        ProfileId tinyint NOT NULL,
        DayOfWeek tinyint NOT NULL,
        IsClosed bit NOT NULL,
        OpenTime time(0) NULL,
        CloseTime time(0) NULL,
        CONSTRAINT PK_SiteProfileBusinessHours PRIMARY KEY (ProfileId, DayOfWeek),
        CONSTRAINT FK_SiteProfileBusinessHours_Profile FOREIGN KEY (ProfileId) REFERENCES dbo.SiteProfile(Id),
        CONSTRAINT CK_SiteProfileBusinessHours_Day CHECK (DayOfWeek BETWEEN 0 AND 6),
        CONSTRAINT CK_SiteProfileBusinessHours_Times CHECK (
            (IsClosed=1 AND OpenTime IS NULL AND CloseTime IS NULL) OR
            (IsClosed=0 AND OpenTime IS NOT NULL AND CloseTime IS NOT NULL AND CloseTime>OpenTime))
    );
END;

-- Existing business hours from GTX-Configuration.xml, inserted only for missing days.
INSERT dbo.SiteProfileBusinessHours (ProfileId, DayOfWeek, IsClosed, OpenTime, CloseTime)
SELECT 1, seed.DayOfWeek, 0, seed.OpenTime, seed.CloseTime
FROM (VALUES
    (0, CAST('11:00' AS time), CAST('16:00' AS time)),
    (1, CAST('09:00' AS time), CAST('19:00' AS time)),
    (2, CAST('09:00' AS time), CAST('19:00' AS time)),
    (3, CAST('09:00' AS time), CAST('19:00' AS time)),
    (4, CAST('09:00' AS time), CAST('19:00' AS time)),
    (5, CAST('09:00' AS time), CAST('19:00' AS time)),
    (6, CAST('09:00' AS time), CAST('18:00' AS time))
) seed(DayOfWeek, OpenTime, CloseTime)
WHERE NOT EXISTS (SELECT 1 FROM dbo.SiteProfileBusinessHours WITH (UPDLOCK, HOLDLOCK)
    WHERE ProfileId=1 AND DayOfWeek=seed.DayOfWeek);

-- Profile access is reserved for the built-in administrator by application authorization.
COMMIT TRANSACTION;

-- Run once against the intended GTX database before enabling Specials.
-- Safe to rerun; existing Specials and Blogs are preserved.
IF OBJECT_ID(N'dbo.Specials', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Specials
    (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Specials PRIMARY KEY,
        Title nvarchar(200) NOT NULL,
        CardContent nvarchar(max) NOT NULL,
        IsPublished bit NOT NULL CONSTRAINT DF_Specials_IsPublished DEFAULT (0),
        CreatedAt datetime2(0) NOT NULL CONSTRAINT DF_Specials_CreatedAt DEFAULT (SYSUTCDATETIME())
    );
    CREATE INDEX IX_Specials_Published_Created ON dbo.Specials(IsPublished, CreatedAt DESC, Id DESC);
END;

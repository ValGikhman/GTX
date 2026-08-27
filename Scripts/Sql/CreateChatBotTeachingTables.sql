SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.ChatBotNavigationLesson', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ChatBotNavigationLesson
    (
        Id INT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_ChatBotNavigationLesson PRIMARY KEY,
        Phrase NVARCHAR(300) NOT NULL,
        NormalizedPhrase NVARCHAR(300) NOT NULL,
        ActionKey NVARCHAR(80) NOT NULL,
        IsActive BIT NOT NULL
            CONSTRAINT DF_ChatBotNavigationLesson_IsActive DEFAULT (1),
        CreatedByRole NVARCHAR(40) NOT NULL,
        CreatedUtc DATETIME2(0) NOT NULL
            CONSTRAINT DF_ChatBotNavigationLesson_CreatedUtc DEFAULT (SYSUTCDATETIME()),
        UpdatedUtc DATETIME2(0) NOT NULL
            CONSTRAINT DF_ChatBotNavigationLesson_UpdatedUtc DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT UQ_ChatBotNavigationLesson_NormalizedPhrase UNIQUE (NormalizedPhrase),
        CONSTRAINT CK_ChatBotNavigationLesson_Phrase_NotBlank CHECK (LEN(LTRIM(RTRIM(Phrase))) > 0),
        CONSTRAINT CK_ChatBotNavigationLesson_ActionKey_NotBlank CHECK (LEN(LTRIM(RTRIM(ActionKey))) > 0)
    );
END;

IF OBJECT_ID(N'dbo.ChatBotNavigationLessonAudit', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ChatBotNavigationLessonAudit
    (
        Id BIGINT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_ChatBotNavigationLessonAudit PRIMARY KEY,
        LessonId INT NOT NULL,
        Operation NVARCHAR(30) NOT NULL,
        ActorRole NVARCHAR(40) NOT NULL,
        PreviousPhrase NVARCHAR(300) NULL,
        PreviousActionKey NVARCHAR(80) NULL,
        NewPhrase NVARCHAR(300) NULL,
        NewActionKey NVARCHAR(80) NULL,
        ChangedUtc DATETIME2(0) NOT NULL
            CONSTRAINT DF_ChatBotNavigationLessonAudit_ChangedUtc DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_ChatBotNavigationLessonAudit_Lesson
            FOREIGN KEY (LessonId) REFERENCES dbo.ChatBotNavigationLesson(Id)
    );

    CREATE INDEX IX_ChatBotNavigationLessonAudit_LessonId_ChangedUtc
        ON dbo.ChatBotNavigationLessonAudit (LessonId, ChangedUtc DESC);
END;

DECLARE @DefaultLessons TABLE
(
    Phrase NVARCHAR(300) NOT NULL,
    NormalizedPhrase NVARCHAR(300) NOT NULL,
    ActionKey NVARCHAR(80) NOT NULL
);

INSERT @DefaultLessons (Phrase, NormalizedPhrase, ActionKey)
VALUES
    (N'dashboard',               N'dashboard',               N'inventory_dashboard'),
    (N'inventory dashboard',     N'inventory dashboard',     N'inventory_dashboard'),
    (N'inventory management',    N'inventory management',    N'inventory_management'),
    (N'Majordome',               N'majordome',               N'majordome_inventory'),
    (N'Majordome inventory',     N'majordome inventory',     N'majordome_inventory'),
    (N'vehicle management',      N'vehicle management',      N'majordome_inventory'),
    (N'employee management',     N'employee management',     N'employee_management'),
    (N'VIN decoder',             N'vin decoder',             N'vin_decoder'),
    (N'announcements',           N'announcements',           N'announcements'),
    (N'announcement management', N'announcement management', N'announcements'),
    (N'blog management',         N'blog management',         N'blog_management'),
    (N'system health',           N'system health',           N'health');

DECLARE @InsertedDefaults TABLE
(
    Id INT NOT NULL,
    Phrase NVARCHAR(300) NOT NULL,
    ActionKey NVARCHAR(80) NOT NULL
);

INSERT dbo.ChatBotNavigationLesson
    (Phrase, NormalizedPhrase, ActionKey, IsActive, CreatedByRole, CreatedUtc, UpdatedUtc)
OUTPUT INSERTED.Id, INSERTED.Phrase, INSERTED.ActionKey
    INTO @InsertedDefaults (Id, Phrase, ActionKey)
SELECT defaults.Phrase,
       defaults.NormalizedPhrase,
       defaults.ActionKey,
       1,
       N'System',
       SYSUTCDATETIME(),
       SYSUTCDATETIME()
FROM @DefaultLessons defaults
WHERE NOT EXISTS
(
    SELECT 1
    FROM dbo.ChatBotNavigationLesson existing
    WHERE existing.NormalizedPhrase = defaults.NormalizedPhrase
);

INSERT dbo.ChatBotNavigationLessonAudit
    (LessonId, Operation, ActorRole, PreviousPhrase, PreviousActionKey,
     NewPhrase, NewActionKey, ChangedUtc)
SELECT inserted.Id,
       N'Seeded',
       N'System',
       NULL,
       NULL,
       inserted.Phrase,
       inserted.ActionKey,
       SYSUTCDATETIME()
FROM @InsertedDefaults inserted;

COMMIT TRANSACTION;

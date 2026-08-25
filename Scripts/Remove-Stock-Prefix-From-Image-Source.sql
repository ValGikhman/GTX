/*
    Removes the redundant leading Stock/ segment from dbo.Images.Source.

    Example:
        Stock  = GTC001834
        Before = GTC001834/IMG_9556.jpg
        After  = IMG_9556.jpg

    Safety:
      1. The script previews and rolls back by default.
      2. Review the summary and sample rows.
      3. Set @CommitChanges = 1 and run the complete script again to commit.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @CommitChanges bit = 0;

IF OBJECT_ID('tempdb..#ImageSourceMigration') IS NOT NULL
    DROP TABLE #ImageSourceMigration;

CREATE TABLE #ImageSourceMigration
(
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    Stock varchar(9) NOT NULL,
    OldSource varchar(850) NOT NULL,
    NewSource varchar(850) NOT NULL
);

INSERT INTO #ImageSourceMigration (Id, Stock, OldSource, NewSource)
SELECT
    image.Id,
    image.Stock,
    image.Source,
    SUBSTRING(normalized.Source, LEN(LTRIM(RTRIM(image.Stock))) + 2, 850)
FROM dbo.Images AS image
CROSS APPLY
(
    VALUES (REPLACE(LTRIM(RTRIM(image.Source)), '\', '/'))
) AS normalized(Source)
WHERE normalized.Source LIKE LTRIM(RTRIM(image.Stock)) + '/%';

DECLARE @TotalRows int = (SELECT COUNT(*) FROM dbo.Images);
DECLARE @RowsToUpdate int = (SELECT COUNT(*) FROM #ImageSourceMigration);
DECLARE @RowsNotMatching int = @TotalRows - @RowsToUpdate;

SELECT
    @TotalRows AS TotalImageRows,
    @RowsToUpdate AS RowsToUpdate,
    @RowsNotMatching AS RowsNotStartingWithStock;

SELECT TOP (100)
    Stock,
    OldSource,
    NewSource
FROM #ImageSourceMigration
ORDER BY Stock, OldSource;

IF EXISTS
(
    SELECT 1
    FROM #ImageSourceMigration
    WHERE NULLIF(LTRIM(RTRIM(NewSource)), '') IS NULL
)
BEGIN
    THROW 50001, 'Migration stopped because at least one resulting filename is empty.', 1;
END;

/*
    Check the final state, including any rows that may already contain only a
    filename. Stock + Source must remain unique after the transformation.
*/
IF EXISTS
(
    SELECT 1
    FROM dbo.Images AS image
    LEFT JOIN #ImageSourceMigration AS migration ON migration.Id = image.Id
    GROUP BY
        image.Stock,
        COALESCE(migration.NewSource, image.Source)
    HAVING COUNT(*) > 1
)
BEGIN
    SELECT
        image.Stock,
        COALESCE(migration.NewSource, image.Source) AS ResultingSource,
        COUNT(*) AS DuplicateCount
    FROM dbo.Images AS image
    LEFT JOIN #ImageSourceMigration AS migration ON migration.Id = image.Id
    GROUP BY
        image.Stock,
        COALESCE(migration.NewSource, image.Source)
    HAVING COUNT(*) > 1
    ORDER BY image.Stock, ResultingSource;

    THROW 50002, 'Migration stopped because duplicate filenames would be created for a stock number.', 1;
END;

BEGIN TRANSACTION;

UPDATE image
SET image.Source = migration.NewSource
FROM dbo.Images AS image
INNER JOIN #ImageSourceMigration AS migration ON migration.Id = image.Id;

DECLARE @UpdatedRows int = @@ROWCOUNT;

IF @UpdatedRows <> @RowsToUpdate
BEGIN
    ROLLBACK TRANSACTION;
    THROW 50003, 'Migration stopped because the number of updated rows did not match the preview.', 1;
END;

IF @CommitChanges = 1
BEGIN
    COMMIT TRANSACTION;

    SELECT
        'COMMITTED' AS MigrationStatus,
        @UpdatedRows AS UpdatedRows;
END;
ELSE
BEGIN
    ROLLBACK TRANSACTION;

    SELECT
        'PREVIEW ONLY - ROLLED BACK' AS MigrationStatus,
        @UpdatedRows AS RowsThatWouldBeUpdated;
END;


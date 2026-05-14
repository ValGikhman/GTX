/*
    Imports stock image files into dbo.Images.

    Assumes folder shape:
      <RootPath>\<Stock>\<ImageFile>

    Example for your current folder:
      EXEC dbo.usp_ImportImagesFromStockFolders
          @RootPath = N'C:\Users\valen\source\repos\GTXAutoGroup\GTX\GTXImages\Inventory\GTX',
          @SourcePrefix = 'GTX',
          @DeleteExistingForStocks = 1,
          @DryRun = 0;

    Notes:
    - SQL Server service account must have read access to @RootPath.
    - Source will be stored as: <SourcePrefix>/<Stock>/<FileName> (or <Stock>/<FileName> when prefix is empty).
*/

IF OBJECT_ID(N'dbo.usp_ImportImagesFromStockFolders', N'P') IS NULL
BEGIN
    EXEC(N'CREATE PROCEDURE dbo.usp_ImportImagesFromStockFolders AS BEGIN SET NOCOUNT ON; END');
END
GO

ALTER PROCEDURE dbo.usp_ImportImagesFromStockFolders
    @RootPath NVARCHAR(4000),
    @SourcePrefix VARCHAR(200) = '',
    @DeleteExistingForStocks BIT = 0,
    @DryRun BIT = 1
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NULLIF(LTRIM(RTRIM(@RootPath)), N'') IS NULL
    BEGIN
        THROW 50001, 'Parameter @RootPath is required.', 1;
    END;

    DECLARE @Prefix VARCHAR(200) = REPLACE(LTRIM(RTRIM(ISNULL(@SourcePrefix, ''))), '\', '/');
    IF @Prefix <> '' AND RIGHT(@Prefix, 1) <> '/'
    BEGIN
        SET @Prefix = @Prefix + '/';
    END;

    DECLARE @Tree TABLE
    (
        RowId INT IDENTITY(1,1) PRIMARY KEY,
        Subdirectory NVARCHAR(512) NOT NULL,
        [Depth] INT NOT NULL,
        [IsFile] BIT NOT NULL
    );

    BEGIN TRY
        INSERT INTO @Tree (Subdirectory, [Depth], [IsFile])
        EXEC master.sys.xp_dirtree @RootPath, 2, 1;
    END TRY
    BEGIN CATCH
        DECLARE @ErrMsg NVARCHAR(2048) = N'xp_dirtree failed for path [' + @RootPath
            + N']. Ensure SQL Server service account can read this folder. '
            + ERROR_MESSAGE();
        THROW 50002, @ErrMsg, 1;
    END CATCH;

    IF OBJECT_ID('tempdb..#ImportStage') IS NOT NULL
    BEGIN
        DROP TABLE #ImportStage;
    END;

    ;WITH TreeGrouped AS
    (
        SELECT
            t.RowId,
            t.Subdirectory,
            t.[Depth],
            t.[IsFile],
            StockGroup = SUM(CASE WHEN t.[Depth] = 1 AND t.[IsFile] = 0 THEN 1 ELSE 0 END)
                OVER (ORDER BY t.RowId ROWS UNBOUNDED PRECEDING)
        FROM @Tree t
    ),
    Stocks AS
    (
        SELECT
            tg.StockGroup,
            StockRaw = MAX(tg.Subdirectory)
        FROM TreeGrouped tg
        WHERE tg.[Depth] = 1
          AND tg.[IsFile] = 0
        GROUP BY tg.StockGroup
    ),
    Mapped AS
    (
        SELECT
            s.StockRaw,
            FileNameRaw = tg.Subdirectory
        FROM TreeGrouped tg
        JOIN Stocks s
            ON s.StockGroup = tg.StockGroup
        WHERE tg.[Depth] = 2
          AND tg.[IsFile] = 1
    ),
    Cleaned AS
    (
        SELECT
            Stock = UPPER(LTRIM(RTRIM(ISNULL(StockRaw, '')))),
            FileName = LTRIM(RTRIM(ISNULL(FileNameRaw, '')))
        FROM Mapped
    ),
    Filtered AS
    (
        SELECT DISTINCT
            c.Stock,
            c.FileName,
            Source = @Prefix + c.Stock + '/' + c.FileName,
            ParsedOrder = TRY_CONVERT(INT, NULLIF(LEFT(c.FileName, PATINDEX('%[^0-9]%', c.FileName + 'X') - 1), ''))
        FROM Cleaned c
        WHERE c.Stock <> ''
          AND LEN(c.Stock) <= 9
          AND c.FileName <> ''
          AND
          (
                LOWER(c.FileName) LIKE '%.jpg'
             OR LOWER(c.FileName) LIKE '%.jpeg'
             OR LOWER(c.FileName) LIKE '%.png'
             OR LOWER(c.FileName) LIKE '%.gif'
             OR LOWER(c.FileName) LIKE '%.webp'
          )
    )
    SELECT
        Stock,
        Source,
        [Order] = COALESCE(ParsedOrder, ROW_NUMBER() OVER (PARTITION BY Stock ORDER BY FileName)),
        DateCreated = GETDATE()
    INTO #ImportStage
    FROM Filtered;

    DECLARE @Stocks INT = (SELECT COUNT(DISTINCT Stock) FROM #ImportStage);
    DECLARE @Candidates INT = (SELECT COUNT(*) FROM #ImportStage);

    IF @DryRun = 1
    BEGIN
        SELECT
            [Mode] = 'DRY-RUN',
            StocksFound = @Stocks,
            CandidateRows = @Candidates;

        SELECT TOP (200)
            s.Stock,
            s.Source,
            s.[Order]
        FROM #ImportStage s
        ORDER BY s.Stock, s.[Order], s.Source;

        RETURN;
    END;

    BEGIN TRAN;

    IF @DeleteExistingForStocks = 1
    BEGIN
        DELETE i
        FROM dbo.Images i
        WHERE EXISTS
        (
            SELECT 1
            FROM #ImportStage s
            WHERE s.Stock = i.Stock
        );
    END;

    INSERT INTO dbo.Images (Stock, Source, [Order], DateCreated)
    SELECT
        s.Stock,
        s.Source,
        s.[Order],
        s.DateCreated
    FROM #ImportStage s
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.Images i
        WHERE i.Stock = s.Stock
          AND i.Source = s.Source
    );

    DECLARE @Inserted INT = @@ROWCOUNT;

    COMMIT TRAN;

    SELECT
        [Mode] = 'EXECUTED',
        StocksFound = @Stocks,
        CandidateRows = @Candidates,
        RowsInserted = @Inserted,
        DeleteExistingForStocks = @DeleteExistingForStocks,
        RootPath = @RootPath,
        SourcePrefix = @Prefix;
END;
GO

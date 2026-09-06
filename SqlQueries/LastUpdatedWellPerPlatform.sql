/*
    PART 2 — Return the last updated well for each platform.

    One row per platform: the well with the most recent UpdatedAt.

    ROW_NUMBER() numbers each platform's wells newest-first, and keeping only
    number 1 leaves exactly one row per platform. Id is the tie-breaker, so two
    wells sharing an UpdatedAt still produce a single, stable row rather than an
    arbitrary one.

    A platform with no wells does not appear. Swapping the INNER JOIN for a LEFT
    JOIN would list it with NULL well columns instead.
*/

WITH RankedWells AS
(
    SELECT
        w.Id,
        w.PlatformId,
        w.UniqueName,
        w.Latitude,
        w.Longitude,
        w.CreatedAt,
        w.UpdatedAt,
        ROW_NUMBER() OVER (
            PARTITION BY w.PlatformId
            ORDER BY w.UpdatedAt DESC, w.Id DESC
        ) AS RowNo
    FROM dbo.Wells AS w
)
SELECT
    p.UniqueName AS PlatformName,
    rw.Id,
    rw.PlatformId,
    rw.UniqueName,
    rw.Latitude,
    rw.Longitude,
    rw.CreatedAt,
    rw.UpdatedAt
FROM dbo.Platforms AS p
INNER JOIN RankedWells AS rw
        ON rw.PlatformId = p.Id
       AND rw.RowNo = 1
ORDER BY p.Id;


/* ---------------------------------------------------------------------------
   Alternative: CROSS APPLY. Same result, and on a large Wells table it can be
   the faster plan, since it seeks the top row per platform instead of ranking
   every row first. Kept here as a note rather than as the answer.

SELECT
    p.UniqueName AS PlatformName,
    w.Id,
    w.PlatformId,
    w.UniqueName,
    w.Latitude,
    w.Longitude,
    w.CreatedAt,
    w.UpdatedAt
FROM dbo.Platforms AS p
CROSS APPLY (
    SELECT TOP (1) *
    FROM dbo.Wells AS w
    WHERE w.PlatformId = p.Id
    ORDER BY w.UpdatedAt DESC, w.Id DESC
) AS w
ORDER BY p.Id;
--------------------------------------------------------------------------- */

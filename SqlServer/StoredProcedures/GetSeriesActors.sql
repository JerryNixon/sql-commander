CREATE PROCEDURE[dbo].[GetSeriesActors]
    @seriesId INT = 1,
    @top INT = 5
AS
SELECT TOP(@top) 
    Id,
	Name,
	BirthYear,
	SeriesId,
	Series
FROM SeriesActors
WHERE SeriesId = @seriesId;
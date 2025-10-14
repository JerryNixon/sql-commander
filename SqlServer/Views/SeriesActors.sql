CREATE VIEW [dbo].[SeriesActors]
AS
SELECT
	a.Id AS Id,
	a.Name AS Name,
	a.BirthYear AS BirthYear,
	s.Id AS SeriesId,
	s.Name AS Series
FROM Series s
JOIN Series_Character AS sc ON s.Id = sc.SeriesId
JOIN Character AS c ON sc.CharacterId = c.Id
JOIN Actor AS a ON c.ActorId = a.Id;
GO



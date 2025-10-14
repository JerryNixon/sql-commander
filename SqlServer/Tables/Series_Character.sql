CREATE TABLE dbo.Series_Character
(
    SeriesId INT NOT NULL,
    CharacterId INT NOT NULL,
    Role VARCHAR(500) NULL,
    CONSTRAINT PK_Series_Character PRIMARY KEY (SeriesId, CharacterId),
    CONSTRAINT FK_Series_Character_Series FOREIGN KEY (SeriesId) REFERENCES dbo.Series(Id),
    CONSTRAINT FK_Series_Character_Character FOREIGN KEY (CharacterId) REFERENCES dbo.[Character](Id)
);

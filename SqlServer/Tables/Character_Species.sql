CREATE TABLE dbo.Character_Species
(
    CharacterId INT NOT NULL,
    SpeciesId INT NOT NULL,
    CONSTRAINT PK_Character_Species PRIMARY KEY (CharacterId, SpeciesId),
    CONSTRAINT FK_Character_Species_Character FOREIGN KEY (CharacterId) REFERENCES dbo.[Character](Id),
    CONSTRAINT FK_Character_Species_Species FOREIGN KEY (SpeciesId) REFERENCES dbo.Species(Id)
);

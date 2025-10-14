CREATE TABLE dbo.[Character]
(
    Id INT NOT NULL PRIMARY KEY,
    Name NVARCHAR(255) NOT NULL,
    ActorId INT NOT NULL,
    Stardate DECIMAL(10,2) NULL,
    CONSTRAINT FK_Character_Actor FOREIGN KEY (ActorId) REFERENCES dbo.Actor(Id)
);

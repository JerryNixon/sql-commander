CREATE TABLE dbo.TodoList
(
    Id INT NOT NULL PRIMARY KEY,
    CategoryId INT NOT NULL,
    Name NVARCHAR(150) NOT NULL,
    Description NVARCHAR(500) NULL,
    CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_TodoList_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT FK_TodoList_TodoCategory FOREIGN KEY (CategoryId) REFERENCES dbo.TodoCategory(Id)
);

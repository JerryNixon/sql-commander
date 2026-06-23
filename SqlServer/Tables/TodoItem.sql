CREATE TABLE dbo.TodoItem
(
    Id INT NOT NULL PRIMARY KEY,
    TodoListId INT NOT NULL,
    ParentTodoItemId INT NULL,
    Title NVARCHAR(200) NOT NULL,
    Notes NVARCHAR(1000) NULL,
    DueDate DATE NULL,
    Priority TINYINT NOT NULL CONSTRAINT DF_TodoItem_Priority DEFAULT (2),
    IsCompleted BIT NOT NULL CONSTRAINT DF_TodoItem_IsCompleted DEFAULT (0),
    CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_TodoItem_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CompletedAt DATETIME2(0) NULL,
    CONSTRAINT FK_TodoItem_TodoList FOREIGN KEY (TodoListId) REFERENCES dbo.TodoList(Id),
    CONSTRAINT FK_TodoItem_ParentTodoItem FOREIGN KEY (ParentTodoItemId) REFERENCES dbo.TodoItem(Id)
);

CREATE TABLE dbo.TodoItemTag
(
    TodoItemId INT NOT NULL,
    TodoTagId INT NOT NULL,
    CONSTRAINT PK_TodoItemTag PRIMARY KEY (TodoItemId, TodoTagId),
    CONSTRAINT FK_TodoItemTag_TodoItem FOREIGN KEY (TodoItemId) REFERENCES dbo.TodoItem(Id),
    CONSTRAINT FK_TodoItemTag_TodoTag FOREIGN KEY (TodoTagId) REFERENCES dbo.TodoTag(Id)
);

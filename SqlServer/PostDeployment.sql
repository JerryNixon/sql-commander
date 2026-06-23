SET NOCOUNT ON;

-- Seed data only if Series table is empty (acts as a simple guard against reseeding)
IF NOT EXISTS (SELECT 1 FROM dbo.Series)
BEGIN
    PRINT 'Seeding initial data...';

    INSERT INTO dbo.Series (Id, Name) VALUES 
      (1, 'Star Trek'),
      (2, 'Star Trek: The Next Generation'),
      (3, 'Star Trek: Voyager'),
      (4, 'Star Trek: Deep Space Nine'),
      (5, 'Star Trek: Enterprise');

    INSERT INTO dbo.Species (Id, Name) VALUES 
      (1, 'Human'), (2, 'Vulcan'), (3, 'Android'), (4, 'Klingon'), (5, 'Betazoid'),
      (6, 'Hologram'), (7, 'Bajoran'), (8, 'Changeling'), (9, 'Trill'), (10, 'Ferengi'),
      (11, 'Denobulan'), (12, 'Borg');

    INSERT INTO dbo.Actor (Id, Name, BirthYear) VALUES 
      (1, 'William Shatner', 1931), (2, 'Leonard Nimoy', 1931), (3, 'DeForest Kelley', 1920),
      (4, 'James Doohan', 1920), (5, 'Nichelle Nichols', 1932), (6, 'George Takei', 1937),
      (7, 'Walter Koenig', 1936), (8, 'Patrick Stewart', 1940), (9, 'Jonathan Frakes', 1952),
      (10, 'Brent Spiner', 1949), (11, 'Michael Dorn', 1952), (12, 'Gates McFadden', 1949),
      (13, 'Marina Sirtis', 1955), (14, 'LeVar Burton', 1957), (15, 'Kate Mulgrew', 1955),
      (16, 'Robert Beltran', 1953), (17, 'Tim Russ', 1956), (18, 'Roxann Dawson', 1958),
      (19, 'Robert Duncan McNeill', 1964), (20, 'Garrett Wang', 1968), (21, 'Robert Picardo', 1953),
      (22, 'Jeri Ryan', 1968), (23, 'Avery Brooks', 1948), (24, 'Nana Visitor', 1957),
      (25, 'Rene Auberjonois', 1940), (26, 'Terry Farrell', 1963), (27, 'Alexander Siddig', 1965),
      (28, 'Armin Shimerman', 1949), (29, 'Cirroc Lofton', 1978), (30, 'Scott Bakula', 1954),
      (31, 'Jolene Blalock', 1975), (32, 'John Billingsley', 1960), (33, 'Connor Trinneer', 1969),
      (34, 'Dominic Keating', 1962), (35, 'Linda Park', 1978), (36, 'Anthony Montgomery', 1971);

    INSERT INTO dbo.[Character] (Id, Name, ActorId, Stardate) VALUES 
      (1, 'James T. Kirk', 1, 2233.04), (2, 'Spock', 2, 2230.06), (3, 'Leonard McCoy', 3, 2227.00),
      (4, 'Montgomery Scott', 4, 2222.00), (5, 'Uhura', 5, 2233.00), (6, 'Hikaru Sulu', 6, 2237.00),
      (7, 'Pavel Chekov', 7, 2245.00), (8, 'Jean-Luc Picard', 8, 2305.07), (9, 'William Riker', 9, 2335.08),
      (10, 'Data', 10, 2336.00), (11, 'Worf', 11, 2340.00), (12, 'Beverly Crusher', 12, 2324.00),
      (13, 'Deanna Troi', 13, 2336.00), (14, 'Geordi La Forge', 14, 2335.02), (15, 'Kathryn Janeway', 15, 2336.05),
      (16, 'Chakotay', 16, 2329.00), (17, 'Tuvok', 17, 2264.00), (18, 'B''Elanna Torres', 18, 2349.00),
      (19, 'Tom Paris', 19, 2346.00), (20, 'Harry Kim', 20, 2349.00), (21, 'The Doctor', 21, 2371.00),
      (22, 'Seven of Nine', 22, 2348.00), (23, 'Benjamin Sisko', 23, 2332.00), (24, 'Kira Nerys', 24, 2343.00),
      (25, 'Odo', 25, 2337.00), (27, 'Jadzia Dax', 26, 2341.00), (28, 'Julian Bashir', 27, 2341.00),
      (29, 'Quark', 28, 2333.00), (30, 'Jake Sisko', 29, 2355.00), (31, 'Jonathan Archer', 30, 2112.00),
      (32, 'T''Pol', 31, 2088.00), (33, 'Phlox', 32, 2102.00), (34, 'Charles "Trip" Tucker III', 33, 2121.00),
      (35, 'Malcolm Reed', 34, 2117.00), (36, 'Hoshi Sato', 35, 2129.00), (37, 'Travis Mayweather', 36, 2126.00);

    INSERT INTO dbo.Series_Character (SeriesId, CharacterId, Role) VALUES 
      (1,1,'Captain'), (1,2,'Science Officer'), (1,3,'Doctor'), (1,4,'Engineer'), (1,5,'Communications Officer'),
      (1,6,'Helmsman'), (1,7,'Navigator'),
      (2,8,'Captain'), (2,9,'First Officer'), (2,10,'Operations Officer'), (2,11,'Security Officer'),
      (2,12,'Doctor'), (2,13,'Counselor'), (2,14,'Engineer'),
      (3,15,'Captain'), (3,16,'First Officer'), (3,17,'Tactical Officer'), (3,18,'Engineer'), (3,19,'Helmsman'),
      (3,20,'Operations Officer'), (3,21,'Doctor'), (3,22,'Astrometrics Officer'),
      (4,23,'Commanding Officer'), (4,24,'First Officer'), (4,25,'Security Officer'), (4,11,'Strategic Operations Officer'),
      (4,27,'Science Officer'), (4,28,'Doctor'), (4,29,'Bar Owner'), (4,30,'Civilian'),
      (5,31,'Captain'), (5,32,'Science Officer'), (5,33,'Doctor'), (5,34,'Chief Engineer'), (5,35,'Armory Officer'),
      (5,36,'Communications Officer'), (5,37,'Helmsman');

    INSERT INTO dbo.Character_Species (CharacterId, SpeciesId) VALUES 
      (1,1),(2,2),(2,1),(3,1),(4,1),(5,1),(6,1),(7,1),(8,1),(9,1),(10,3),(11,4),(12,1),(13,1),(13,5),(14,1),
      (15,1),(16,1),(17,2),(18,1),(18,4),(19,1),(20,1),(21,6),(22,1),(22,12),(23,1),(24,7),(25,8),(27,9),
      (28,1),(29,10),(30,1),(31,1),(32,2),(33,11),(34,1),(35,1),(36,1),(37,1);
END
GO

-- Seed todo demo data only if TodoCategory is empty. These related tables make the diagram feature more interesting.
IF OBJECT_ID(N'dbo.TodoCategory', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM dbo.TodoCategory)
BEGIN
    PRINT 'Seeding todo demo data...';

    INSERT INTO dbo.TodoCategory (Id, Name, Color, SortOrder) VALUES
      (1, N'Home', N'#4f46e5', 1),
      (2, N'Work', N'#0891b2', 2),
      (3, N'Errands', N'#f97316', 3),
      (4, N'Learning', N'#16a34a', 4);

    INSERT INTO dbo.TodoList (Id, CategoryId, Name, Description, CreatedAt) VALUES
      (1, 1, N'Weekend Reset', N'Household tasks to make Monday less dramatic.', '2026-06-01T09:00:00'),
      (2, 2, N'SQL Commander Demo', N'Polish the demo flow for schema exploration.', '2026-06-02T10:30:00'),
      (3, 3, N'Groceries', N'Things to pick up around town.', '2026-06-03T08:15:00'),
      (4, 4, N'Database Practice', N'Small relational modeling exercises.', '2026-06-04T14:00:00');

    INSERT INTO dbo.TodoTag (Id, Name, Color) VALUES
      (1, N'urgent', N'#dc2626'),
      (2, N'demo', N'#9333ea'),
      (3, N'blocked', N'#64748b'),
      (4, N'quick win', N'#22c55e'),
      (5, N'follow-up', N'#0ea5e9');

    INSERT INTO dbo.TodoItem (Id, TodoListId, ParentTodoItemId, Title, Notes, DueDate, Priority, IsCompleted, CreatedAt, CompletedAt) VALUES
      (1, 1, NULL, N'Clean kitchen', N'Counters, sink, and floors.', '2026-06-22', 2, 0, '2026-06-01T09:05:00', NULL),
      (2, 1, 1, N'Empty dishwasher', N'Put everything back where future you can find it.', '2026-06-22', 3, 1, '2026-06-01T09:10:00', '2026-06-01T09:40:00'),
      (3, 1, 1, N'Take out recycling', NULL, '2026-06-22', 2, 0, '2026-06-01T09:15:00', NULL),
      (4, 2, NULL, N'Create todo diagram demo', N'Add related tables with categories, lists, items, and tags.', '2026-06-23', 1, 0, '2026-06-02T10:35:00', NULL),
      (5, 2, 4, N'Refresh metadata', N'Confirm tables appear in SQL Commander.', '2026-06-23', 2, 0, '2026-06-02T10:45:00', NULL),
      (6, 2, 4, N'Open diagram', N'Use the Diagram button under the tree pane.', '2026-06-23', 2, 0, '2026-06-02T10:50:00', NULL),
      (7, 3, NULL, N'Buy coffee', N'Essential operational dependency.', '2026-06-21', 1, 1, '2026-06-03T08:20:00', '2026-06-03T09:05:00'),
      (8, 3, NULL, N'Pick up fruit', N'Apples, bananas, and berries.', '2026-06-21', 3, 0, '2026-06-03T08:25:00', NULL),
      (9, 4, NULL, N'Model many-to-many relationships', N'Use TodoItemTag as the bridge table.', '2026-06-24', 2, 0, '2026-06-04T14:05:00', NULL),
      (10, 4, 9, N'Add self-referencing task', N'ParentTodoItemId demonstrates hierarchy.', '2026-06-24', 2, 0, '2026-06-04T14:10:00', NULL);

    INSERT INTO dbo.TodoItemTag (TodoItemId, TodoTagId) VALUES
      (1, 4),
      (3, 5),
      (4, 1), (4, 2),
      (5, 2), (5, 5),
      (6, 2),
      (7, 4),
      (9, 2),
      (10, 2), (10, 5);
END
GO

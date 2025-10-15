var builder = DistributedApplication.CreateBuilder(args);

var sql = builder
    .AddSqlServer("sql", port: 2468)
    .WithLifetime(ContainerLifetime.Persistent)
    .WithDataVolume("sql-commander-data-volume")
    .AddDatabase("db");

var sqlproj = builder
    .AddSqlProject<Projects.SqlServer>("sqlproj")
    .WithReference(sql);

var sqlcmdr = builder
    .AddProject<Projects.SqlCommander_Web>("sqlcmdr")
    .WithParentRelationship(sql)
    .WaitForCompletion(sqlproj)
    .WithReference(sql);

builder.Build().Run();
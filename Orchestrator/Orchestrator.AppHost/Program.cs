var builder = DistributedApplication.CreateBuilder(args);

// A database host
var sqlServer = builder
    .AddSqlServer(name: "engineSqlServer", port: 62949)
    .WithDataVolume("engine-sqlserver-data")
    .WithLifetime(ContainerLifetime.Session);

// Login database to hold account and authentication information.
var loginServerDatabase = sqlServer.AddDatabase("LoginServerDb", "LoginServerDb");

// World database to hold character and world information.
var worldServerDatabase = sqlServer.AddDatabase("WorldServerDb", "WorldServerDb");

var migrationsRunner = builder.AddProject<Projects.SqlMigrationRunner>("SqlMigrationRunner")
    .WithReference(loginServerDatabase, connectionName: "loginServer")
    .WithReference(worldServerDatabase, connectionName: "worldServer")
    .WaitFor(loginServerDatabase)
    .WaitFor(worldServerDatabase);

// Add base services to the container.
builder.AddProject<Projects.LoginServer>("LoginServer")
    .WithReference(loginServerDatabase, connectionName: "LoginServer")
    .WaitForCompletion(migrationsRunner);

builder.AddProject<Projects.WorldServer>("WorldServer")
    .WithReference(worldServerDatabase, connectionName: "WorldServer")
    .WaitForCompletion(migrationsRunner);

// TODO: Add additional endpoints to the login and world servers to provide information about the server status, player count, etc.

// Public web application server to provide a web interface for users to view and manage their accounts and view game server information.
builder.AddProject<Projects.WebAppServer>("PublicWebAppServer")
    .WaitForCompletion(migrationsRunner)
    .WithReference(loginServerDatabase)
    .WithReference(worldServerDatabase);





builder.Build().Run();

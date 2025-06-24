var builder = DistributedApplication.CreateBuilder(args);

// TODO: Think about real login server (useing actual idp) to provide a token to be used by the client for the world server.

// Add base services to the container.
builder.AddProject<Projects.LoginServer>("loginserver");
builder.AddProject<Projects.WorldServer>("worldserver");

// TODO: Add additional endpoints to the login and world servers to provide information about the server status, player count, etc.
// TODO: Add web server project to display the login server and world server information.

builder.Build().Run();

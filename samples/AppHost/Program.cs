var builder = DistributedApplication.CreateBuilder(args);

var pgUser = builder.AddParameter("pg-username");
var pgPassword = builder.AddParameter("pg-password", secret: true);

var postgresServer = builder
    .AddPostgres("postgres-server", pgUser, pgPassword, 5432)
    .WithImageTag("15.7")
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);

var postgresDatabase = postgresServer
    .AddDatabase("hangfire")
    .WithCreationScript(
        """
        CREATE DATABASE hangfire;
        """
    );

builder.AddProject<Projects.Web>("server").WithReference(postgresDatabase).WaitFor(postgresDatabase);

var mcp = builder
    .AddProject<Projects.HangfireMCP>("hangfire-mcp")
    .WithReference(postgresDatabase)
    .WaitFor(postgresDatabase);

builder.AddMCPInspector().WithSSE(mcp).WithParentRelationship(mcp);

builder.Build().Run();

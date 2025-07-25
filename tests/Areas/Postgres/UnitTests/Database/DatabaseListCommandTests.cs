// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Parsing;
using System.Text.Json;
using System.Text.Json.Serialization;
using AzureMcp.Areas.Postgres.Commands.Database;
using AzureMcp.Areas.Postgres.Services;
using AzureMcp.Models.Command;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace AzureMcp.Tests.Areas.Postgres.UnitTests.Database;

[Trait("Area", "Postgres")]
public class DatabaseListCommandTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IPostgresService _postgresService;
    private readonly ILogger<DatabaseListCommand> _logger;

    public DatabaseListCommandTests()
    {
        _postgresService = Substitute.For<IPostgresService>();
        _logger = Substitute.For<ILogger<DatabaseListCommand>>();

        var collection = new ServiceCollection();
        collection.AddSingleton(_postgresService);

        _serviceProvider = collection.BuildServiceProvider();
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsDatabases_WhenDatabasesExist()
    {
        var expectedDatabases = new List<string> { "db1", "db2" };
        _postgresService.ListDatabasesAsync("sub123", "rg1", "user1", "server1").Returns(expectedDatabases);

        var command = new DatabaseListCommand(_logger);
        var args = command.GetCommand().Parse(["--subscription", "sub123", "--resource-group", "rg1", "--user-name", "user1", "--server", "server1"]);
        var context = new CommandContext(_serviceProvider);

        var response = await command.ExecuteAsync(context, args);

        Assert.NotNull(response);
        Assert.Equal(200, response.Status);
        Assert.Equal("Success", response.Message);
        Assert.NotNull(response.Results);

        var json = JsonSerializer.Serialize(response.Results);
        var result = JsonSerializer.Deserialize<DatabaseListResult>(json);
        Assert.NotNull(result);
        Assert.Equal(expectedDatabases, result.Databases);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsMessage_WhenNoDatabasesExist()
    {
        _postgresService.ListDatabasesAsync("sub123", "rg1", "user1", "server1").Returns([]);

        var command = new DatabaseListCommand(_logger);
        var args = command.GetCommand().Parse(["--subscription", "sub123", "--resource-group", "rg1", "--user-name", "user1", "--server", "server1"]);
        var context = new CommandContext(_serviceProvider);

        var response = await command.ExecuteAsync(context, args);

        Assert.NotNull(response);
        Assert.Equal(200, response.Status);
        Assert.Equal("Success", response.Message);
        Assert.Null(response.Results);
    }

    [Fact]
    public async Task ExecuteAsync_HandlesException()
    {
        _postgresService.ListDatabasesAsync("sub123", "rg1", "user1", "server1").ThrowsAsync(new Exception("Test exception"));

        var command = new DatabaseListCommand(_logger);
        var args = command.GetCommand().Parse(["--subscription", "sub123", "--resource-group", "rg1", "--user-name", "user1", "--server", "server1"]);
        var context = new CommandContext(_serviceProvider);
        var response = await command.ExecuteAsync(context, args);

        Assert.NotNull(response);
        Assert.Equal(500, response.Status);
        Assert.Contains("Test exception", response.Message);
    }

    [Theory]
    [InlineData("--subscription")]
    [InlineData("--resource-group")]
    [InlineData("--user-name")]
    [InlineData("--server")]
    public async Task ExecuteAsync_ReturnsError_WhenParameterIsMissing(string missingParameter)
    {
        var command = new DatabaseListCommand(_logger);
        var args = command.GetCommand().Parse(new string[]
        {
            missingParameter == "--subscription" ? "" : "--subscription", "sub123",
            missingParameter == "--resource-group" ? "" : "--resource-group", "rg1",
            missingParameter == "--user-name" ? "" : "--user-name", "user1",
            missingParameter == "--server" ? "" : "--server", "server123",
        });

        var context = new CommandContext(_serviceProvider);
        var response = await command.ExecuteAsync(context, args);

        Assert.NotNull(response);
        Assert.Equal(400, response.Status);
        Assert.Equal($"Missing Required options: {missingParameter}", response.Message);
    }


    private class DatabaseListResult
    {
        [JsonPropertyName("Databases")]
        public List<string> Databases { get; set; } = new List<string>();
    }
}

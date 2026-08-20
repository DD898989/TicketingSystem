using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using System;
using System.IO;
using System.Net.Http;

namespace TicketingSystem.BddTests.Support;

public class TestHost : IDisposable
{
    public WebApplicationFactory<Program> App { get; }
    public HttpClient Client { get; }
    public string DbPath { get; }
    public string DbConn { get; }
    public string RedisConn { get; }

    public TestHost()
    {
        // Use a unique database file name for each scenario to run completely isolated
        DbPath = Path.Combine(AppContext.BaseDirectory, $"tickets_test_{Guid.NewGuid()}.db");
        DbConn = $"Data Source={DbPath}";
        
        // Use 127.0.0.1 instead of localhost on Windows to guarantee stable IPv4 resolution
        RedisConn = "127.0.0.1:6379";

        // Set the environment variables before building the WebApplicationFactory
        Environment.SetEnvironmentVariable("SQLITE_CONNECTION", DbConn);
        Environment.SetEnvironmentVariable("REDIS_CONNECTION", RedisConn);

        App = new WebApplicationFactory<Program>();
        Client = App.CreateClient();
    }

    public IDatabase GetRedisDatabase()
    {
        return App.Services.GetRequiredService<IDatabase>();
    }

    public TicketingDbContext GetDbContext()
    {
        // Return a fresh context using a scope
        var scope = App.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<TicketingDbContext>();
    }

    public void Dispose()
    {
        Client.Dispose();
        App.Dispose();

        // Release SQLite file locks and delete the temp db file
        if (File.Exists(DbPath))
        {
            try
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                File.Delete(DbPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting test database file: {ex.Message}");
            }
        }
    }
}

using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using TicketingSystem;
using TicketingSystem.Endpoints;

var builder = WebApplication.CreateBuilder(args);

var redisConn = Environment.GetEnvironmentVariable("REDIS_CONNECTION") ?? "redis:6379";
var redis = ConnectionMultiplexer.Connect(redisConn);
builder.Services.AddSingleton<IDatabase>(redis.GetDatabase());

var sqliteConn = Environment.GetEnvironmentVariable("SQLITE_CONNECTION") ?? "Data Source=db/tickets.db";
builder.Services.AddDbContext<TicketingDbContext>(options => options.UseSqlite(sqliteConn), ServiceLifetime.Transient);
builder.Services.AddHostedService<Consumer>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TicketingDbContext>();
    db.Database.EnsureCreated();
}

Init.Map(app);
Book.Map(app);

app.Run();

public partial class Program { }

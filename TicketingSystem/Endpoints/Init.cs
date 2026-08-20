using StackExchange.Redis;
using Microsoft.EntityFrameworkCore;

namespace TicketingSystem.Endpoints;


public static class Init
{
    public record InitRequest(string ConcertId, string AreaId, int TotalTickets);
    public static void Map(this IEndpointRouteBuilder app)
    {
        app.MapPost("/init", (
            IDatabase r,
            TicketingDbContext db,
            [Microsoft.AspNetCore.Mvc.FromBody] List<InitRequest> requests
            ) =>
        {

            db.Orders.ExecuteDelete();
            foreach (var key in (string[])r.Execute("KEYS", "*"))
                r.KeyDelete(key);


            foreach (var request in requests)
            {
                var key = $"ticketLeft:{request.ConcertId}:{request.AreaId}";
                r.StringSet(key, request.TotalTickets);
            }

            return Results.Ok();
        });
    }
}

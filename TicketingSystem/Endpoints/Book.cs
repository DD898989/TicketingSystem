using StackExchange.Redis;

namespace TicketingSystem.Endpoints;

public static class Book
{
    public record BookRequest(string ConcertId, string AreaId, string MemberId, int Quantity);

    public static void Map(this IEndpointRouteBuilder app)
    {
        app.MapPost("/book", async (
            IDatabase r,
            BookRequest req
            ) =>
        {
            var ticketLeftKey = $"ticketLeft:{req.ConcertId}:{req.AreaId}";
            var memberLimitKey = $"memberLimit:{req.ConcertId}:{req.AreaId}:{req.MemberId}";
            var ticketOrderKey = $"ticketOrder:{req.ConcertId}:{req.AreaId}:{req.MemberId}:{Guid.NewGuid()}";

            int maxQtyPerMember = 4;

            int okCode = 0;
            int exceedLimitCode = -1;
            int insufficientStockCode = -2;

            var keys = new RedisKey[] { ticketLeftKey, memberLimitKey, ticketOrderKey };
            var values = new RedisValue[] { req.Quantity, maxQtyPerMember };

            var luaScript = $@"
local ticketLeft = KEYS[1]
local memberLimit = KEYS[2]
local ticketOrder = KEYS[3]
local quantity = tonumber(ARGV[1])
local maxLimit = tonumber(ARGV[2])

local currentOrdered = tonumber(redis.call('GET', memberLimit) or '0')
if currentOrdered + quantity > maxLimit then
    return {exceedLimitCode}
end

local left = tonumber(redis.call('GET', ticketLeft) or '0')
if left < quantity then
    return {insufficientStockCode}
end

redis.call('DECRBY', ticketLeft, quantity)
redis.call('INCRBY', memberLimit, quantity)
redis.call('SET', ticketOrder, quantity)
return {okCode}
";

            var result = (int)await r.ScriptEvaluateAsync(luaScript, keys, values);

            if (result != okCode)
                return Results.BadRequest(result);
            return Results.Ok(new {});
        });
    }
}
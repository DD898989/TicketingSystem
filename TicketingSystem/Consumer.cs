using StackExchange.Redis;

namespace TicketingSystem;

public class Consumer(
    IDatabase r, 
    IServiceScopeFactory scopeFactory
    ) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken st)
    {
        while (!st.IsCancellationRequested)
        {
            await Task.Delay(500);

            try
            {
                var keys = r.Multiplexer.GetServer(r.Multiplexer.GetEndPoints()[0]).Keys(pattern: "ticketOrder:*").ToList();

                int batchSize = 1000;
                for (int i = 0; i < keys.Count; i += batchSize)
                {
                    var ordersToInsert = new List<Order>();

                    var batchKeys = keys.Skip(i).Take(batchSize).ToList();
                    foreach ( var key in batchKeys)
                    {
                        var quantityVal = await r.StringGetDeleteAsync(key);
                        var parts = key.ToString().Split(':');

                        ordersToInsert.Add(new Order
                        {
                            ConcertId = parts[1],
                            AreaId = parts[2],
                            MemberId = parts[3],
                            Quantity = int.Parse(quantityVal.ToString()),
                            CreatedAt = DateTime.Now,
                        });
                    }

                    using (var scope = scopeFactory.CreateScope())
                    {
                        var db = scope.ServiceProvider.GetRequiredService<TicketingDbContext>();
                        db.Orders.AddRange(ordersToInsert);
                        await db.SaveChangesAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing orders in background consumer: {ex.Message}");
            }
        }
    }
}

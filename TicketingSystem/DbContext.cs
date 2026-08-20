using Microsoft.EntityFrameworkCore;

namespace TicketingSystem;

public class TicketingDbContext(DbContextOptions<TicketingDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
}

public class Order
{
    public int Id { get; set; }
    public string ConcertId { get; set; }
    public string AreaId { get; set; }
    public string MemberId { get; set; }
    public int Quantity { get; set; }
    public DateTime CreatedAt { get; set; }
}

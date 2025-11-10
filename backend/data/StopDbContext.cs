using Microsoft.EntityFrameworkCore;

public class StopsDbContext : DbContext
{
    public StopsDbContext(DbContextOptions<StopsDbContext> options) : base(options) { }
    public DbSet<Stop> Stops { get; set; }
}
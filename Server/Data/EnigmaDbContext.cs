using Microsoft.EntityFrameworkCore;

namespace Enigma.Server.Data;

public class EnigmaDbContext : DbContext
{
    public EnigmaDbContext(DbContextOptions<EnigmaDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    }
}
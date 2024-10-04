using AssistantAI.DataTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AssistantAI;

public class SqliteDatabaseContext : DbContext
{
    public DbSet<UserData> UserDataSet { get; set; }
    public DbSet<GuildData> GuildDataSet { get; set; }
    public DbSet<ChannelData> ChannelDataSet { get; set; }

    public SqliteDatabaseContext(DbContextOptions<SqliteDatabaseContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    }
}
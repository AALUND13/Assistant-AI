using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AssistantAI;

public class SqliteDatabaseContext : DbContext
{
    public DbSet<UserData> UserDataSet { get; set; }
    public DbSet<GuildData> GuildDataSet { get; set; }
    public DbSet<ChannelData> ChannelDataSet { get; set; }

    public SqliteDatabaseContext(DbContextOptions<SqliteDatabaseContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        modelBuilder.Entity<ChannelData>()
            .HasMany(c => c.ChatMessages)
            .WithOne(cm => cm.Channel)
            .HasForeignKey(cm => cm.ChannelId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserData>()
            .HasMany(u => u.UserMemory)
            .WithOne(m => m.RelatedData)
            .HasForeignKey(um => um.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<GuildData>()
            .HasMany(g => g.GuildMemory)
            .WithOne(m => m.RelatedData)
            .HasForeignKey(gm => gm.GuildId)
            .OnDelete(DeleteBehavior.Cascade);

        base.OnModelCreating(modelBuilder);
    }

}
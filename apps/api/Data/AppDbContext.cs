using Microsoft.EntityFrameworkCore;

public class AppDbContext : DbContext
{
    // This line below is a contructor, it connects us to the DB based on how we configured Program.cs
    public AppDbContext(DbContextOptions<AppDbContext> opt) : base(opt) {}

    public DbSet<Person> People => Set<Person>();
    public DbSet<PersonRole> PersonRoles => Set<PersonRole>();
    public DbSet<AuthAccount> AuthAccounts => Set<AuthAccount>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<FileRecord> Files => Set<FileRecord>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
using ARH.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ARH.Data{

    public class ArhDbContext : DbContext
    {
        public ArhDbContext(
            DbContextOptions<ArhDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users => Set<User>();

        public DbSet<Application> Applications => Set<Application>();

        public DbSet<AccessRequest> AccessRequests
            => Set<AccessRequest>();

        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

        protected override void OnModelCreating(
            ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfigurationsFromAssembly(
                typeof(ArhDbContext).Assembly);
        }
    }
}
using Microsoft.EntityFrameworkCore;
using VulnerableApp.Models;

namespace VulnerableApp.Data
{
    public class AppDbContext : DbContext
    {
        private const string AdminPasswordHash = "$2a$11$abcdefghijklmnopqrstuuD7r1iou4Nkda8bawO9Sa8HY8tugj9ba";
        private const string User1PasswordHash = "$2a$11$abcdefghijklmnopqrstuuN8hM0Lie0.O7wznMeu.K7hWZUu6ZDQa";
        private const string User2PasswordHash = "$2a$11$abcdefghijklmnopqrstuuI5mYSFuFNN98A/6se3.SVFJAGDoYqvy";

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var seedDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Local);

            modelBuilder.Entity<User>()
                .Property(user => user.Balance)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<User>().HasData(
                new User { Id = 1, Username = "admin", PasswordHash = AdminPasswordHash, Email = "admin@test.com", Balance = 1000m, CreatedAt = seedDate },
                new User { Id = 2, Username = "user1", PasswordHash = User1PasswordHash, Email = "user@test.com", Balance = 500m, CreatedAt = seedDate },
                new User { Id = 3, Username = "user2", PasswordHash = User2PasswordHash, Email = "user2@test.com", Balance = 750m, CreatedAt = seedDate }
            );
        }
    }
}

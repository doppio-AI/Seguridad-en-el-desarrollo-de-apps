using Microsoft.EntityFrameworkCore;
using VulnerableApp.Models;

namespace VulnerableApp.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>().HasData(
    new User
    {
        Id = 1,
        Username = "admin",
        Password = "admin",
        PasswordHash = "$2b$11$.hn63SQVzdNRGjD8s7z2ku0TgnAOUcaKphgsmENA9UFGbrqcEXpaS",
        Email = "admin@test.com",
        Balance = 1000m,
        CreatedAt = new DateTime(2024, 1, 1)
    },
    new User
    {
        Id = 2,
        Username = "user1",
        Password = "123456",
        PasswordHash = "$2b$11$YhMOm3Kw9pWhdrBTGtQqfO6Y6DpGSkIsrP3g3yWqwO9rDWFrgjJsC",
        Email = "user@test.com",
        Balance = 500m,
        CreatedAt = new DateTime(2024, 1, 1)
    },
    new User
    {
        Id = 3,
        Username = "user2",
        Password = "password",
        PasswordHash = "$2b$11$9EwQ4bIoyc5GOxKi6MZSueGbDRZFqtXBlfi9Y7gS1TOPB/xB5dV2u",
        Email = "user2@test.com",
        Balance = 750m,
        CreatedAt = new DateTime(2024, 1, 1)
    }
);
        }
    }
}
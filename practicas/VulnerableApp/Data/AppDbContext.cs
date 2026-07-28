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
        PasswordHash = "$2a$11$nMD.vQH858TzSJiSyNKexucoSuTx3U58hzaN09P40bDzluK1XP6M2",
        Email = "admin@test.com",
        Balance = 1000m,
        CreatedAt = new DateTime(2024, 1, 1)
    },
    new User
    {
        Id = 2,
        Username = "user1",
        Password = "123456",
        PasswordHash = "$2a$11$kzcuxEZN/oNktHiSoAUYaOWEhWs7MxgTShk.o9MBIi.z7BwtL6tIm",
        Email = "user@test.com",
        Balance = 500m,
        CreatedAt = new DateTime(2024, 1, 1)
    },
    new User
    {
        Id = 3,
        Username = "user2",
        Password = "password",
        PasswordHash = "$2a$11$Yr9Dm.2oDsD5h8.uXXBkHecPNENuJnowA/2CUcSk2TOvMz4mBiGSi",
        Email = "user2@test.com",
        Balance = 750m,
        CreatedAt = new DateTime(2024, 1, 1)
    }
);
        }
    }
}
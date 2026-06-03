namespace VulnerableApp.Models
{
    public class User
    {
        public int Id { get; set; }

        public string Username { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty; // Vulnerable: sin hash

        public string Email { get; set; } = string.Empty;

        public decimal Balance { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
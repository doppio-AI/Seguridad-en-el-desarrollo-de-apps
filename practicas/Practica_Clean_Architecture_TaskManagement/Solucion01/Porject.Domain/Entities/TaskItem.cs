namespace Project.Domain.Entities
{
    public class TaskItem
    {
        public Guid Id { get; private set; }
        public string Title { get; private set; } = string.Empty;
        public string Description { get; private set; } = string.Empty;
        public bool IsCompleted { get; private set; }
        public DateTime CreatedAt { get; private set; }

        private TaskItem() { }

        public static TaskItem Create(string title, string description)
        {
            if (string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("El título es requerido");

            return new TaskItem
            {
                Id = Guid.NewGuid(),
                Title = title,
                Description = description ?? string.Empty,
                IsCompleted = false,
                CreatedAt = DateTime.UtcNow
            };
        }

        public void MarkAsCompleted()
        {
            IsCompleted = true;
        }
    }
}
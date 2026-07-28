namespace Project.Application.DTOs
{
    public record TaskDto(
        Guid Id,
        string Title,
        string Description,
        bool IsCompleted
    );
}
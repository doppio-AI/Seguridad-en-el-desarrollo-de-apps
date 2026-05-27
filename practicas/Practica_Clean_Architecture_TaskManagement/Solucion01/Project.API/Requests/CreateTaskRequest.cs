namespace Project.Api.Requests
{
    public record CreateTaskRequest(
        string Title,
        string Description
    );
}
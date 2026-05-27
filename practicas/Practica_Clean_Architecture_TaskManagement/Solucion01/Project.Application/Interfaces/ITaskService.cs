using Project.Application.DTOs;

namespace Project.Application.Interfaces
{
    public interface ITaskService
    {
        Task<IEnumerable<TaskDto>> GetTasksAsync();
        Task<TaskDto> CreateTaskAsync(string title, string description);
        Task<TaskDto> CompleteTaskAsync(Guid id);
    }
}
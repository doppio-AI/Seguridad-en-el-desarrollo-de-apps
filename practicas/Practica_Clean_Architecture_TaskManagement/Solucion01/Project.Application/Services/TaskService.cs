using Project.Application.DTOs;
using Project.Application.Interfaces;
using Project.Domain.Entities;

namespace Project.Application.Services
{
    public class TaskService : ITaskService
    {
        private readonly ITaskRepository _taskRepository;

        public TaskService(ITaskRepository taskRepository)
        {
            _taskRepository = taskRepository;
        }

        public async Task<IEnumerable<TaskDto>> GetTasksAsync()
        {
            var tasks = await _taskRepository.GetAllAsync();

            return tasks.Select(task => new TaskDto(
                task.Id,
                task.Title,
                task.Description,
                task.IsCompleted
            ));
        }

        public async Task<TaskDto> CreateTaskAsync(string title, string description)
        {
            var task = TaskItem.Create(title, description);

            await _taskRepository.AddAsync(task);

            return new TaskDto(
                task.Id,
                task.Title,
                task.Description,
                task.IsCompleted
            );
        }

        public async Task<TaskDto> CompleteTaskAsync(Guid id)
        {
            var task = await _taskRepository.GetByIdAsync(id);

            if (task is null)
                throw new KeyNotFoundException("La tarea no fue encontrada");

            task.MarkAsCompleted();

            await _taskRepository.UpdateAsync(task);

            return new TaskDto(
                task.Id,
                task.Title,
                task.Description,
                task.IsCompleted
            );
        }
    }
}
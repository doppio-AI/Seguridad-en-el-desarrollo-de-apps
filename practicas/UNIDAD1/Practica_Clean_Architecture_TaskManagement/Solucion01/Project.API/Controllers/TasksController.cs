using Microsoft.AspNetCore.Mvc;
using Project.Api.Requests;
using Project.Application.DTOs;
using Project.Application.Interfaces;

namespace Project.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TasksController : ControllerBase
    {
        private readonly ITaskService _taskService;

        public TasksController(ITaskService taskService)
        {
            _taskService = taskService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<TaskDto>>> GetAll()
        {
            var tasks = await _taskService.GetTasksAsync();
            return Ok(tasks);
        }

        [HttpPost]
        public async Task<ActionResult<TaskDto>> Create([FromBody] CreateTaskRequest request)
        {
            var task = await _taskService.CreateTaskAsync(
                request.Title,
                request.Description
            );

            return CreatedAtAction(nameof(GetAll), new { id = task.Id }, task);
        }

        [HttpPut("{id:guid}/complete")]
        public async Task<ActionResult<TaskDto>> Complete(Guid id)
        {
            var task = await _taskService.CompleteTaskAsync(id);
            return Ok(task);
        }
    }
}
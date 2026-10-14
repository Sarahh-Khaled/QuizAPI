using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuizAPI.Dtos;
using QuizAPI.Entities;
using QuizAPI.Services;

namespace QuizAPI.Controllers;

[ApiController]
[Route("api/quizzes/{quizId:int}")]
[Authorize]
public class AttemptsController : ControllerBase
{
    private readonly AttemptService _service;

    public AttemptsController(AttemptService service) => _service = service;

    private string UserId => User.FindFirst("sub")!.Value;
    private bool IsTeacher => User.IsInRole(Roles.Teacher);

    [HttpPost("attempts")]
    [Authorize(Roles = Roles.Student)]
    public async Task<IActionResult> Start(int quizId)
    {
        var result = await _service.StartAsync(quizId, UserId);
        return CreatedAtAction(nameof(GetAttempt), new { quizId, attemptId = result.AttemptId }, result);
    }

    [HttpPost("attempts/{attemptId:int}/answers")]
    [Authorize(Roles = Roles.Student)]
    public async Task<ActionResult<AttemptResultDto>> Submit(
        int quizId, int attemptId, SubmitAnswersDto dto)
        => Ok(await _service.SubmitAsync(quizId, attemptId, UserId, dto));

    [HttpGet("attempts/{attemptId:int}")]
    public async Task<ActionResult<AttemptResultDto>> GetAttempt(int quizId, int attemptId)
        => Ok(await _service.GetAsync(quizId, attemptId, UserId, IsTeacher));

    [HttpGet("attempts")]
    [Authorize(Roles = Roles.Teacher)]
    public async Task<ActionResult<PagedResult<AttemptSummaryDto>>> GetAll(
        int quizId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        => Ok(await _service.GetAllForQuizAsync(quizId, UserId, page, pageSize));

    [HttpGet("statistics")]
    [Authorize(Roles = Roles.Teacher)]
    public async Task<ActionResult<QuizStatisticsDto>> Statistics(int quizId)
        => Ok(await _service.GetStatisticsAsync(quizId, UserId));
}

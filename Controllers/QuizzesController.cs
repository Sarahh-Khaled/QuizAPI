using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuizAPI.Dtos;
using QuizAPI.Entities;
using QuizAPI.Services;

namespace QuizAPI.Controllers;

[ApiController]
[Route("api/quizzes")]
[Authorize]
public class QuizzesController : ControllerBase
{
    private readonly QuizService _service;

    public QuizzesController(QuizService service) => _service = service;

    private string UserId => User.FindFirst("sub")!.Value;
    private bool IsTeacher => User.IsInRole(Roles.Teacher);

    [HttpGet]
    public async Task<ActionResult<PagedResult<QuizSummaryDto>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null)
        => Ok(await _service.GetAllAsync(UserId, IsTeacher, page, pageSize, search));

    [HttpGet("{quizId:int}")]
    public async Task<IActionResult> GetById(int quizId)
    {
        if (IsTeacher)
            return Ok(await _service.GetForTeacherAsync(quizId, UserId));

        return Ok(await _service.GetForStudentAsync(quizId));
    }

    [HttpPost]
    [Authorize(Roles = Roles.Teacher)]
    public async Task<IActionResult> Create(CreateQuizDto dto)
    {
        var quiz = await _service.CreateAsync(UserId, dto);
        return CreatedAtAction(nameof(GetById), new { quizId = quiz.Id }, quiz);
    }

    [HttpPut("{quizId:int}")]
    [Authorize(Roles = Roles.Teacher)]
    public async Task<IActionResult> Update(int quizId, UpdateQuizDto dto)
        => Ok(await _service.UpdateAsync(quizId, UserId, dto));

    [HttpDelete("{quizId:int}")]
    [Authorize(Roles = Roles.Teacher)]
    public async Task<IActionResult> Delete(int quizId)
    {
        await _service.DeleteAsync(quizId, UserId);
        return NoContent();
    }

    [HttpPost("{quizId:int}/questions")]
    [Authorize(Roles = Roles.Teacher)]
    public async Task<IActionResult> AddQuestion(int quizId, QuestionInputDto dto)
    {
        var question = await _service.AddQuestionAsync(quizId, UserId, dto);
        return StatusCode(StatusCodes.Status201Created, question);
    }

    [HttpPut("{quizId:int}/questions/{questionId:int}")]
    [Authorize(Roles = Roles.Teacher)]
    public async Task<IActionResult> UpdateQuestion(int quizId, int questionId, QuestionInputDto dto)
        => Ok(await _service.UpdateQuestionAsync(quizId, questionId, UserId, dto));

    [HttpDelete("{quizId:int}/questions/{questionId:int}")]
    [Authorize(Roles = Roles.Teacher)]
    public async Task<IActionResult> DeleteQuestion(int quizId, int questionId)
    {
        await _service.DeleteQuestionAsync(quizId, questionId, UserId);
        return NoContent();
    }
}

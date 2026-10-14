using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using QuizAPI.Data;
using QuizAPI.Dtos;
using QuizAPI.Entities;
using QuizAPI.Middleware;

namespace QuizAPI.Services;

public class QuizService
{
    private readonly AppDbContext _db;

    public QuizService(AppDbContext db) => _db = db;

    private static readonly Expression<Func<Quiz, QuizSummaryDto>> ToSummary = q => new QuizSummaryDto
    {
        Id = q.Id,
        Title = q.Title,
        Description = q.Description,
        DurationMinutes = q.DurationMinutes,
        Deadline = q.Deadline,
        IsPublished = q.IsPublished,
        TeacherId = q.TeacherId,
        TeacherName = q.Teacher.FullName,
        QuestionsCount = q.Questions.Count
    };

    // ---------- Quizzes ----------

    public async Task<PagedResult<QuizSummaryDto>> GetAllAsync(
        string userId, bool isTeacher, int page, int pageSize, string? search)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var query = _db.Quizzes.AsNoTracking();
        query = isTeacher
            ? query.Where(q => q.TeacherId == userId)
            : query.Where(q => q.IsPublished);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(q => q.Title.Contains(term) ||
                                     (q.Description != null && q.Description.Contains(term)));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(q => q.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ToSummary)
            .ToListAsync();

        return new PagedResult<QuizSummaryDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    public async Task<TeacherQuizDetailsDto> GetForTeacherAsync(int quizId, string teacherId)
    {
        var quiz = await _db.Quizzes.AsNoTracking()
            .Include(q => q.Teacher)
            .Include(q => q.Questions).ThenInclude(q => q.Options)
            .FirstOrDefaultAsync(q => q.Id == quizId)
            ?? throw new NotFoundException("Quiz not found.");

        if (quiz.TeacherId != teacherId)
            throw new ForbiddenException("You do not own this quiz.");

        return new TeacherQuizDetailsDto
        {
            Id = quiz.Id,
            Title = quiz.Title,
            Description = quiz.Description,
            DurationMinutes = quiz.DurationMinutes,
            Deadline = quiz.Deadline,
            IsPublished = quiz.IsPublished,
            TeacherId = quiz.TeacherId,
            TeacherName = quiz.Teacher.FullName,
            QuestionsCount = quiz.Questions.Count,
            Questions = quiz.Questions.Select(MapTeacherQuestion).ToList()
        };
    }

    public async Task<QuizSummaryDto> GetForStudentAsync(int quizId)
    {
        var dto = await _db.Quizzes.AsNoTracking()
            .Where(q => q.Id == quizId && q.IsPublished)
            .Select(ToSummary)
            .FirstOrDefaultAsync();

        return dto ?? throw new NotFoundException("Quiz not found.");
    }

    public async Task<QuizSummaryDto> CreateAsync(string teacherId, CreateQuizDto dto)
    {
        ValidateDeadline(dto.Deadline);

        var quiz = new Quiz
        {
            Title = dto.Title.Trim(),
            Description = dto.Description,
            DurationMinutes = dto.DurationMinutes,
            //Deadline = dto.Deadline,
            Deadline = dto.Deadline.ToUniversalTime(),
            IsPublished = false,
            TeacherId = teacherId
        };

        _db.Quizzes.Add(quiz);
        await _db.SaveChangesAsync();
        return await GetSummaryAsync(quiz.Id);
    }

    public async Task<QuizSummaryDto> UpdateAsync(int quizId, string teacherId, UpdateQuizDto dto)
    {
        var quiz = await GetEditableQuizAsync(quizId, teacherId, includeQuestions: true);
        ValidateDeadline(dto.Deadline);

        if (dto.IsPublished)
        {
            if (quiz.Questions.Count == 0)
                throw new BadRequestException("A quiz needs at least one question before it can be published.");

            if (quiz.Questions.Any(q => q.Options.Count(o => o.IsCorrect) != 1))
                throw new BadRequestException("Every question must have exactly one correct option.");
        }

        quiz.Title = dto.Title.Trim();
        quiz.Description = dto.Description;
        quiz.DurationMinutes = dto.DurationMinutes;
        //quiz.Deadline = dto.Deadline;
        quiz.Deadline = dto.Deadline.ToUniversalTime();
        quiz.IsPublished = dto.IsPublished;

        await _db.SaveChangesAsync();
        return await GetSummaryAsync(quizId);
    }

    public async Task DeleteAsync(int quizId, string teacherId)
    {
        var quiz = await GetEditableQuizAsync(quizId, teacherId);
        _db.Quizzes.Remove(quiz);
        await _db.SaveChangesAsync();
    }

    // ---------- Questions ----------

    public async Task<TeacherQuestionDto> AddQuestionAsync(int quizId, string teacherId, QuestionInputDto dto)
    {
        await GetEditableQuizAsync(quizId, teacherId);
        ValidateQuestion(dto);

        var question = new Question
        {
            QuizId = quizId,
            Text = dto.Text.Trim(),
            Points = dto.Points,
            Options = ToOptions(dto)
        };

        _db.Questions.Add(question);
        await _db.SaveChangesAsync();
        return MapTeacherQuestion(question);
    }

    public async Task<TeacherQuestionDto> UpdateQuestionAsync(
        int quizId, int questionId, string teacherId, QuestionInputDto dto)
    {
        await GetEditableQuizAsync(quizId, teacherId);
        ValidateQuestion(dto);
        var question = await FindQuestionAsync(quizId, questionId);

        question.Text = dto.Text.Trim();
        question.Points = dto.Points;

        _db.Options.RemoveRange(question.Options);
        question.Options = ToOptions(dto);

        await _db.SaveChangesAsync();
        return MapTeacherQuestion(question);
    }

    public async Task DeleteQuestionAsync(int quizId, int questionId, string teacherId)
    {
        await GetEditableQuizAsync(quizId, teacherId);
        var question = await FindQuestionAsync(quizId, questionId);

        _db.Questions.Remove(question);
        await _db.SaveChangesAsync();
    }

    // ---------- Helpers ----------

    // 404 لو الكويز مش موجود، 403 لو مش بتاعك، 409 لو اتنشر
    // 404 for not found, 403 for forbidden, 409 for conflict
    private async Task<Quiz> GetEditableQuizAsync(int quizId, string teacherId, bool includeQuestions = false)
    {
        IQueryable<Quiz> query = _db.Quizzes;
        if (includeQuestions)
            query = query.Include(q => q.Questions).ThenInclude(q => q.Options);

        var quiz = await query.FirstOrDefaultAsync(q => q.Id == quizId)
            ?? throw new NotFoundException("Quiz not found.");

        if (quiz.TeacherId != teacherId)
            throw new ForbiddenException("You do not own this quiz.");

        if (quiz.IsPublished)
            throw new ConflictException("A published quiz cannot be changed.");

        return quiz;
    }

    private async Task<Question> FindQuestionAsync(int quizId, int questionId) =>
        await _db.Questions.Include(q => q.Options)
            .FirstOrDefaultAsync(q => q.Id == questionId && q.QuizId == quizId)
        ?? throw new NotFoundException("Question not found.");

    private async Task<QuizSummaryDto> GetSummaryAsync(int quizId) =>
        await _db.Quizzes.AsNoTracking().Where(q => q.Id == quizId).Select(ToSummary).FirstAsync();

    private static void ValidateDeadline(DateTime deadline)
    {
        if (deadline.ToUniversalTime() <= DateTime.UtcNow)
            throw new BadRequestException("Deadline must be in the future.");
    }

    private static void ValidateQuestion(QuestionInputDto dto)
    {
        if (dto.Options.Count < 2)
            throw new BadRequestException("A question needs at least two options.");

        if (dto.Options.Count(o => o.IsCorrect) != 1)
            throw new BadRequestException("Every question must have exactly one correct option.");
    }

    private static List<Option> ToOptions(QuestionInputDto dto) =>
        dto.Options.Select(o => new Option { Text = o.Text.Trim(), IsCorrect = o.IsCorrect }).ToList();

    private static TeacherQuestionDto MapTeacherQuestion(Question q) => new()
    {
        Id = q.Id,
        Text = q.Text,
        Points = q.Points,
        Options = q.Options
            .Select(o => new TeacherOptionDto { Id = o.Id, Text = o.Text, IsCorrect = o.IsCorrect })
            .ToList()
    };
}

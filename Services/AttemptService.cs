using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using QuizAPI.Data;
using QuizAPI.Dtos;
using QuizAPI.Entities;
using QuizAPI.Middleware;

namespace QuizAPI.Services;

public class AttemptService
{
    private readonly AppDbContext _db;

    public AttemptService(AppDbContext db) => _db = db;

    // ---------- الطالب يبدأ المحاولة ----------
    public async Task<StartAttemptResponseDto> StartAsync(int quizId, string studentId)
    {
        var quiz = await _db.Quizzes.AsNoTracking()
            .Include(q => q.Questions).ThenInclude(q => q.Options)
            .AsSplitQuery()
            .FirstOrDefaultAsync(q => q.Id == quizId && q.IsPublished)
            ?? throw new NotFoundException("Quiz not found.");

        var now = DateTime.UtcNow;

        if (now > AsUtc(quiz.Deadline))
            throw new BadRequestException("The deadline for this quiz has passed.");

        if (await _db.Attempts.AnyAsync(a => a.QuizId == quizId && a.StudentId == studentId))
            throw new ConflictException("You have already attempted this quiz.");

        var attempt = new Attempt { QuizId = quizId, StudentId = studentId, StartedAt = now };
        _db.Attempts.Add(attempt);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // لو الطالب ضغط مرتين في نفس اللحظة، الـ Unique Index هو اللي بيحمينا
            throw new ConflictException("You have already attempted this quiz.");
        }

        return new StartAttemptResponseDto
        {
            AttemptId = attempt.Id,
            QuizId = quizId,
            StartedAt = now,
            ExpiresAt = now.AddMinutes(quiz.DurationMinutes),
            Questions = quiz.Questions.OrderBy(q => q.Id).Select(q => new StudentQuestionDto
            {
                Id = q.Id,
                Text = q.Text,
                Points = q.Points,
                Options = q.Options.OrderBy(o => o.Id)
                    .Select(o => new StudentOptionDto { Id = o.Id, Text = o.Text })
                    .ToList()
            }).ToList()
        };
    }

    // ---------- الطالب يسلّم ويتصحح ----------
    public async Task<AttemptResultDto> SubmitAsync(
        int quizId, int attemptId, string studentId, SubmitAnswersDto dto)
    {
        var attempt = await _db.Attempts
            .Include(a => a.Quiz).ThenInclude(q => q.Questions).ThenInclude(q => q.Options)
            .AsSplitQuery()
            .FirstOrDefaultAsync(a => a.Id == attemptId && a.QuizId == quizId)
            ?? throw new NotFoundException("Attempt not found.");

        if (attempt.StudentId != studentId)
            throw new ForbiddenException("This is not your attempt.");

        if (attempt.SubmittedAt != null)
            throw new ConflictException("This attempt was already submitted.");

        var now = DateTime.UtcNow;
        var expiresAt = AsUtc(attempt.StartedAt).AddMinutes(attempt.Quiz.DurationMinutes);
        if (now > expiresAt)
            throw new BadRequestException("Time is up. Answers must be submitted within the quiz duration.");

        if (dto.Answers.GroupBy(a => a.QuestionId).Any(g => g.Count() > 1))
            throw new BadRequestException("Each question can be answered only once.");

        var questions = attempt.Quiz.Questions.ToDictionary(q => q.Id);
        var score = 0;
        var answers = new List<Answer>();

        foreach (var item in dto.Answers)
        {
            if (!questions.TryGetValue(item.QuestionId, out var question))
                throw new BadRequestException($"Question {item.QuestionId} does not belong to this quiz.");

            var option = question.Options.FirstOrDefault(o => o.Id == item.SelectedOptionId)
                ?? throw new BadRequestException(
                    $"Option {item.SelectedOptionId} does not belong to question {item.QuestionId}.");

            answers.Add(new Answer
            {
                AttemptId = attempt.Id,
                QuestionId = question.Id,
                SelectedOptionId = option.Id
            });

            if (option.IsCorrect) score += question.Points;
        }

        _db.Answers.AddRange(answers);
        attempt.SubmittedAt = now;
        attempt.Score = score;
        await _db.SaveChangesAsync();

        return await LoadResultAsync(attemptId);
    }

    // ---------- عرض محاولة (الطالب بتاعها أو المدرس صاحب الكويز) ----------
    public async Task<AttemptResultDto> GetAsync(int quizId, int attemptId, string userId, bool isTeacher)
    {
        var attempt = await _db.Attempts.AsNoTracking()
            .Include(a => a.Quiz)
            .FirstOrDefaultAsync(a => a.Id == attemptId && a.QuizId == quizId)
            ?? throw new NotFoundException("Attempt not found.");

        var allowed = isTeacher ? attempt.Quiz.TeacherId == userId : attempt.StudentId == userId;
        if (!allowed)
            throw new ForbiddenException("You cannot view this attempt.");

        return await LoadResultAsync(attemptId);
    }

    // ---------- المدرس يشوف كل المحاولات ----------
    public async Task<PagedResult<AttemptSummaryDto>> GetAllForQuizAsync(
        int quizId, string teacherId, int page, int pageSize)
    {
        await EnsureTeacherOwnsQuizAsync(quizId, teacherId);

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var query = _db.Attempts.AsNoTracking().Where(a => a.QuizId == quizId);
        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(a => a.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AttemptSummaryDto
            {
                AttemptId = a.Id,
                StudentId = a.StudentId,
                StudentName = a.Student.FullName,
                StartedAt = a.StartedAt,
                SubmittedAt = a.SubmittedAt,
                Score = a.Score
            })
            .ToListAsync();

        return new PagedResult<AttemptSummaryDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    // ---------- الإحصائيات (LINQ GroupBy + Aggregation) ----------
    public async Task<QuizStatisticsDto> GetStatisticsAsync(int quizId, string teacherId)
    {
        await EnsureTeacherOwnsQuizAsync(quizId, teacherId);

        var submitted = _db.Attempts.AsNoTracking()
            .Where(a => a.QuizId == quizId && a.SubmittedAt != null);

        var scores = await submitted.Select(a => a.Score!.Value).ToListAsync();

        // لكل سؤال: كام طالب جاوب صح
        var correctPerQuestion = await _db.Answers.AsNoTracking()
            .Where(a => a.Attempt.QuizId == quizId && a.Attempt.SubmittedAt != null)
            .GroupBy(a => a.QuestionId)
            .Select(g => new
            {
                QuestionId = g.Key,
                Correct = g.Sum(x => x.SelectedOption.IsCorrect ? 1 : 0)
            })
            .ToDictionaryAsync(x => x.QuestionId, x => x.Correct);

        var questions = await _db.Questions.AsNoTracking()
            .Where(q => q.QuizId == quizId)
            .OrderBy(q => q.Id)
            .Select(q => new { q.Id, q.Text })
            .ToListAsync();

        var count = scores.Count;

        return new QuizStatisticsDto
        {
            QuizId = quizId,
            AttemptCount = count,
            AverageScore = count == 0 ? 0 : Math.Round(scores.Average(), 2),
            HighestScore = count == 0 ? 0 : scores.Max(),
            LowestScore = count == 0 ? 0 : scores.Min(),
            Questions = questions.Select(q =>
            {
                var correct = correctPerQuestion.GetValueOrDefault(q.Id);
                return new QuestionStatDto
                {
                    QuestionId = q.Id,
                    Text = q.Text,
                    CorrectCount = correct,
                    CorrectPercentage = count == 0 ? 0 : Math.Round(correct * 100.0 / count, 2)
                };
            }).ToList()
        };
    }

    // ---------- Helpers ----------

    private async Task EnsureTeacherOwnsQuizAsync(int quizId, string teacherId)
    {
        var quiz = await _db.Quizzes.AsNoTracking()
            .Select(q => new { q.Id, q.TeacherId })
            .FirstOrDefaultAsync(q => q.Id == quizId)
            ?? throw new NotFoundException("Quiz not found.");

        if (quiz.TeacherId != teacherId)
            throw new ForbiddenException("You do not own this quiz.");
    }

    private async Task<AttemptResultDto> LoadResultAsync(int attemptId)
    {
        var a = await _db.Attempts.AsNoTracking()
            .Include(x => x.Student)
            .Include(x => x.Answers)
            .Include(x => x.Quiz).ThenInclude(q => q.Questions).ThenInclude(q => q.Options)
            .AsSplitQuery()
            .FirstAsync(x => x.Id == attemptId);

        var questions = a.Quiz.Questions.ToDictionary(q => q.Id);

        return new AttemptResultDto
        {
            AttemptId = a.Id,
            QuizId = a.QuizId,
            StudentId = a.StudentId,
            StudentName = a.Student.FullName,
            StartedAt = a.StartedAt,
            SubmittedAt = a.SubmittedAt,
            Score = a.Score,
            TotalPoints = a.Quiz.Questions.Sum(q => q.Points),
            Answers = a.Answers.Select(ans =>
            {
                var q = questions[ans.QuestionId];
                var correct = q.Options.Any(o => o.Id == ans.SelectedOptionId && o.IsCorrect);
                return new AnswerResultDto
                {
                    QuestionId = ans.QuestionId,
                    SelectedOptionId = ans.SelectedOptionId,
                    IsCorrect = correct,
                    PointsEarned = correct ? q.Points : 0
                };
            }).ToList()
        };
    }

    // SQL Server بيرجّع التواريخ Unspecified، فبنعتبرها UTC
    private static DateTime AsUtc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 };
}
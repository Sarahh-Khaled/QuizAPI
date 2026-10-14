using System.ComponentModel.DataAnnotations;

namespace QuizAPI.Dtos;

public class CreateQuizDto
{
    [Required, MaxLength(200)] public string Title { get; set; } = string.Empty;
    [MaxLength(1000)] public string? Description { get; set; }
    [Range(1, 600)] public int DurationMinutes { get; set; }
    public DateTime Deadline { get; set; }
}

public class UpdateQuizDto : CreateQuizDto
{
    public bool IsPublished { get; set; }
}

public class OptionInputDto
{
    [Required] public string Text { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
}

public class QuestionInputDto
{
    [Required] public string Text { get; set; } = string.Empty;
    [Range(1, 100)] public int Points { get; set; } = 1;
    [Required] public List<OptionInputDto> Options { get; set; } = new();
}

public class QuizSummaryDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DurationMinutes { get; set; }
    public DateTime Deadline { get; set; }
    public bool IsPublished { get; set; }
    public string TeacherId { get; set; } = string.Empty;
    public string TeacherName { get; set; } = string.Empty;
    public int QuestionsCount { get; set; }
}

public class TeacherOptionDto
{
    public int Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
}

public class TeacherQuestionDto
{
    public int Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public int Points { get; set; }
    public List<TeacherOptionDto> Options { get; set; } = new();
}

public class TeacherQuizDetailsDto : QuizSummaryDto
{
    public List<TeacherQuestionDto> Questions { get; set; } = new();
}

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}

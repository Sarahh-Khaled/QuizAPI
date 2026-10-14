using System.ComponentModel.DataAnnotations;

namespace QuizAPI.Dtos;

// ---- اللي الطالب بيستلمه (من غير IsCorrect) ----
public class StudentOptionDto
{
    public int Id { get; set; }
    public string Text { get; set; } = string.Empty;
}

public class StudentQuestionDto
{
    public int Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public int Points { get; set; }
    public List<StudentOptionDto> Options { get; set; } = new();
}

public class StartAttemptResponseDto
{
    public int AttemptId { get; set; }
    public int QuizId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public List<StudentQuestionDto> Questions { get; set; } = new();
}

// ---- اللي الطالب بيبعته ----
public class SubmitAnswerItemDto
{
    public int QuestionId { get; set; }
    public int SelectedOptionId { get; set; }
}

public class SubmitAnswersDto
{
    [Required] public List<SubmitAnswerItemDto> Answers { get; set; } = new();
}

// ---- النتيجة ----
public class AnswerResultDto
{
    public int QuestionId { get; set; }
    public int SelectedOptionId { get; set; }
    public bool IsCorrect { get; set; }
    public int PointsEarned { get; set; }
}

public class AttemptResultDto
{
    public int AttemptId { get; set; }
    public int QuizId { get; set; }
    public string StudentId { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public int? Score { get; set; }
    public int TotalPoints { get; set; }
    public List<AnswerResultDto> Answers { get; set; } = new();
}

public class AttemptSummaryDto
{
    public int AttemptId { get; set; }
    public string StudentId { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public int? Score { get; set; }
}

// ---- الإحصائيات ----
public class QuestionStatDto
{
    public int QuestionId { get; set; }
    public string Text { get; set; } = string.Empty;
    public int CorrectCount { get; set; }
    public double CorrectPercentage { get; set; }
}

public class QuizStatisticsDto
{
    public int QuizId { get; set; }
    public int AttemptCount { get; set; }
    public double AverageScore { get; set; }
    public int HighestScore { get; set; }
    public int LowestScore { get; set; }
    public List<QuestionStatDto> Questions { get; set; } = new();
}
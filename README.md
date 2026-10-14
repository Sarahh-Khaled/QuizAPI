# Quiz and Grading API

A RESTful back-end built with **ASP.NET Core Web API** where teachers create multiple-choice quizzes and students take them. The API grades each attempt automatically and gives teachers per-quiz statistics.

## Features

- JWT authentication with two roles: **Teacher** and **Student**
- Teachers create quizzes, add multiple-choice questions, and publish them
- Students start one attempt per quiz, submit answers, and get their score
- Teachers view all attempts and statistics for their own quizzes
- Pagination and search on the quiz list
- Centralized error handling with consistent JSON responses
- Interactive API documentation with Swagger

## Tech Stack

- .NET 10, ASP.NET Core Web API (controllers)
- Entity Framework Core with SQL Server
- ASP.NET Core Identity (users, roles, password hashing)
- JWT Bearer authentication
- Swagger (Swashbuckle)

## Project Structure

```
QuizAPI/
├── Controllers/   # Thin HTTP layer: routing and authorization
├── Services/      # Business rules (QuizService, AttemptService, TokenService)
├── Entities/      # Database models
├── Data/          # AppDbContext and relationship configuration
├── Dtos/          # Request/response models
└── Middleware/    # Custom exceptions and global exception handling
```

Controllers contain no business logic. Services enforce the rules and throw typed exceptions, and one middleware turns them into HTTP responses.

## Data Model

`Quiz` → many `Question` → many `Option`
`Quiz` → many `Attempt` → many `Answer`
`Quiz.TeacherId` and `Attempt.StudentId` point to the user.

| Entity | Fields |
|---|---|
| Quiz | Title, Description, DurationMinutes, Deadline, IsPublished, TeacherId |
| Question | QuizId, Text, Points |
| Option | QuestionId, Text, IsCorrect |
| Attempt | QuizId, StudentId, StartedAt, SubmittedAt, Score |
| Answer | AttemptId, QuestionId, SelectedOptionId |

## Business Rules

| Rule | Implementation | Status |
|---|---|---|
| Only the owner can change a quiz, and not after it is published | Ownership and publish check in one shared method used by every write operation | 403 / 409 |
| Every question has exactly one correct option | Validated when adding/updating a question and again before publishing | 400 |
| One attempt per student per quiz | Unique index on (QuizId, StudentId), plus a pre-check and handling of the database error for double clicks | 409 |
| No attempts after the deadline | Checked when an attempt starts | 400 |
| Answers must arrive within the duration | StartedAt + DurationMinutes is checked on submit | 400 |
| Students never see `IsCorrect` before submitting | Separate student DTOs that do not contain the field | n/a |
| Score is the sum of points of correct answers | Calculated on the server from the stored options, never from client input | n/a |

## Endpoints

| Method | Route | Who |
|---|---|---|
| POST | `/api/auth/register`, `/api/auth/login` | Anyone |
| GET | `/api/quizzes?page=1&pageSize=10&search=algebra` | Teacher (own quizzes), Student (published) |
| POST | `/api/quizzes` | Teacher |
| GET | `/api/quizzes/{quizId}` | Teacher (own, with questions), Student (published) |
| PUT, DELETE | `/api/quizzes/{quizId}` | Teacher |
| POST | `/api/quizzes/{quizId}/questions` | Teacher |
| PUT, DELETE | `/api/quizzes/{quizId}/questions/{questionId}` | Teacher |
| POST | `/api/quizzes/{quizId}/attempts` | Student (starts the attempt) |
| POST | `/api/quizzes/{quizId}/attempts/{attemptId}/answers` | Student (submits and gets the score) |
| GET | `/api/quizzes/{quizId}/attempts/{attemptId}` | Student (own) or Teacher (quiz owner) |
| GET | `/api/quizzes/{quizId}/attempts` | Teacher |
| GET | `/api/quizzes/{quizId}/statistics` | Teacher |

The statistics endpoint returns the attempt count, average, highest and lowest score, and the percentage of students who answered each question correctly (LINQ `GroupBy` with aggregation).

## Getting Started

**Prerequisites:** .NET 10 SDK, SQL Server or LocalDB, and the EF Core CLI (`dotnet tool install --global dotnet-ef`).

```bash
git clone git clone https://github.com/Sarahh-Khaled/QuizAPI.git
cd QuizAPI
```

1. Set the connection string in `appsettings.json` (the default points to LocalDB).
2. Set the JWT secret (at least 32 characters). Do not commit a real key:

```bash
dotnet user-secrets init
dotnet user-secrets set "Jwt:Key" "long-random-secret"
```

3. Create the database and run the API:

```bash
dotnet ef database update
dotnet run
```

4. Open `https://localhost:7087/swagger`.

The Teacher and Student roles are created automatically on startup.

## Trying it in Swagger

1. Register a teacher and a student (`role`: `Teacher` or `Student`).
2. Log in and copy the `token`.
3. Click **Authorize** and paste the token only (no `Bearer ` prefix).
4. As the teacher: create a quiz, add questions, then update it with `isPublished: true`.
5. As the student: start an attempt, then submit the answers.
6. As the teacher: open the attempts list and the statistics.

## Design Decisions

- **ASP.NET Identity** for users: password hashing with a salt, password policy, and roles come ready and well tested instead of being hand written.
- **Role chosen at registration**, limited to Teacher or Student.
- **Publishing through `isPublished` on the quiz update**, since the task defines no separate publish endpoint. Publishing is one-way, so a quiz with attempts can never be edited or deleted.
- **403 for a teacher who does not own the quiz, 404 for an unpublished quiz seen by a student**, so existence is not leaked.
- **Deadlines stored in UTC** to avoid time zone mismatches.
- **Delete rules:** questions, options, and attempts cascade from their parent. User relations and answer-to-question/option are restricted, which also avoids SQL Server multiple cascade path errors.

## Author

Sara Khaled · [[LinkedIn]](https://www.linkedin.com/in/sara-khaled-6142641b3/) · Sarakhaled.slem@gamil.com

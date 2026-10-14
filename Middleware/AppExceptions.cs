namespace QuizAPI.Middleware;

public abstract class AppException   : Exception
{
    public int StatusCode { get; }
    protected AppException(int statusCode, string message) : base(message) => StatusCode = statusCode;
}

public class BadRequestException(string message) : AppException(400, message);
public class ForbiddenException(string message) : AppException(403, message);
public class NotFoundException(string message) : AppException(404, message);
public class ConflictException(string message) : AppException(409, message);

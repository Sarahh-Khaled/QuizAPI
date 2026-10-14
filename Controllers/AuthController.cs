using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using QuizAPI.Dtos;
using QuizAPI.Entities;
using QuizAPI.Services;

namespace QuizAPI.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly TokenService _tokenService;

    public AuthController(UserManager<ApplicationUser> userManager, TokenService tokenService)
    {
        _userManager = userManager;
        _tokenService = tokenService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterDto dto)
    {
        if (dto.Role != Roles.Teacher && dto.Role != Roles.Student)
            return BadRequest(new { message = "Role must be Teacher or Student." });

        var user = new ApplicationUser
        {
            UserName = dto.Email,
            Email = dto.Email,
            FullName = dto.FullName
        };

        var result = await _userManager.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

        await _userManager.AddToRoleAsync(user, dto.Role);
        return Ok(BuildResponse(user, dto.Role));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user is null || !await _userManager.CheckPasswordAsync(user, dto.Password))
            return Unauthorized(new { message = "Invalid email or password." });

        var role = (await _userManager.GetRolesAsync(user)).First();
        return Ok(BuildResponse(user, role));
    }

    private AuthResponseDto BuildResponse(ApplicationUser user, string role)
    {
        var (token, expires) = _tokenService.CreateToken(user, role);
        return new AuthResponseDto
        {
            Token = token,
            ExpiresAt = expires,
            Email = user.Email!,
            FullName = user.FullName,
            Role = role
        };
    }
}

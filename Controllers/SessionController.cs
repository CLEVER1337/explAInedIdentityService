using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

public class SessionController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ILogger<SessionController> _logger;
    private readonly TokenService _tokenService;

    public SessionController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ILogger<SessionController> logger,
        TokenService tokenService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _logger = logger;
        _tokenService = tokenService;
    }

    [Route("session")]
    [HttpPost]
    public async Task<IActionResult> Login([FromBody] LoginViewModel model)
    {
        var result = await _signInManager.PasswordSignInAsync(
            model.Email,
            model.Password,
            model.RememberMe,
            lockoutOnFailure: false);

        if (result.Succeeded)
        {
            _logger.LogInformation("User logged in.");
            var accessToken = _tokenService.GenerateAccessToken(await _userManager.FindByNameAsync(model.Email));
            var refreshToken = _tokenService.GenerateRefreshToken(await _userManager.FindByNameAsync(model.Email));
            return Ok(new { accessToken, refreshToken });
        }

        return Unauthorized(new { message = "Invalid login attempt." });
    }

    [Route("session/refresh")]
    [Authorize]
    [HttpPost]
    public async Task<IActionResult> RefreshToken()
    {
        try
        {
            var refreshToken = HttpContext.Request.Headers["Authorization"].FirstOrDefault()?.Split(' ')[1];

            var refreshTask = _tokenService.RefreshTokens(refreshToken);

            var blackListTask = _tokenService.BlackListToken(refreshToken);

            Task.WaitAll(refreshTask, blackListTask);

            return Ok(new { accessToken = refreshTask.Result.accessToken, refreshToken = refreshTask.Result.refreshToken });
        }
        catch (SecurityTokenException ex)
        {
            _logger.LogError(ex, "Invalid token.");
            return BadRequest(new { message = "Invalid token." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing token.");
            return StatusCode(500, new { message = "An error occurred while refreshing the token." });
        }
    }

    [Route("session")]
    [Authorize]
    [HttpDelete]
    public async Task<IActionResult> Logout()
    {
        try
        {
            _logger.LogInformation(_userManager.GetUserAsync(User)?.Result?.Nickname ?? "No user");
            // _logger.LogInformation(_signInManager.IsSignedIn(User).ToString());
            // await _signInManager.SignOutAsync();
            _logger.LogInformation(HttpContext.Request.Headers["Authorization"].FirstOrDefault()!.Split(' ')[1]);
            await _tokenService.BlackListToken(HttpContext.Request.Headers["Authorization"].FirstOrDefault()!.Split(' ')[1]);
            _logger.LogInformation("User logged out.");
            return Ok(new { message = "Logged out." });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError($"{ex.Message}");
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout.");
            return StatusCode(500, new { message = "An error occurred during logout." });
        }
    }
}
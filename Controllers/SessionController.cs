using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

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

    [Route("session")]
    [Authorize]
    [HttpDelete]
    public async Task<IActionResult> Logout()
    {
        _logger.LogInformation(_userManager.GetUserAsync(User)?.Result?.Nickname ?? "No user");
        // _logger.LogInformation(_signInManager.IsSignedIn(User).ToString());
        // await _signInManager.SignOutAsync();
        _logger.LogInformation(HttpContext.Request.Headers["Authorization"].FirstOrDefault().Split(' ')[1]);
        _tokenService.BlackListToken(HttpContext.Request.Headers["Authorization"].FirstOrDefault().Split(' ')[1]);
        _logger.LogInformation("User logged out.");
        return Ok(new { message = "Logged out." });
    }
}
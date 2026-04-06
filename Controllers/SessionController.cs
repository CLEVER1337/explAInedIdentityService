using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

public class SessionController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ILogger<SessionController> _logger;

    public SessionController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ILogger<SessionController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _logger = logger;
    }

    [Route("session")]
    [HttpPost]
    public async Task<IActionResult> Login([FromBody] LoginViewModel model)
    {
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

        var result = await _signInManager.PasswordSignInAsync(
            model.Email,
            model.Password,
            model.RememberMe,
            lockoutOnFailure: false);

        if (result.Succeeded)
        {
            _logger.LogInformation("User logged in.");
            return Ok(new { success = true });
        }

        return Unauthorized(new { message = "Invalid login attempt." });
    }

    [Route("session")]
    [HttpDelete]
    public async Task<IActionResult> Logout()
    {
        _logger.LogInformation(_userManager.GetUserAsync(User)?.Result?.Nickname ?? "No user");
        _logger.LogInformation(_signInManager.IsSignedIn(User).ToString());
        await _signInManager.SignOutAsync();
        _logger.LogInformation("User logged out.");
        return Ok(new { message = "Logged out." });
    }
}
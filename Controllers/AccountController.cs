using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ILogger<AccountController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Register([FromBody] RegisterViewModel model)
    {
        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            Nickname = model.Nickname,
        };

        var result = await _userManager.CreateAsync(user, model.Password);

        if (result.Succeeded)
        {
            await _signInManager.SignInAsync(user, isPersistent: false);
            _logger.LogInformation("User created a new account with password.");
            return CreatedAtAction(nameof(Register), new { id = user.Id, email = user.Email });
        }

        foreach (var error in result.Errors)
            ModelState.AddModelError(string.Empty, error.Description);

        return BadRequest();
    }

    // [HttpPost]
    // public async Task<IActionResult> Login([FromBody] LoginViewModel model)
    // {
    //     await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

    //     var result = await _signInManager.PasswordSignInAsync(
    //         model.Email,
    //         model.Password,
    //         model.RememberMe,
    //         lockoutOnFailure: false);

    //     if (result.Succeeded)
    //     {
    //         _logger.LogInformation("User logged in.");
    //         return Ok(new { success = true });
    //     }

    //     return Unauthorized(new { message = "Invalid login attempt." });
    // }

    // [HttpPost]
    // public async Task<IActionResult> Logout()
    // {
    //     _logger.LogInformation(_userManager.GetUserAsync(User)?.Result?.Nickname ?? "No user");
    //     _logger.LogInformation(_signInManager.IsSignedIn(User).ToString());
    //     await _signInManager.SignOutAsync();
    //     _logger.LogInformation("User logged out.");
    //     return Ok(new { message = "Logged out." });
    // }
}

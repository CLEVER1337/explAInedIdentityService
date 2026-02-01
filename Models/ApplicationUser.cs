using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

public class ApplicationUser : IdentityUser
{
    [Required]
    [PersonalData]
    public string Nickname { get; set; } = "";

    [Required]
    [PersonalData]
    public string Email { get; set; } = "";
}
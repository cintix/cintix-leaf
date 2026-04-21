namespace Leaf.Web.UI.Models.Auth;

public sealed class LoginViewModel
{
    public string Email { get; set; } = "admin@leaf.local";
    public string Password { get; set; } = "leafadmin";
    public bool Remember { get; set; } = true;
    public string Error { get; set; } = string.Empty;
}

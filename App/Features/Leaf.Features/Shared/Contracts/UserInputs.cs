namespace Leaf.Features.Shared.Contracts;

public sealed record CreateUserInput(string DisplayName, string Email, string Password, bool IsAdmin);
public sealed record UpdateUserInput(int Id, string DisplayName, string Email, bool IsActive, bool IsAdmin);

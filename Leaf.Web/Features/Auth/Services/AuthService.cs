using Leaf.Web.Core.Primitives;
using Leaf.Web.Core.Security;
using Leaf.Web.Core.Time;
using Leaf.Web.Features.Shared;
using Leaf.Web.Features.Shared.Contracts;
using Leaf.Web.Features.Shared.Models;

namespace Leaf.Web.Features.Auth.Services;

public sealed class AuthService(
    ILeafRepository repository,
    IPasswordHasher passwordHasher,
    IClock clock) : IAuthService
{
    public async Task<Result<User>> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return Result<User>.Failure(Error.Validation("Email and password are required."));
        }

        var user = await repository.GetUserByEmailAsync(email.Trim(), ct);
        if (user is null || !user.IsActive)
        {
            return Result<User>.Failure(Error.Unauthorized("Invalid credentials."));
        }

        if (!passwordHasher.Verify(password, user.PasswordHash))
        {
            return Result<User>.Failure(Error.Unauthorized("Invalid credentials."));
        }

        return Result<User>.Success(user);
    }

    public async Task<Result<int>> RegisterAsync(CreateUserInput input, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(input.DisplayName) || string.IsNullOrWhiteSpace(input.Email) || string.IsNullOrWhiteSpace(input.Password))
        {
            return Result<int>.Failure(Error.Validation("Display name, email and password are required."));
        }

        var existing = await repository.GetUserByEmailAsync(input.Email.Trim(), ct);
        if (existing is not null)
        {
            return Result<int>.Failure(Error.Conflict("Email is already in use."));
        }

        var user = new User
        {
            DisplayName = input.DisplayName.Trim(),
            Email = input.Email.Trim(),
            PasswordHash = passwordHasher.Hash(input.Password),
            IsAdmin = input.IsAdmin,
            IsActive = true,
            CreatedUtc = clock.UtcNow
        };

        var id = await repository.CreateUserAsync(user, ct);
        return Result<int>.Success(id);
    }
}

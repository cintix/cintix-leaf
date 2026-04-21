using Leaf.Web.Core.Primitives;
using Leaf.Web.Features.Shared.Contracts;
using Leaf.Web.Features.Shared.Models;

namespace Leaf.Web.Features.Auth;

public interface IAuthService
{
    Task<Result<User>> LoginAsync(string email, string password, CancellationToken ct = default);
    Task<Result<int>> RegisterAsync(CreateUserInput input, CancellationToken ct = default);
}

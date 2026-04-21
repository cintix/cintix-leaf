using Microsoft.AspNetCore.Mvc;

namespace Leaf.Web.Infrastructure;

public static class HttpContextExtensions
{
    public static int? CurrentUserId(this HttpContext context)
    {
        var id = context.Session.GetInt32(SessionKeys.UserId);
        return id is > 0 ? id : null;
    }

    public static IActionResult ToErrorJson(this ControllerBase controller, string message, int statusCode = StatusCodes.Status400BadRequest)
        => controller.StatusCode(statusCode, new { error = message });
}

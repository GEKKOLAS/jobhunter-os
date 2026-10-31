using JobHunter.Application.Identity;
using Microsoft.AspNetCore.Mvc;

namespace JobHunter.Api.Controllers;

[ApiController]
[Route("api/v1/me")]
public sealed class MeController(MeHandler handler) : ControllerBase
{
    public sealed record UpdateMeRequest(string DisplayName);
    public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

    [HttpGet]
    public async Task<UserDto> Get(CancellationToken ct) => await handler.GetAsync(ct);

    [HttpPatch]
    public async Task<UserDto> Update(UpdateMeRequest request, CancellationToken ct) =>
        await handler.UpdateAsync(new UpdateMeCommand(request.DisplayName), ct);

    [HttpPost("password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        await handler.ChangePasswordAsync(new ChangePasswordCommand(request.CurrentPassword, request.NewPassword), ct);
        return NoContent();
    }
}

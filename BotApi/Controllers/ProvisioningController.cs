using Microsoft.AspNetCore.Mvc;
using BotApi.Services;
using BotApi.Models;

namespace BotApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProvisioningController : ControllerBase
{
    private readonly UserProvisioningService _provisioning;

    public ProvisioningController(UserProvisioningService provisioning)
    {
        _provisioning = provisioning;
    }

    /// <summary>
    /// Provisioniert einen Benutzer in Authentik + Gruppe + Matrix.
    /// </summary>
    [HttpPost("{username}")]
    public async Task<IActionResult> ProvisionUser(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return BadRequest("Username darf nicht leer sein.");

        try
        {
            await _provisioning.EnsureUserProvisionedAsync(username);
            return Ok(new
            {
                message = $"Benutzer '{username}' wurde erfolgreich provisioniert."
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                error = "Provisionierung fehlgeschlagen"+ex.Message,
                details = ex.Message
            });
        }
    }
}
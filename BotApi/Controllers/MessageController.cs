using BotApi.Models;
using BotApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace BotApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MessageController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly MessageService _messageService;

    public MessageController(IConfiguration config, MessageService messageService)
    {
        _config = config;
        _messageService = messageService;
    }

    [HttpPost("send")]
    public async Task<IActionResult> SendMessage([FromBody] MessageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest("Message missing");
        
        var password = Request.Headers["X-Admin-Password"].ToString();
        var correctPassword = _config["AdminPassword"];

        if (password != correctPassword)
            return Unauthorized();

        var result = await _messageService.SendToYouthAsync(request.Message);

        return Ok(new { status = "sent", result });
    }
}
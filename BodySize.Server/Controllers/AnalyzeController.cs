using Microsoft.AspNetCore.Mvc;

namespace BodySize.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AnalyzeController : ControllerBase
{
    /// <summary>
    /// Binds multipart fields explicitly. Also accepts height/heightCm via querystring fallback.
    /// </summary>
    [HttpPost("analyze")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Analyze(
        [FromForm(Name = "heightCm")] int heightCm,
        [FromForm(Name = "gender")] string gender,
        [FromForm(Name = "front")] IFormFile front,
        [FromForm(Name = "back")] IFormFile back,
        [FromForm(Name = "left")] IFormFile left,
        [FromForm(Name = "right")] IFormFile right)
    {
        // Fallbacks in case the binder missed height for any reason
        if (heightCm <= 0)
        {
            if (int.TryParse(Request.Form["height"], out var h1)) heightCm = h1;
            else if (int.TryParse(Request.Query["heightCm"], out var h2)) heightCm = h2;
            else if (int.TryParse(Request.Query["height"], out var h3)) heightCm = h3;
        }

        if (heightCm <= 0)
            return BadRequest("\"heightCm must be provided and > 0\"");

        if (front is null || back is null || left is null || right is null)
            return BadRequest("All four images must be provided.");

        // TODO: Replace this stub with your actual measurement pipeline.
        // For now, return plausible numbers to prove end-to-end works.
        var result = new
        {
            chest = 98.0,
            waist = 87.5,
            hips = 101.5,
            shoulders = 71.8,
            torsoLength = 56.0,
            topSize = "M",
            bottomSize = "M"
        };

        // Simulate async I/O (e.g., saving/processing); remove if not needed
        await Task.CompletedTask;

        return Ok(result);
    }
}

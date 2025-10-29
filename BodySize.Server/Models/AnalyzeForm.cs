using Microsoft.AspNetCore.Http;

namespace BodySize.Server.Models;

public class AnalyzeForm
{
    public double? HeightCm { get; set; }
    public string? Gender { get; set; }

    public IFormFile? Front { get; set; }
    public IFormFile? Back { get; set; }
    public IFormFile? Left { get; set; }
    public IFormFile? Right { get; set; }
}

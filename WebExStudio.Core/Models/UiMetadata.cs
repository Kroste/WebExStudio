namespace WebExStudio.Core.Models;

public sealed class UiMetadata
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 200;
    public double Height { get; set; } = 60;
}

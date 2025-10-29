namespace BodySize.Server.Models;

public class MeasurementResult
{
    public double ChestCm { get; set; }
    public double WaistCm { get; set; }
    public double HipsCm { get; set; }
    public double ShoulderWidthCm { get; set; }
    public double TorsoLengthCm { get; set; }

    public string TopEu { get; set; } = "";
    public string BottomEu { get; set; } = "";

    public string Debug { get; set; } = "";
}

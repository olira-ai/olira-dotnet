using Olira;

namespace Olira.Tests;

public class SignalsParquetTests
{
    [Fact]
    public void SerializeSignalRecords_WritesNonEmptyParquet()
    {
        var t0 = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var records = new List<Dictionary<string, object?>>();
        for (var i = 0; i < 20; i++)
        {
            records.Add(new Dictionary<string, object?>
            {
                ["ts"] = t0.AddMilliseconds(i * 50),
                ["x"] = 0.0,
                ["y"] = 0.0,
                ["z"] = 9.81,
            });
        }

        var blob = Signals.SerializeSignalRecords(records);
        Assert.True(blob.Length > 100);
        // Parquet magic / PAR1 footer
        Assert.Equal((byte)'P', blob[^4]);
        Assert.Equal((byte)'A', blob[^3]);
        Assert.Equal((byte)'R', blob[^2]);
        Assert.Equal((byte)'1', blob[^1]);
    }

    [Fact]
    public void SerializeSignalRecords_AcceptsDateTimeOffset()
    {
        var t0 = DateTimeOffset.Parse("2026-01-01T12:00:00Z");
        var records = new List<Dictionary<string, object?>>
        {
            new()
            {
                ["ts"] = t0,
                ["x"] = 1.0,
                ["y"] = 2.0,
                ["z"] = 3.0,
            },
        };

        var blob = Signals.SerializeSignalRecords(records);
        Assert.True(blob.Length > 50);
    }

    [Fact]
    public void SerializeSignalRecords_Empty_ThrowsValidationError()
    {
        Assert.Throws<ValidationError>(() => Signals.SerializeSignalRecords([]));
    }
}

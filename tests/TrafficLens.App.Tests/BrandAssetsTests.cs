using System.Drawing;
using System.IO;

namespace TrafficLens.App.Tests;

public class BrandAssetsTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "TrafficLens.sln")))
        {
            dir = dir.Parent;
        }

        if (dir is null)
        {
            throw new InvalidOperationException("Test run outside a TrafficLens checkout");
        }

        return dir.FullName;
    }

    private static string BrandingPath(params string[] segments) =>
        Path.Combine(new[] { RepoRoot(), "assets", "branding" }.Concat(segments).ToArray());

    [Fact]
    public void BrandIcon_ExistsAsValidMultiSizeIco()
    {
        var iconPath = BrandingPath("TrafficLens.ico");

        Assert.True(File.Exists(iconPath), $"Missing {iconPath}");
        var bytes = File.ReadAllBytes(iconPath);
        Assert.True(bytes.Length > 1000, "ICO should contain embedded PNG image data");

        using var reader = new BinaryReader(new MemoryStream(bytes));
        Assert.Equal((ushort)0, reader.ReadUInt16());
        Assert.Equal((ushort)1, reader.ReadUInt16());
        Assert.Equal((ushort)7, reader.ReadUInt16());

        using var icon = new Icon(iconPath, 32, 32);
        using var bitmap = icon.ToBitmap();
        Assert.Equal(32, bitmap.Width);
        Assert.Equal(32, bitmap.Height);
    }

    [Fact]
    public void BrandPngs_ExistAtExpectedSizes()
    {
        foreach (var (name, expected) in new[] { ("TrafficLens-256.png", 256), ("TrafficLens-128.png", 128) })
        {
            var pngPath = BrandingPath(name);
            Assert.True(File.Exists(pngPath), $"Missing {pngPath}");

            using var stream = File.OpenRead(pngPath);
            var header = new byte[8];
            Assert.Equal(8, stream.Read(header, 0, 8));
            Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, header);

            stream.Position = 0;
            using var image = Image.FromStream(stream);
            Assert.Equal(expected, image.Width);
            Assert.Equal(expected, image.Height);
        }
    }
}
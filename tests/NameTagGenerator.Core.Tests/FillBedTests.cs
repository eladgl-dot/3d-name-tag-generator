using NameTagGenerator.Items;

namespace NameTagGenerator.Tests;

/// <summary>Arrange "Fill the bed": tags packed tightly, turned 90° when that fits better, spilling onto more beds.</summary>
public class FillBedTests
{
    static ArrangeSettings Bed(double w = 256, double h = 256, double gap = 5) =>
        new() { Shape = ArrangeShape.FillBed, PlateWidth = w, PlateHeight = h, GapX = gap, GapY = gap };

    // Each tag's footprint on the bed, after turning, relative to its own bed's middle.
    static List<(int Bed, double MinX, double MinY, double MaxX, double MaxY)> Footprints(IReadOnlyList<(double W, double H)> sizes, ArrangeSettings s, ArrayResult r)
    {
        var list = new List<(int, double, double, double, double)>();
        for (var i = 0; i < sizes.Count; i++)
        {
            var (w, h) = r.Turned[i] ? (sizes[i].H, sizes[i].W) : sizes[i];
            var (bx, by) = ArrayLayout.BedCentre(r.Beds[i], s);
            var (cx, cy) = (r.Centers[i].X - bx, r.Centers[i].Y - by);
            list.Add((r.Beds[i], cx - w / 2, cy - h / 2, cx + w / 2, cy + h / 2));
        }
        return list;
    }

    static void AssertSound(IReadOnlyList<(double W, double H)> sizes, ArrangeSettings s, ArrayResult r)
    {
        var f = Footprints(sizes, s, r);
        foreach (var t in f)
        {
            Assert.InRange(t.MinX, -s.PlateWidth / 2 - 1e-6, s.PlateWidth / 2);
            Assert.InRange(t.MaxX, -s.PlateWidth / 2, s.PlateWidth / 2 + 1e-6);
            Assert.InRange(t.MinY, -s.PlateHeight / 2 - 1e-6, s.PlateHeight / 2);
            Assert.InRange(t.MaxY, -s.PlateHeight / 2, s.PlateHeight / 2 + 1e-6);
        }
        for (var i = 0; i < f.Count; i++)
            for (var j = i + 1; j < f.Count; j++)
            {
                if (f[i].Bed != f[j].Bed) continue;
                var apart = f[i].MaxX + s.GapX <= f[j].MinX + 1e-6 || f[j].MaxX + s.GapX <= f[i].MinX + 1e-6
                    || f[i].MaxY + s.GapY <= f[j].MinY + 1e-6 || f[j].MaxY + s.GapY <= f[i].MinY + 1e-6;
                Assert.True(apart, $"tags {i} and {j} overlap or are closer than the gap");
            }
    }

    [Fact]
    public void FillBed_IsAddedAtTheEnd_SoSavedShapesKeepTheirMeaning()
    {
        Assert.Equal(4, (int)ArrangeShape.FillBed);
    }

    [Fact]
    public void FortyCandles_FitTwoBeds_MostOnTheFirst()
    {
        var sizes = Enumerable.Repeat((19.0, 72.0), 40).ToList();
        var s = Bed();
        var r = ArrayLayout.Place(sizes, s);
        AssertSound(sizes, s, r);
        Assert.Equal(2, r.BedCount);
        Assert.True(r.Beds.Count(b => b == 0) >= 33, $"bed 1 has {r.Beds.Count(b => b == 0)}");
        Assert.True(r.FitsPlate);
    }

    [Fact]
    public void FewTags_StayOnOneBed_NotTurned()
    {
        var sizes = new List<(double, double)> { (60, 20), (50, 20), (70, 20) };
        var r = ArrayLayout.Place(sizes, Bed());
        Assert.Equal(1, r.BedCount);
        Assert.All(r.Beds, b => Assert.Equal(0, b));
        AssertSound(sizes, Bed(), r);
    }

    [Fact]
    public void TagLongerThanTheBedIsWide_IsTurned()
    {
        var sizes = new List<(double, double)> { (200, 20) };
        var s = Bed(w: 100, h: 250);
        var r = ArrayLayout.Place(sizes, s);
        Assert.True(r.Turned[0]);
        Assert.True(r.FitsPlate);
        AssertSound(sizes, s, r);
    }

    [Fact]
    public void LoneTag_OnAWideBed_IsNotTurned()
    {
        var sizes = new List<(double, double)> { (100, 20) };
        var s = Bed(w: 300, h: 200);
        var r = ArrayLayout.Place(sizes, s);
        Assert.False(r.Turned[0]);
        AssertSound(sizes, s, r);
    }

    [Fact]
    public void TurningPacksMore()
    {
        // 100 × 30 tags on a 245 × 100 bed: unturned only 6 fit (2 across, 3 down), turned 7 fit in a row.
        // 20 tags: 3 beds with turning, 4 without.
        var sizes = Enumerable.Repeat((100.0, 30.0), 20).ToList();
        var s = Bed(w: 245, h: 100);
        var r = ArrayLayout.Place(sizes, s);
        AssertSound(sizes, s, r);
        Assert.Equal(3, r.BedCount);
        Assert.Contains(true, r.Turned);
    }

    [Fact]
    public void MixedSizes_NoOverlaps()
    {
        var rng = new Random(7);
        var sizes = Enumerable.Range(0, 120).Select(_ => (10 + rng.NextDouble() * 80, 8 + rng.NextDouble() * 30)).ToList();
        var s = Bed();
        var r = ArrayLayout.Place(sizes, s);
        AssertSound(sizes, s, r);
        Assert.True(r.BedCount >= 2);
    }

    [Fact]
    public void TagBiggerThanTheBed_GetsItsOwnBed_AndIsReported()
    {
        var sizes = new List<(double, double)> { (20, 20), (300, 300), (20, 20) };
        var r = ArrayLayout.Place(sizes, Bed());
        Assert.False(r.FitsPlate);
        Assert.Equal(r.Beds[0], r.Beds[2]);
        Assert.NotEqual(r.Beds[0], r.Beds[1]);
    }

    [Fact]
    public void ListOrder_FillsTheFirstBedFirst()
    {
        var sizes = Enumerable.Repeat((19.0, 72.0), 40).ToList();
        var r = ArrayLayout.Place(sizes, Bed());
        Assert.All(r.Beds.Take(30), b => Assert.Equal(0, b));
        Assert.Equal(1, r.Beds[^1]);
    }

    [Fact]
    public void Beds_SitSideBySide()
    {
        var s = Bed();
        Assert.Equal((0.0, 0.0), ArrayLayout.BedCentre(0, s));
        var (x1, y1) = ArrayLayout.BedCentre(1, s);
        Assert.True(x1 >= 256 + 20);
        Assert.Equal(0, y1);
    }

    [Fact]
    public void OtherShapes_AreOneBed_NothingTurned()
    {
        var sizes = Enumerable.Repeat((19.0, 72.0), 40).ToList();
        var r = ArrayLayout.Place(sizes, new ArrangeSettings());
        Assert.Equal(1, r.BedCount);
        Assert.All(r.Beds, b => Assert.Equal(0, b));
        Assert.All(r.Turned, Assert.False);
    }
}

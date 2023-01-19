using RT.Serialization;

namespace RandomWallpaper;

class Settings : IClassifyObjectProcessor
{
    public List<string> Paths = new List<string>();
    public int SkipRecent = 40;
    public double OldBias = 1.5;
    public int MinimumTime = 10;

    public Dictionary<string, ImageInfo> Images = new Dictionary<string, ImageInfo>(StringComparer.OrdinalIgnoreCase);

    void IClassifyObjectProcessor.BeforeSerialize() { }

    void IClassifyObjectProcessor.AfterDeserialize()
    {
        foreach (var kvp in Images)
            kvp.Value.FileName = kvp.Key;
    }
}

class ImageInfo
{
    [ClassifyIgnore]
    public string FileName; // same as the dictionary key, for convenience
    [ClassifyIgnore]
    public double Probability;

    public double MoreOrLess = 0;
    public DateTime? Applied = null;
    public DateTime? Removed = null;
}

namespace PandaPlateWatch.Tests;

public class StateStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), $"panda-state-{Guid.NewGuid():N}");

    private string Path_ => Path.Combine(_directory, "nested", "state.json");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public void MissingFileMeansNothingRecorded() =>
        Assert.False(new FileStateStore(Path_).AlreadyNotified("2026-09-20"));

    [Fact]
    public void RecordThenRead()
    {
        var store = new FileStateStore(Path_);

        store.Record("2026-09-20");

        Assert.True(store.AlreadyNotified("2026-09-20"));
        Assert.False(store.AlreadyNotified("2026-09-21"));
    }

    [Fact]
    public void RecordCreatesMissingDirectories()
    {
        new FileStateStore(Path_).Record("2026-09-20");

        Assert.True(File.Exists(Path_));
    }

    [Fact]
    public void RecordingTwiceDoesNotDuplicate()
    {
        var store = new FileStateStore(Path_);

        store.Record("2026-09-20");
        store.Record("2026-09-20");

        Assert.Equal(2, File.ReadAllText(Path_).Split("2026-09-20").Length);
    }

    [Fact]
    public void OldEntriesAreTrimmed()
    {
        var store = new FileStateStore(Path_, keep: 3);

        foreach (var day in Enumerable.Range(1, 5))
        {
            store.Record($"2026-09-0{day}");
        }

        Assert.False(store.AlreadyNotified("2026-09-01"));
        Assert.False(store.AlreadyNotified("2026-09-02"));
        Assert.True(store.AlreadyNotified("2026-09-03"));
        Assert.True(store.AlreadyNotified("2026-09-05"));
    }

    [Fact]
    public void CorruptFileIsTreatedAsEmpty()
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path_)!);
        File.WriteAllText(Path_, "{ this is not json");
        var store = new FileStateStore(Path_);

        // A corrupt file must never swallow an alert...
        Assert.False(store.AlreadyNotified("2026-09-20"));
        // ...and must not stop the next write from fixing it.
        store.Record("2026-09-20");
        Assert.True(store.AlreadyNotified("2026-09-20"));
    }

    [Fact]
    public void NullStoreNeverRemembersAnything()
    {
        var store = NullStateStore.Instance;

        store.Record("2026-09-20");

        Assert.False(store.AlreadyNotified("2026-09-20"));
    }
}

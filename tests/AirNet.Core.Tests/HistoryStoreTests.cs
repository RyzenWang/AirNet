using AirNet.Core.Demo;
using AirNet.Core.History;

namespace AirNet.Core.Tests;

public sealed class HistoryStoreTests
{
    [Fact]
    public async Task RoundTripsAndClearsRecords()
    {
        var directory = Path.Combine(Path.GetTempPath(), "airnet-tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new HistoryStore(directory);
            await store.AppendAsync(DemoSnapshotFactory.CreateSnapshot(downloadMbps: 384.2, uploadMbps: 92.6));

            var records = await store.LoadAsync(5);

            var record = Assert.Single(records);
            Assert.Equal(384.2, record.DownMbps, 1);
            Assert.True(await store.CountAsync() == 1);

            await store.ClearAsync();
            Assert.Equal(0, await store.CountAsync());
            Assert.Empty(await store.LoadAsync(5));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}

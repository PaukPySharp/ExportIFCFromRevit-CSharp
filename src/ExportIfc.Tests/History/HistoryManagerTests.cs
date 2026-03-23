using ExportIfc.History;
using ExportIfc.Tests.TestInfrastructure;

using Xunit;

namespace ExportIfc.Tests.History;

/// <summary>
/// Проверяет обновление истории моделей без дублей и с корректным rollback по времени.
/// </summary>
public sealed class HistoryManagerTests
{
    [Fact]
    public void UpdateRecord_PrunesFutureRows_WhenModelTimeRollsBack()
    {
        var path = @"C:\Models\Rollback.rvt";

        var dt1 = new DateTime(2026, 3, 8, 10, 0, 0);
        var dt2 = new DateTime(2026, 3, 8, 11, 0, 0);
        var rollback = new DateTime(2026, 3, 8, 10, 30, 0);

        var history = HistoryManager.FromRows(
            [
                new HistoryRow(path, dt1),
                new HistoryRow(path, dt2)
            ]);

        var model = TestModelFactory.Create(path, rollback);

        // При rollback по mtime история должна отбросить будущие записи
        // и зафиксировать новое последнее известное состояние модели.
        history.UpdateRecord(model);

        var snapshot = history.GetRowsSnapshot();

        Assert.Equal(2, snapshot.Count);
        Assert.Contains(snapshot, x => x.Path == path && x.LastModifiedMinute == dt1);
        Assert.Contains(snapshot, x => x.Path == path && x.LastModifiedMinute == rollback);
        Assert.DoesNotContain(snapshot, x => x.Path == path && x.LastModifiedMinute == dt2);
        Assert.True(history.IsUpToDate(model));
    }

    [Fact]
    public void UpdateRecord_DoesNotDuplicateSameTimestamp()
    {
        var path = @"C:\Models\Stable.rvt";
        var dt = new DateTime(2026, 3, 8, 10, 0, 0);

        var history = HistoryManager.FromRows(
            [
                new HistoryRow(path, dt)
            ]);

        var model = TestModelFactory.Create(path, dt);

        history.UpdateRecord(model);

        var snapshot = history.GetRowsSnapshot();

        Assert.Single(snapshot);
        Assert.Equal(path, snapshot[0].Path);
        Assert.Equal(dt, snapshot[0].LastModifiedMinute);
    }
}

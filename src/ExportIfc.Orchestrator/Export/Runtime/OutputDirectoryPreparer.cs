using ExportIfc.Models;

namespace ExportIfc.Export.Runtime;

/// <summary>
/// Подготавливает выходные каталоги для моделей, реально дошедших до пакетного плана.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Создаёт каталоги выгрузки перед запуском или dry-run подготовкой пакетов.
/// 2. Убирает дублирование вызовов <see cref="Directory.CreateDirectory(string)"/> для повторяющихся путей.
/// 3. Работает только с уже отобранными моделями, чтобы не создавать каталоги для отсеянных направлений.
///
/// Контракты:
/// 1. Пустые и whitespace-пути игнорируются.
/// 2. Один и тот же каталог создаётся не более одного раза за вызов.
/// 3. Класс ничего не знает о правилах отбора моделей и получает уже готовый список.
/// </remarks>
internal sealed class OutputDirectoryPreparer
{
    /// <summary>
    /// Создаёт отсутствующие каталоги выгрузки для набора моделей.
    /// </summary>
    /// <param name="models">Модели, фактически вошедшие в batch-план.</param>
    public void EnsureFor(IEnumerable<RevitModel> models)
    {
        ArgumentNullException.ThrowIfNull(models);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var model in models)
        {
            EnsureDirectory(model.OutputDirMapping, seen);
            EnsureDirectory(model.OutputDirNoMap, seen);
        }
    }

    /// <summary>
    /// Создаёт каталог, если путь непустой и ещё не обрабатывался в рамках текущего вызова.
    /// </summary>
    /// <param name="path">Путь к каталогу выгрузки.</param>
    /// <param name="seen">Набор уже обработанных путей без учёта регистра.</param>
    private static void EnsureDirectory(string? path, HashSet<string> seen)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        if (!seen.Add(path))
            return;

        Directory.CreateDirectory(path);
    }
}
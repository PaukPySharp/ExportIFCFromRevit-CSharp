using ExportIfc.IO;
using ExportIfc.Models;

namespace ExportIfc.Manage;

/// <summary>
/// Сборщик моделей Revit из каталога, описанного строкой листа Path.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Перечисляет подходящие RVT-файлы в каталоге строки Path.
/// 2. Отфильтровывает временные и неподходящие файлы.
/// 3. Собирает итоговые модели для дальнейшей оркестрации выгрузки.
///
/// Контракты:
/// 1. Сбор выполняется только по верхнему уровню каталога без рекурсии.
/// 2. В выборку попадают только «чистые» RVT-файлы,
///    прошедшие проверку <see cref="FileSystemEx.IsPureRvt(string)"/>.
/// 3. Если время модификации файла не удалось получить,
///    файл пропускается, а сообщение добавляется в коллекцию проблем.
/// 4. Метод не создаёт и не изменяет файлы в файловой системе.
/// </remarks>
internal static class ManageModelCollector
{
    /// <summary>
    /// Собирает модели из каталога строки листа Path.
    /// </summary>
    /// <param name="rowData">Нормализованные данные строки Path.</param>
    /// <param name="mtimeIssues">Коллекция сообщений о проблемах с временем модификации.</param>
    /// <returns>Найденные модели для дальнейшей выгрузки.</returns>
    /// <remarks>
    /// Метод ожидает, что каталог <see cref="ManagePathRowData.RvtDir"/>
    /// уже был проверен на предыдущем этапе разбора строки.
    /// </remarks>
    public static IReadOnlyList<RevitModel> Collect(
        ManagePathRowData rowData,
        ICollection<string> mtimeIssues)
    {
        ArgumentNullException.ThrowIfNull(rowData);
        ArgumentNullException.ThrowIfNull(mtimeIssues);

        var models = new List<RevitModel>();

        foreach (var file in EnumerateCandidateModels(rowData.RvtDir))
        {
            var normalizedFilePath = FileSystemEx.NormalizePath(file);
            var lastModifiedMinute = FileTime.GetMTimeMinute(file);

            if (lastModifiedMinute is null)
            {
                mtimeIssues.Add(
                    $"{normalizedFilePath} — не удалось определить время модификации");
                continue;
            }

            models.Add(CreateModel(
                rowData,
                normalizedFilePath,
                lastModifiedMinute.Value));
        }

        return models;
    }

    /// <summary>
    /// Перечисляет подходящие RVT-файлы в каталоге строки Path.
    /// </summary>
    /// <param name="directoryPath">Каталог с RVT-моделями.</param>
    /// <returns>Отсортированная последовательность подходящих файлов.</returns>
    /// <remarks>
    /// Перечисление ограничено верхним уровнем каталога.
    /// Сортировка нужна для детерминированного порядка обработки.
    /// </remarks>
    private static IEnumerable<string> EnumerateCandidateModels(string directoryPath)
    {
        return Directory
            .EnumerateFiles(directoryPath, "*", SearchOption.TopDirectoryOnly)
            .Where(FileSystemEx.IsPureRvt)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Создаёт описание модели для дальнейшей выгрузки.
    /// </summary>
    /// <param name="rowData">Нормализованные данные строки Path.</param>
    /// <param name="normalizedRvtPath">Нормализованный путь к RVT-файлу.</param>
    /// <param name="lastModifiedMinute">Время модификации файла с точностью до минуты.</param>
    /// <returns>Итоговое описание модели.</returns>
    private static RevitModel CreateModel(
        ManagePathRowData rowData,
        string normalizedRvtPath,
        DateTime lastModifiedMinute)
    {
        return new RevitModel
        {
            RvtPath = normalizedRvtPath,
            LastModifiedMinute = lastModifiedMinute,
            OutputDirMapping = rowData.OutputDirMapping,
            MappingJson = rowData.MappingJson,
            IfcClassMappingFile = rowData.IfcClassMappingFile,
            OutputDirNoMap = rowData.OutputDirNoMap,
            NoMapJson = rowData.NoMapJson
        };
    }
}
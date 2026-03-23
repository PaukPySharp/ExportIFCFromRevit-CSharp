using ExportIfc.Config;
using ExportIfc.IO;
using ExportIfc.Models;

namespace ExportIfc.Validation;

/// <summary>
/// Проверка актуальности IFC-файлов для модели Revit.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Определяет, требуется ли повторная выгрузка IFC по направлению с маппингом.
/// 2. Определяет, требуется ли повторная выгрузка IFC по направлению без маппинга.
/// 3. Снижает количество обращений к файловой системе за счёт кэша IFC-файлов по папкам.
///
/// Контракты:
/// 1. Сравнение выполняется по времени модификации с точностью до минуты.
/// 2. Для направления с маппингом отсутствие ожидаемого IFC-пути означает,
///    что актуальный IFC отсутствует.
/// 3. Для направления без маппинга отсутствие ожидаемого IFC-пути означает,
///    что направление не требуется и условие считается выполненным.
/// 4. Ошибки доступа к файловой системе трактуются как невозможность подтвердить актуальность IFC.
/// 5. Кэш IFC-файлов собирается один раз на папку в рамках жизненного цикла экземпляра.
/// </remarks>
public sealed class IfcFreshnessChecker : IIfcFreshnessChecker
{
    private readonly Dictionary<string, Dictionary<string, DateTime>> _folderCache =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Проверяет актуальность IFC по направлению с маппингом.
    /// </summary>
    /// <param name="model">Модель Revit.</param>
    /// <returns>
    /// <see langword="true"/>, если IFC существует и не старее RVT;
    /// иначе <see langword="false"/>.
    /// </returns>
    public bool IsIfcUpToDateMapping(RevitModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return IsIfcUpToDate(
            model.ExpectedIfcPathMapping(),
            model.LastModifiedMinute,
            noneMeansFresh: false);
    }

    /// <summary>
    /// Проверяет актуальность IFC по направлению без маппинга.
    /// </summary>
    /// <param name="model">Модель Revit.</param>
    /// <returns>
    /// <see langword="true"/>, если направление не требуется
    /// либо IFC существует и не старее RVT;
    /// иначе <see langword="false"/>.
    /// </returns>
    public bool IsIfcUpToDateNoMap(RevitModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return IsIfcUpToDate(
            model.ExpectedIfcPathNoMap(),
            model.LastModifiedMinute,
            noneMeansFresh: true);
    }

    /// <summary>
    /// Выполняет общую проверку актуальности IFC по ожидаемому пути.
    /// </summary>
    /// <param name="expectedIfcPath">Ожидаемый путь к IFC-файлу.</param>
    /// <param name="rvtMtimeMinute">Время модификации RVT-модели с точностью до минуты.</param>
    /// <param name="noneMeansFresh">
    /// Признак того, что отсутствие ожидаемого пути считается допустимым состоянием.
    /// </param>
    /// <returns>
    /// <see langword="true"/>, если по правилам текущего направления IFC считается актуальным;
    /// иначе <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// Метод проверяет существование ожидаемого IFC-файла в кэше папки
    /// и сравнивает его время модификации с временем RVT-модели.
    /// </remarks>
    private bool IsIfcUpToDate(
        string? expectedIfcPath,
        DateTime rvtMtimeMinute,
        bool noneMeansFresh)
    {
        if (string.IsNullOrWhiteSpace(expectedIfcPath))
            return noneMeansFresh;

        try
        {
            var folder = Path.GetDirectoryName(expectedIfcPath);
            var fileName = Path.GetFileName(expectedIfcPath);

            if (string.IsNullOrWhiteSpace(folder) || string.IsNullOrWhiteSpace(fileName))
                return false;

            var cache = GetFolderCache(folder);
            if (!cache.TryGetValue(fileName, out var ifcMtimeMinute))
                return false;

            return ifcMtimeMinute >= rvtMtimeMinute;
        }
        catch (Exception ex) when (
            ex is IOException ||
            ex is UnauthorizedAccessException ||
            ex is ArgumentException)
        {
            return false;
        }
    }

    /// <summary>
    /// Возвращает кэш IFC-файлов для указанной папки.
    /// </summary>
    /// <param name="folder">Путь к папке с IFC-файлами.</param>
    /// <returns>
    /// Словарь вида «имя IFC-файла -> время модификации с точностью до минуты».
    /// </returns>
    /// <remarks>
    /// При первом обращении метод сканирует папку, читает времена модификации IFC-файлов
    /// и сохраняет результат в локальный кэш экземпляра.
    /// </remarks>
    private Dictionary<string, DateTime> GetFolderCache(string folder)
    {
        if (_folderCache.TryGetValue(folder, out var existing))
            return existing;

        var cache = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        try
        {
            if (!Directory.Exists(folder))
            {
                _folderCache[folder] = cache;
                return cache;
            }

            var files = Directory
                .EnumerateFiles(folder, "*", SearchOption.TopDirectoryOnly)
                .Where(path => string.Equals(
                    Path.GetExtension(path),
                    ProjectFileExtensions.Ifc,
                    StringComparison.OrdinalIgnoreCase));

            foreach (var file in files)
            {
                var mtime = FileTime.GetMTimeMinute(file);
                if (mtime is null)
                    continue;

                var fileName = Path.GetFileName(file);
                if (string.IsNullOrWhiteSpace(fileName))
                    continue;

                cache[fileName] = mtime.Value;
            }
        }
        catch (Exception ex) when (
            ex is IOException ||
            ex is UnauthorizedAccessException ||
            ex is ArgumentException)
        {
            // Ошибки доступа к папке трактуются как невозможность
            // подтвердить актуальность IFC в текущем прогоне.
        }

        _folderCache[folder] = cache;
        return cache;
    }
}
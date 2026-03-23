using ExportIfc.Config;
using ExportIfc.IO;
using ExportIfc.Settings;

namespace ExportIfc.Manage;

/// <summary>
/// Разрешает и проверяет файловые зависимости одной строки листа Path.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Строит полные пути к обязательным конфигурационным файлам строки.
/// 2. Проверяет контракт Excel-значений, которые должны задавать только имя файла.
/// 3. Отделяет доменную логику строки Path от общих файловых утилит.
///
/// Контракты:
/// 1. Значения ячеек с именами конфигурационных файлов должны содержать только имя файла без каталогов.
/// 2. Обязательные конфигурационные файлы должны существовать.
/// 3. Базовый каталог конфигурационных файлов экспорта должен быть задан и нормализован заранее.
/// </remarks>
internal sealed class ManagePathRowFileResolver
{
    private readonly AppSettings _stg;
    private readonly string _baseConfigDirectory;

    /// <summary>
    /// Создаёт резолвер файловых зависимостей строки Path.
    /// </summary>
    /// <param name="stg">Итоговые настройки приложения.</param>
    /// <param name="baseConfigDirectory">Базовый каталог конфигурационных файлов экспорта.</param>
    public ManagePathRowFileResolver(
        AppSettings stg,
        string baseConfigDirectory)
    {
        ArgumentNullException.ThrowIfNull(stg);

        if (string.IsNullOrWhiteSpace(baseConfigDirectory))
        {
            throw new ArgumentException(
                "Не задан базовый каталог конфигурационных файлов экспорта.",
                nameof(baseConfigDirectory));
        }

        _stg = stg;
        _baseConfigDirectory = Path.GetFullPath(baseConfigDirectory.Trim());
    }

    /// <summary>
    /// Строит путь к обязательному JSON-файлу настроек выгрузки с маппингом.
    /// </summary>
    /// <param name="mapDir">Каталог с файлами настроек маппинга.</param>
    /// <returns>Полный путь к существующему JSON-файлу.</returns>
    public string ResolveMappingJson(string mapDir)
    {
        if (string.IsNullOrWhiteSpace(mapDir))
        {
            throw new ArgumentException(
                "Не задан каталог с файлами настроек маппинга.",
                nameof(mapDir));
        }

        var mappingJson = Path.Combine(mapDir, ProjectFiles.JsonConfigFileName(_stg));
        FileSystemEx.EnsureExistingFile(mappingJson, ManagePathRowDescriptions.MappingJsonFile);

        return mappingJson;
    }

    /// <summary>
    /// Строит путь к обязательному файлу сопоставления категорий.
    /// </summary>
    /// <param name="row">Номер строки Excel.</param>
    /// <param name="ifcClassMappingRaw">Сырое имя файла из ячейки.</param>
    /// <returns>Полный путь к существующему TXT-файлу.</returns>
    public string ResolveIfcClassMappingFile(int row, string ifcClassMappingRaw)
    {
        return BuildExistingConfigFileInDirectory(
            row,
            ifcClassMappingRaw,
            Path.Combine(_baseConfigDirectory, _stg.MappingDirLayers),
            ProjectFileExtensions.Txt,
            ManagePathRowDescriptions.IfcClassMappingFileName,
            ManagePathRowDescriptions.IfcClassMappingFile);
    }

    /// <summary>
    /// Строит путь к обязательному JSON-файлу выгрузки без маппинга.
    /// </summary>
    /// <param name="row">Номер строки Excel.</param>
    /// <param name="noMapNameRaw">Сырое имя файла из ячейки.</param>
    /// <returns>Полный путь к существующему JSON-файлу.</returns>
    public string ResolveNoMapJson(int row, string noMapNameRaw)
    {
        return BuildExistingConfigFileInDirectory(
            row,
            noMapNameRaw,
            Path.Combine(_baseConfigDirectory, _stg.MappingDirCommon),
            ProjectFileExtensions.Json,
            ManagePathRowDescriptions.NoMapJsonFileName,
            ManagePathRowDescriptions.NoMapJsonFile);
    }

    /// <summary>
    /// Строит путь к обязательному конфигурационному файлу в заданном каталоге.
    /// </summary>
    /// <param name="row">Номер строки Excel.</param>
    /// <param name="rawValue">Сырое значение имени файла из ячейки.</param>
    /// <param name="directoryPath">Базовый каталог для поиска файла.</param>
    /// <param name="requiredExtension">Обязательное расширение файла.</param>
    /// <param name="valueDescription">Описание значения для текста ошибки.</param>
    /// <param name="fileDescription">Описание файла для текста ошибки.</param>
    /// <returns>Полный путь к существующему файлу.</returns>
    private string BuildExistingConfigFileInDirectory(
        int row,
        string rawValue,
        string directoryPath,
        string requiredExtension,
        string valueDescription,
        string fileDescription)
    {
        var fileName = ValidateConfigFileName(
            row,
            rawValue,
            valueDescription,
            requiredExtension);
        var fullPath = Path.Combine(directoryPath, fileName);

        FileSystemEx.EnsureExistingFile(fullPath, fileDescription);
        return fullPath;
    }

    /// <summary>
    /// Проверяет, что значение из Excel задаёт только корректное имя файла.
    /// </summary>
    /// <param name="row">Номер строки Excel.</param>
    /// <param name="rawValue">Сырое значение из ячейки.</param>
    /// <param name="valueDescription">Описание значения для текста ошибки.</param>
    /// <param name="requiredExtension">Обязательное расширение файла.</param>
    /// <returns>Проверенное имя файла с обязательным расширением.</returns>
    /// <exception cref="InvalidDataException">
    /// Выбрасывается, если значение пустое или не может быть интерпретировано
    /// как корректное имя файла без каталогов.
    /// </exception>
    private string ValidateConfigFileName(
        int row,
        string rawValue,
        string valueDescription,
        string requiredExtension)
    {
        var trimmedValue = (rawValue ?? string.Empty).Trim();
        if (trimmedValue.Length == 0)
        {
            throw new InvalidDataException(
                $"Лист '{_stg.SheetPath}', строка {row}: не задано {valueDescription}.");
        }

        var fileName = FileSystemEx.TryNormalizeFileName(trimmedValue, requiredExtension);
        if (fileName is not null)
            return fileName;

        throw new InvalidDataException(
            $"Лист '{_stg.SheetPath}', строка {row}: {valueDescription} должно быть корректным именем файла без каталогов: '{rawValue}'.");
    }
}
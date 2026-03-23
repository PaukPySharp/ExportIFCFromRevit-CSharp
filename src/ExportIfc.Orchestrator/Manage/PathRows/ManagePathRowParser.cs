using ExportIfc.Config;
using ExportIfc.IO;
using ExportIfc.Logging;
using ExportIfc.Settings;

namespace ExportIfc.Manage;

/// <summary>
/// Парсер одной строки листа Path из управляющей Excel-книги
/// <see cref="ProjectFileNames.ManageWorkbook"/>.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Проверить обязательные поля строки.
/// 2. Привести и валидировать пути.
/// 3. Собрать итоговую конфигурацию строки для дальнейшей обработки.
///
/// Контракты:
/// 1. Для путей из Excel принимаются только абсолютные пути.
/// 2. Обязательные конфигурационные файлы должны существовать.
/// 3. Для выгрузки без маппинга обе ячейки должны быть заполнены одновременно
///    либо одновременно пусты.
/// 4. Значения ячеек с именами конфигурационных файлов должны задаваться
///    только именем файла без каталогов и абсолютных путей.
/// </remarks>
internal sealed class ManagePathRowParser
{
    private readonly AppSettings _stg;
    private readonly ConsoleLogger _manageLog;
    private readonly ManagePathRowFileResolver _fileResolver;

    /// <summary>
    /// Создаёт парсер строки листа Path.
    /// </summary>
    /// <param name="stg">Итоговые настройки приложения.</param>
    /// <param name="baseConfigDirectory">Базовый каталог конфигурационных файлов экспорта.</param>
    /// <param name="manageLog">Логгер загрузчика управляющей Excel-книги моделей.</param>
    public ManagePathRowParser(
        AppSettings stg,
        string baseConfigDirectory,
        ConsoleLogger manageLog)
    {
        ArgumentNullException.ThrowIfNull(stg);
        ArgumentNullException.ThrowIfNull(manageLog);

        _stg = stg;
        _manageLog = manageLog;
        _fileResolver = new ManagePathRowFileResolver(stg, baseConfigDirectory);
    }

    /// <summary>
    /// Разбирает строку листа Path.
    /// </summary>
    /// <param name="row">Номер строки Excel.</param>
    /// <param name="values">Сырые значения строки.</param>
    /// <returns>Нормализованные данные строки или <see langword="null"/>.</returns>
    /// <remarks>
    /// Ошибки формата строки и отсутствие обязательных каталогов строки
    /// логируются как предупреждение, после чего строка пропускается.
    ///
    /// Ошибки обязательных конфигурационных файлов и нарушения контракта
    /// имени файла считаются фатальными и не замалчиваются.
    /// </remarks>
    public ManagePathRowData? TryParse(int row, ManagePathRowValues values)
    {
        if (!HasRequiredValues(row, values))
            return null;

        var rvtDir = TryParseDirectoryPath(
            row,
            values.RvtDirRaw,
            ManagePathRowDescriptions.ModelsDirectory,
            mustExist: true);
        if (rvtDir is null)
            return null;

        var outputDirMapping = TryParseOutputDirectory(
            row,
            values.OutputDirMappingRaw,
            ManagePathRowDescriptions.OutputDirMapping);
        if (outputDirMapping is null)
            return null;

        var mapDir = TryParseDirectoryPath(
            row,
            values.MappingDirectoryRaw,
            ManagePathRowDescriptions.MappingSettingsDirectory,
            mustExist: true);
        if (mapDir is null)
            return null;

        var mappingJson = _fileResolver.ResolveMappingJson(mapDir);
        var ifcClassMappingFile = _fileResolver.ResolveIfcClassMappingFile(row, values.IfcClassMappingRaw);

        var optionalExport = TryParseOptionalNoMapExport(row, values);
        if (!optionalExport.IsValid)
            return null;

        return new ManagePathRowData
        {
            RowKey = BuildRowKey(
                rvtDir,
                outputDirMapping,
                mappingJson,
                ifcClassMappingFile,
                optionalExport.OutputDirNoMap,
                optionalExport.NoMapJson),
            RvtDir = rvtDir,
            OutputDirMapping = outputDirMapping,
            MappingJson = mappingJson,
            IfcClassMappingFile = ifcClassMappingFile,
            OutputDirNoMap = optionalExport.OutputDirNoMap,
            NoMapJson = optionalExport.NoMapJson
        };
    }

    /// <summary>
    /// Проверяет заполненность обязательных полей строки.
    /// </summary>
    /// <param name="row">Номер строки Excel.</param>
    /// <param name="values">Сырые значения строки.</param>
    /// <returns>
    /// <see langword="true"/>, если обязательные поля заполнены;
    /// иначе <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// При отсутствии обязательных значений метод пишет предупреждение
    /// и не выбрасывает исключение.
    /// </remarks>
    private bool HasRequiredValues(int row, ManagePathRowValues values)
    {
        var missingFields = new List<string>(capacity: 4);

        if (string.IsNullOrWhiteSpace(values.RvtDirRaw))
            missingFields.Add(ManagePathRowDescriptions.ModelsDirectory);

        if (string.IsNullOrWhiteSpace(values.OutputDirMappingRaw))
            missingFields.Add(ManagePathRowDescriptions.OutputDirMapping);

        if (string.IsNullOrWhiteSpace(values.MappingDirectoryRaw))
            missingFields.Add(ManagePathRowDescriptions.MappingSettingsDirectory);

        if (string.IsNullOrWhiteSpace(values.IfcClassMappingRaw))
            missingFields.Add(ManagePathRowDescriptions.IfcClassMappingFileName);

        if (missingFields.Count == 0)
            return true;

        _manageLog.Warn(
            "Лист '{0}', строка {1}: не заполнены обязательные поля: {2}. Строка пропущена.",
            _stg.SheetPath,
            row,
            string.Join(", ", missingFields));

        return false;
    }

    /// <summary>
    /// Пытается разобрать абсолютный путь к каталогу.
    /// </summary>
    /// <param name="row">Номер строки Excel.</param>
    /// <param name="rawValue">Сырое значение из ячейки.</param>
    /// <param name="description">Человеко-читаемое описание поля.</param>
    /// <param name="mustExist">
    /// Признак того, что каталог должен существовать на момент проверки.
    /// </param>
    /// <returns>Нормализованный путь или <see langword="null"/>.</returns>
    private string? TryParseDirectoryPath(
        int row,
        string rawValue,
        string description,
        bool mustExist)
    {
        var path = FileSystemEx.TryNormalizeAbsolutePath(rawValue);
        if (path is null)
        {
            LogInvalidAbsolutePath(row, rawValue, description);
            return null;
        }

        if (Directory.Exists(path))
            return path;

        if (File.Exists(path))
        {
            _manageLog.Warn(
                "Лист '{0}', строка {1}: {2} должен указывать на каталог, но найден файл: '{3}'.",
                _stg.SheetPath,
                row,
                description,
                rawValue);
            return null;
        }

        if (!mustExist)
            return path;

        _manageLog.Warn(
            "Лист '{0}', строка {1}: {2} не найден: '{3}'.",
            _stg.SheetPath,
            row,
            description,
            rawValue);
        return null;
    }

    /// <summary>
    /// Пытается разобрать каталог назначения для выгрузки.
    /// </summary>
    /// <param name="row">Номер строки Excel.</param>
    /// <param name="rawValue">Сырое значение из ячейки.</param>
    /// <param name="description">Человеко-читаемое описание поля.</param>
    /// <returns>Нормализованный путь или <see langword="null"/>.</returns>
    /// <remarks>
    /// Допускается отсутствие каталога на диске.
    /// Если путь уже существует, он обязан указывать именно на каталог.
    /// </remarks>
    private string? TryParseOutputDirectory(
        int row,
        string rawValue,
        string description)
    {
        var path = TryParseDirectoryPath(
            row,
            rawValue,
            description,
            mustExist: false);
        if (path is null)
            return null;

        FileSystemEx.EnsureDirectoryOrMissing(
            path,
            $"{ManagePathRowDescriptions.OutputDirectory} {description}");

        return path;
    }

    /// <summary>
    /// Разбирает настройки необязательной выгрузки без маппинга.
    /// </summary>
    /// <param name="row">Номер строки Excel.</param>
    /// <param name="values">Сырые значения строки.</param>
    /// <returns>Результат разбора параметров выгрузки без маппинга.</returns>
    /// <remarks>
    /// Если режим выгрузки без маппинга отключён, метод всегда возвращает
    /// корректный пустой результат.
    /// </remarks>
    private OptionalNoMapExport TryParseOptionalNoMapExport(
        int row,
        ManagePathRowValues values)
    {
        if (!_stg.EnableUnmappedExport)
            return OptionalNoMapExport.Valid(null, null);

        // Optional здесь относится ко всему no-map маршруту:
        // либо каталог и JSON заданы вместе, либо обе ячейки пусты.
        var hasOutNoMap = !string.IsNullOrWhiteSpace(values.OutputDirNoMapRaw);
        var hasNoMapName = !string.IsNullOrWhiteSpace(values.NoMapJsonRaw);

        if (hasOutNoMap != hasNoMapName)
        {
            _manageLog.Warn(
                "Лист '{0}', строка {1}: для выгрузки без маппинга нужно заполнить и каталог, и имя JSON-файла, либо оставить обе ячейки пустыми.",
                _stg.SheetPath,
                row);
            return OptionalNoMapExport.Invalid();
        }

        if (!hasOutNoMap)
            return OptionalNoMapExport.Valid(null, null);

        var outputDirNoMap = TryParseOutputDirectory(
            row,
            values.OutputDirNoMapRaw,
            ManagePathRowDescriptions.OutputDirNoMap);
        if (outputDirNoMap is null)
            return OptionalNoMapExport.Invalid();

        var noMapJson = _fileResolver.ResolveNoMapJson(row, values.NoMapJsonRaw);
        return OptionalNoMapExport.Valid(outputDirNoMap, noMapJson);
    }

    /// <summary>
    /// Пишет предупреждение о невалидном абсолютном пути.
    /// </summary>
    /// <param name="row">Номер строки Excel.</param>
    /// <param name="rawValue">Сырое значение из ячейки.</param>
    /// <param name="description">Человеко-читаемое описание поля.</param>
    private void LogInvalidAbsolutePath(
        int row,
        string rawValue,
        string description)
    {
        _manageLog.Warn(
            "Лист '{0}', строка {1}: {2} должен быть задан абсолютным путём: '{3}'.",
            _stg.SheetPath,
            row,
            description,
            rawValue);
    }

    /// <summary>
    /// Собирает ключ строки для защиты от дубликатов.
    /// </summary>
    /// <param name="rvtDir">Каталог RVT-моделей.</param>
    /// <param name="outputDirMapping">Каталог выгрузки с маппингом.</param>
    /// <param name="mappingJson">Путь к JSON-конфигурации с маппингом.</param>
    /// <param name="ifcClassMappingFile">Путь к файлу сопоставления категорий.</param>
    /// <param name="outputDirNoMap">Каталог выгрузки без маппинга.</param>
    /// <param name="noMapJson">Путь к JSON-конфигурации без маппинга.</param>
    /// <returns>Нормализованный ключ строки.</returns>
    private static string BuildRowKey(
        string rvtDir,
        string outputDirMapping,
        string mappingJson,
        string ifcClassMappingFile,
        string? outputDirNoMap,
        string? noMapJson)
    {
        return string.Join(
            "|",
            rvtDir,
            outputDirMapping,
            mappingJson,
            ifcClassMappingFile,
            outputDirNoMap ?? string.Empty,
            noMapJson ?? string.Empty);
    }

    /// <summary>
    /// Результат разбора опциональной выгрузки без маппинга.
    /// </summary>
    /// <param name="IsValid">Признак корректности набора значений.</param>
    /// <param name="OutputDirNoMap">Каталог выгрузки без маппинга.</param>
    /// <param name="NoMapJson">Путь к JSON-файлу без маппинга.</param>
    private readonly record struct OptionalNoMapExport(
        bool IsValid,
        string? OutputDirNoMap,
        string? NoMapJson)
    {
        /// <summary>
        /// Создаёт невалидный результат разбора.
        /// </summary>
        public static OptionalNoMapExport Invalid() => new(false, null, null);

        /// <summary>
        /// Создаёт корректный результат разбора.
        /// </summary>
        /// <param name="outputDirNoMap">Каталог выгрузки без маппинга.</param>
        /// <param name="noMapJson">Путь к JSON-файлу без маппинга.</param>
        public static OptionalNoMapExport Valid(string? outputDirNoMap, string? noMapJson)
            => new(true, outputDirNoMap, noMapJson);
    }
}
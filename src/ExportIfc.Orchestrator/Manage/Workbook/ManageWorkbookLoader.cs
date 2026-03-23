using ClosedXML.Excel;

using ExportIfc.Config;
using ExportIfc.Excel;
using ExportIfc.IO;
using ExportIfc.Logging;
using ExportIfc.Models;
using ExportIfc.Settings;

namespace ExportIfc.Manage;

/// <summary>
/// Загрузчик управляющей Excel-книги
/// <see cref="ProjectFileNames.ManageWorkbook"/>.
/// </summary>
/// <remarks>
/// Назначение:
/// Читает рабочую книгу, формирует список моделей, ignore-список
/// и сообщения о проблемах с временем модификации.
///
/// Контракты:
/// 1. Для строк листа Path используются только абсолютные пути.
/// 2. Основной лист Path читается до первой полностью пустой строки.
/// 3. Лист со списком исключений читается по занятому диапазону, пустые ячейки пропускаются.
/// 4. Обязательные файлы конфигурации проверяются сразу.
/// 5. Для зависимых конфигурационных файлов используется проверенный runtime-путь
///    <see cref="ProjectPaths.DirExportConfig"/>.
/// 6. Сбор моделей из каталога вынесен в отдельный класс.
/// </remarks>
public sealed class ManageWorkbookLoader : IManageWorkbookLoader
{
    private readonly ConsoleLogger _manageLog = Log.For(LogComponents.Manage);

    /// <summary>
    /// Загружает данные из управляющей Excel-книги моделей.
    /// </summary>
    /// <param name="manageXlsxPath">Полный путь к <see cref="ProjectFileNames.ManageWorkbook"/>.</param>
    /// <param name="stg">Итоговые настройки приложения.</param>
    /// <param name="paths">Подготовленные runtime-пути проекта.</param>
    /// <returns>Собранные данные для дальнейшей оркестрации.</returns>
    /// <remarks>
    /// Отсутствие workbook-файла считается фатальной ошибкой.
    /// Отсутствие обязательного листа Path тоже считается фатальной ошибкой конфигурации.
    /// Отсутствие листа ignore обрабатывается мягко:
    /// загрузчик пишет предупреждение и продолжает работу без ignore-списка.
    ///
    /// Workbook открывается в режиме чтения с разрешением совместного доступа,
    /// чтобы повысить шанс чтения файла, уже открытого в Excel или другом процессе.
    /// Это не гарантирует успешное открытие при эксклюзивной блокировке файла.
    ///
    /// Если workbook открыт в Excel с несохранёнными изменениями,
    /// загрузчик читает только последнюю сохранённую на диске версию файла.
    /// </remarks>
    public ManageWorkbookData Load(
        string manageXlsxPath,
        AppSettings stg,
        ProjectPaths paths)
    {
        ArgumentNullException.ThrowIfNull(stg);
        ArgumentNullException.ThrowIfNull(paths);

        var fullManageXlsxPath = FileSystemEx.NormalizeExistingFilePath(
            manageXlsxPath,
            ProjectFileNames.ManageWorkbook);

        var models = new List<RevitModel>();
        var ignore = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var mtimeIssues = new List<string>();

        // Защита от дублирующихся конфигурационных строк листа Path.
        var seenRows = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using var workbook = ExcelWorkbookOpener.OpenForSharedRead(
            fullManageXlsxPath,
            ProjectFileNames.ManageWorkbook);

        ReadPathSheet(
            workbook,
            fullManageXlsxPath,
            stg,
            paths,
            models,
            mtimeIssues,
            seenRows);

        ReadIgnoreSheet(
            workbook,
            stg,
            ignore);

        return new ManageWorkbookData
        {
            Models = models,
            Ignore = ignore,
            MTimeIssues = mtimeIssues
        };
    }

    /// <summary>
    /// Читает основной лист Path и собирает модели для экспорта.
    /// </summary>
    /// <param name="workbook">Открытая Excel-книга.</param>
    /// <param name="manageXlsxPath">Полный путь к workbook-файлу.</param>
    /// <param name="stg">Итоговые настройки приложения.</param>
    /// <param name="paths">Подготовленные runtime-пути проекта.</param>
    /// <param name="models">Накопитель найденных моделей.</param>
    /// <param name="mtimeIssues">Накопитель предупреждений по времени модификации.</param>
    /// <param name="seenRows">Набор ключей строк для защиты от дубликатов.</param>
    /// <remarks>
    /// Лист читается до первой полностью пустой строки.
    /// Для обязательных конфигурационных файлов используется
    /// <see cref="ProjectPaths.DirExportConfig"/>.
    /// </remarks>
    private void ReadPathSheet(
        XLWorkbook workbook,
        string manageXlsxPath,
        AppSettings stg,
        ProjectPaths paths,
        List<RevitModel> models,
        List<string> mtimeIssues,
        HashSet<string> seenRows)
    {
        if (!workbook.TryGetWorksheet(stg.SheetPath, out var worksheet))
        {
            throw new InvalidDataException(
                $"В файле '{manageXlsxPath}' не найден обязательный лист '{stg.SheetPath}'. " +
                "Экспорт не может продолжаться.");
        }

        var parser = new ManagePathRowParser(stg, paths.DirExportConfig, _manageLog);

        for (var row = 2; ; row++)
        {
            var rowValues = ReadPathRowValues(worksheet, row);
            if (rowValues.IsBlank())
                break;

            var rowData = parser.TryParse(row, rowValues);
            if (rowData is null)
                continue;

            if (!seenRows.Add(rowData.RowKey))
            {
                _manageLog.Warn(
                    "Лист '{0}', строка {1}: обнаружен дубликат конфигурации, строка пропущена.",
                    stg.SheetPath,
                    row);
                continue;
            }

            // Из одной строки Path может получиться несколько RevitModel,
            // если в каталоге найдено несколько RVT-файлов.
            var revitModels = ManageModelCollector.Collect(rowData, mtimeIssues);

            models.AddRange(revitModels);
        }
    }

    /// <summary>
    /// Читает лист ignore и собирает список путей, исключённых из обработки.
    /// </summary>
    /// <param name="workbook">Открытая Excel-книга.</param>
    /// <param name="stg">Итоговые настройки приложения.</param>
    /// <param name="ignore">Накопитель нормализованных ignore-путей.</param>
    /// <remarks>
    /// Лист читается по занятому диапазону строк.
    /// Пустые ячейки пропускаются, невалидные относительные пути логируются как предупреждения.
    /// </remarks>
    private void ReadIgnoreSheet(
        XLWorkbook workbook,
        AppSettings stg,
        HashSet<string> ignore)
    {
        if (!workbook.TryGetWorksheet(stg.SheetIgnore, out var worksheet))
        {
            _manageLog.Warn(
                "Лист '{0}' не найден в файле {1}. Ignore-список не загружен.",
                stg.SheetIgnore,
                ProjectFileNames.ManageWorkbook);
            return;
        }

        var lastRow = ExcelCells.GetLastUsedRowNumber(worksheet);
        if (lastRow < 2)
            return;

        for (var row = 2; row <= lastRow; row++)
        {
            var pathRaw = ExcelCells.GetCellText(
                worksheet,
                row,
                ExcelSchema.ManageIgnoreColPath);

            if (string.IsNullOrWhiteSpace(pathRaw))
                continue;

            var path = FileSystemEx.TryNormalizeAbsolutePath(pathRaw);
            if (path is null)
            {
                _manageLog.Warn(
                    "Лист '{0}', строка {1}: путь ignore должен быть абсолютным. Значение '{2}' пропущено.",
                    stg.SheetIgnore,
                    row,
                    pathRaw);
                continue;
            }

            ignore.Add(FileSystemEx.NormalizePathWithLowerExtension(path));
        }
    }

    /// <summary>
    /// Считывает сырые значения одной строки листа Path.
    /// </summary>
    /// <param name="worksheet">Лист Path управляющей книги.</param>
    /// <param name="row">Номер строки Excel.</param>
    /// <returns>Набор сырых значений строки.</returns>
    private static ManagePathRowValues ReadPathRowValues(
        IXLWorksheet worksheet,
        int row)
    {
        string Read(int column) => ExcelCells.GetCellText(worksheet, row, column);

        return new ManagePathRowValues(
            RvtDirRaw: Read(ExcelSchema.ManageColRvtDir),
            OutputDirMappingRaw: Read(ExcelSchema.ManageColOutMap),
            MappingDirectoryRaw: Read(ExcelSchema.ManageColMapDir),
            IfcClassMappingRaw: Read(ExcelSchema.ManageColIfcClassMapping),
            OutputDirNoMapRaw: Read(ExcelSchema.ManageColOutNoMap),
            NoMapJsonRaw: Read(ExcelSchema.ManageColNoMapName));
    }
}

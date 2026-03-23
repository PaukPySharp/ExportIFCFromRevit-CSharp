using ExportIfc.Config;
using ExportIfc.Logging;
using ExportIfc.Settings;

namespace ExportIfc.Export.Runtime;

/// <summary>
/// Контекст одного запуска оркестратора.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Собирает в одном объекте итоговые настройки, рабочие пути,
///    логгер и производные значения конкретного batch-прогона.
/// 2. Убирает повторное вычисление одних и тех же runtime-данных
///    на разных шагах оркестрации.
/// 3. Делает зависимости текущего запуска явно передаваемыми между шагами оркестратора.
///
/// Контракты:
/// 1. Контекст создаётся один раз в начале прогона и далее используется как неизменяемый снимок состояния.
/// 2. Все производные пути и идентификаторы рассчитываются при создании контекста,
///    а не лениво по ходу выполнения.
/// 3. Контекст не содержит прикладной логики экспорта и не управляет жизненным циклом зависимостей.
/// </remarks>
internal sealed class ExportRunContext
{
    /// <summary>
    /// Инициализирует контекст уже подготовленными значениями текущего запуска.
    /// </summary>
    /// <param name="settings">Итоговые настройки приложения.</param>
    /// <param name="paths">Набор рабочих путей проекта.</param>
    /// <param name="exportLog">Логгер оркестратора.</param>
    /// <param name="manageWorkbookPath">Полный путь к управляющей Excel-книге.</param>
    /// <param name="historyWorkbookPath">Полный путь к Excel-файлу истории.</param>
    /// <param name="tmpJsonPath">Полный путь к временному transfer-файлу.</param>
    /// <param name="startedAtUnix">Unix-время старта текущего прогона.</param>
    /// <param name="runId">Идентификатор текущего запуска.</param>
    private ExportRunContext(
        AppSettings settings,
        ProjectPaths paths,
        ConsoleLogger exportLog,
        string manageWorkbookPath,
        string historyWorkbookPath,
        string tmpJsonPath,
        long startedAtUnix,
        string runId)
    {
        Settings = settings;
        Paths = paths;
        ExportLog = exportLog;
        ManageWorkbookPath = manageWorkbookPath;
        HistoryWorkbookPath = historyWorkbookPath;
        TmpJsonPath = tmpJsonPath;
        StartedAtUnix = startedAtUnix;
        RunId = runId;
    }

    /// <summary>
    /// Итоговые настройки приложения для текущего запуска.
    /// </summary>
    public AppSettings Settings { get; }

    /// <summary>
    /// Набор рабочих путей проекта, вычисленных для текущего запуска.
    /// </summary>
    public ProjectPaths Paths { get; }

    /// <summary>
    /// Логгер оркестратора текущего запуска.
    /// </summary>
    public ConsoleLogger ExportLog { get; }

    /// <summary>
    /// Полный путь к управляющей Excel-книге моделей.
    /// </summary>
    public string ManageWorkbookPath { get; }

    /// <summary>
    /// Полный путь к Excel-файлу рабочей истории состояний моделей.
    /// </summary>
    public string HistoryWorkbookPath { get; }

    /// <summary>
    /// Полный путь к временному файлу <see cref="ProjectFileNames.TmpJson"/>.
    /// </summary>
    public string TmpJsonPath { get; }

    /// <summary>
    /// Unix-время старта текущего прогона.
    /// </summary>
    public long StartedAtUnix { get; }

    /// <summary>
    /// Идентификатор текущего запуска для логов и batch-артефактов.
    /// </summary>
    public string RunId { get; }

    /// <summary>
    /// Создаёт контекст запуска оркестратора.
    /// </summary>
    /// <param name="settings">Загруженные итоговые настройки приложения.</param>
    /// <returns>Готовый контекст запуска.</returns>
    /// <remarks>
    /// Метод один раз вычисляет производные пути, идентификатор запуска
    /// и служебные значения, которые затем переиспользуются
    /// во всём orchestration-сценарии.
    /// </remarks>
    public static ExportRunContext Create(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        // Рабочие пути текущего запуска.
        var paths = ProjectPaths.Build(settings);

        // Логгер и ключевые файловые артефакты прогона.
        var exportLog = Log.For(LogComponents.Exporter);
        var manageWorkbookPath = ProjectFiles.ManageXlsx(paths);
        var historyWorkbookPath = ProjectFiles.HistoryXlsx(paths);
        var tmpJsonPath = ProjectFiles.TmpJson(paths);

        // Служебные идентификаторы и время старта прогона.
        var startedAtUnix = DateTimeOffset.Now.ToUnixTimeSeconds();
        var runId = DateTime.Now.ToString(ProjectFormats.RunId);

        return new ExportRunContext(
            settings,
            paths,
            exportLog,
            manageWorkbookPath,
            historyWorkbookPath,
            tmpJsonPath,
            startedAtUnix,
            runId);
    }
}
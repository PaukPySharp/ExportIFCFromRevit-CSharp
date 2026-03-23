namespace ExportIfc.Config;

/// <summary>
/// Производные каталоги проекта.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Формирует абсолютные пути к ключевым каталогам внутри <see cref="ProjectDirectoryNames.AdminData"/>.
/// 2. Централизует правила построения рабочих директорий.
/// 3. Разводит каталоговую и файловую логику по разным модулям.
///
/// Контракты:
/// 1. Методы этого класса только вычисляют пути и не создают каталоги.
/// 2. Базовый путь <c>dirAdminData</c> должен приходить уже в итоговом виде.
/// 3. Изменение схемы вложенных каталогов выполняется здесь, а не размазывается по проекту.
/// </remarks>
public static class ProjectDirectories
{
    /// <summary>
    /// Возвращает путь к каталогу истории внутри <see cref="ProjectDirectoryNames.AdminData"/>.
    /// </summary>
    /// <param name="dirAdminData">Базовый каталог административных данных.</param>
    /// <returns>Полный путь к каталогу истории.</returns>
    public static string History(string dirAdminData)
        => Path.Combine(dirAdminData, ProjectDirectoryNames.History);

    /// <summary>
    /// Возвращает путь к каталогу логов внутри <see cref="ProjectDirectoryNames.AdminData"/>.
    /// </summary>
    /// <param name="dirAdminData">Базовый каталог административных данных.</param>
    /// <returns>Полный путь к каталогу логов.</returns>
    public static string Logs(string dirAdminData)
        => Path.Combine(dirAdminData, LogDirectoryNames.Logs);

    /// <summary>
    /// Возвращает путь к каталогу технических логов внутри каталога <see cref="LogDirectoryNames.Logs"/>.
    /// </summary>
    /// <param name="dirAdminData">Базовый каталог административных данных.</param>
    /// <returns>Полный путь к каталогу технических логов.</returns>
    public static string TechLogs(string dirAdminData)
        => Path.Combine(Logs(dirAdminData), LogDirectoryNames.Tech);

    /// <summary>
    /// Возвращает путь к каталогу зеркал консольного вывода внутри <see cref="LogDirectoryNames.Tech"/>.
    /// </summary>
    /// <param name="dirAdminData">Базовый каталог административных данных.</param>
    /// <returns>Полный путь к каталогу консольных техлогов.</returns>
    public static string TechConsoleLogs(string dirAdminData)
        => Path.Combine(TechLogs(dirAdminData), LogDirectoryNames.Console);
}

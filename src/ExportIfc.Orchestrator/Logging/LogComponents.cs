namespace ExportIfc.Logging;

/// <summary>
/// Имена компонентов форматированного лога оркестратора.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Централизует короткие имена источников сообщений.
/// 2. Убирает строковые литералы из вызовов <see cref="Log.For(string)"/>.
/// 3. Делает компонентные имена единообразными по всему оркестратору.
///
/// Контракты:
/// 1. Значения используются только как технические имена компонентов лога.
/// 2. Имена должны оставаться короткими и стабильными внутри текущего формата вывода.
/// 3. Это не пользовательские настройки и не внешний ini/json-контракт.
/// </remarks>
internal static class LogComponents
{
    /// <summary>
    /// Основная точка входа приложения.
    /// </summary>
    public const string Main = "main";

    /// <summary>
    /// Загрузка и разбор управляющей workbook-книги.
    /// </summary>
    public const string Manage = "manage";

    /// <summary>
    /// Запуск внешнего процесса Revit.
    /// </summary>
    public const string Launcher = "launcher";

    /// <summary>
    /// Основной orchestration-процесс выгрузки.
    /// </summary>
    public const string Exporter = "exporter";

    /// <summary>
    /// Чтение и обработка рабочей истории состояний моделей.
    /// </summary>
    public const string History = "history";
}
using Spectre.Console;
using System.Globalization;

using ExportIfc.Config;

namespace ExportIfc.Logging;

/// <summary>
/// Компонентный логгер оркестратора на базе Spectre.Console.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Форматирует построчный вывод компонентов оркестратора.
/// 2. Пишет сообщения в консоль в едином читаемом формате.
/// 3. Передаёт plain-text копию строки в зеркало консольного техлога.
///
/// Контракты:
/// 1. Все операции вывода синхронизируются через <see cref="ConsoleSync.Sync"/>.
/// 2. Формат даты и времени берётся из <see cref="ProjectFormats.DateTimeDisplay"/>.
/// 3. Некорректный шаблон форматирования не должен ломать процесс.
/// </remarks>
internal sealed class ConsoleLogger
{
    private const string _unknownComponentName = "unknown";

    private readonly string _name;

    /// <summary>
    /// Создаёт консольный логгер для указанного компонента.
    /// </summary>
    /// <param name="name">Короткое имя компонента.</param>
    public ConsoleLogger(string name)
    {
        _name = string.IsNullOrWhiteSpace(name)
            ? _unknownComponentName
            : name.Trim();
    }

    /// <summary>
    /// Пишет информационное сообщение.
    /// </summary>
    /// <param name="message">Шаблон или готовый текст сообщения.</param>
    /// <param name="args">Аргументы форматирования.</param>
    public void Info(string message, params object[] args) =>
        Write(ConsoleLogLevels.Info, message, args);

    /// <summary>
    /// Пишет предупреждение.
    /// </summary>
    /// <param name="message">Шаблон или готовый текст сообщения.</param>
    /// <param name="args">Аргументы форматирования.</param>
    public void Warn(string message, params object[] args) =>
        Write(ConsoleLogLevels.Warning, message, args);

    /// <summary>
    /// Пишет сообщение об ошибке.
    /// </summary>
    /// <param name="message">Шаблон или готовый текст сообщения.</param>
    /// <param name="args">Аргументы форматирования.</param>
    public void Error(string message, params object[] args) =>
        Write(ConsoleLogLevels.Error, message, args);

    /// <summary>
    /// Формирует одну строку компонентного лога, пишет её в консоль
    /// и передаёт plain-text копию в зеркало техлога.
    /// </summary>
    /// <param name="level">Короткий код уровня сообщения.</param>
    /// <param name="message">Шаблон или готовый текст сообщения.</param>
    /// <param name="args">Аргументы форматирования.</param>
    private void Write(string level, string? message, object[]? args)
    {
        var text = FormatMessage(message, args);
        var timestamp = DateTime.Now.ToString(
            ProjectFormats.DateTimeDisplay,
            CultureInfo.InvariantCulture);

        var escapedTimestamp = Markup.Escape(timestamp);
        var escapedLevel = Markup.Escape(level);
        var escapedName = Markup.Escape(_name);
        var escapedText = Markup.Escape(text);

        var line =
            $"[grey50]{escapedTimestamp}[/] " +
            $"[{GetLevelStyle(level)}]{escapedLevel}[/] " +
            $"[deepskyblue3][[{escapedName}]][/] " +
            $"{escapedText}";

        lock (ConsoleSync.Sync)
        {
            AnsiConsole.MarkupLine(line);
            ConsoleTranscript.WriteLogLine(timestamp, level, _name, text);
        }
    }

    /// <summary>
    /// Безопасно форматирует текст сообщения.
    /// </summary>
    /// <param name="message">Шаблон или готовый текст сообщения.</param>
    /// <param name="args">Аргументы форматирования.</param>
    /// <returns>Готовая строка для вывода в лог.</returns>
    /// <remarks>
    /// Логгер не должен падать из-за некорректного шаблона string.Format.
    /// Если шаблон и аргументы не совпали, возвращается исходное сообщение
    /// с диагностическим хвостом по переданным аргументам.
    /// </remarks>
    private static string FormatMessage(string? message, object[]? args)
    {
        var template = message ?? string.Empty;

        if (args is null || args.Length == 0)
            return template;

        try
        {
            return string.Format(CultureInfo.InvariantCulture, template, args);
        }
        catch (FormatException)
        {
            var safeArgs = string.Join(
                ", ",
                Array.ConvertAll(args, static arg => arg?.ToString() ?? "<null>"));

            return $"{template} [format-error; args: {safeArgs}]";
        }
    }

    /// <summary>
    /// Возвращает стиль Spectre.Console для заданного уровня лога.
    /// </summary>
    /// <param name="level">Короткий код уровня сообщения.</param>
    /// <returns>Строка стиля Spectre.Console.</returns>
    private static string GetLevelStyle(string level) =>
        level switch
        {
            ConsoleLogLevels.Error => "bold white on red",
            ConsoleLogLevels.Warning => "bold black on yellow",
            ConsoleLogLevels.Info => "bold black on grey",
            _ => "bold white on blue"
        };
}

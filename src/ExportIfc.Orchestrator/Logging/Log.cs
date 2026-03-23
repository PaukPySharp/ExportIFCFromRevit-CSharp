using Spectre.Console;

namespace ExportIfc.Logging;

/// <summary>
/// Общие точки входа для форматированного вывода оркестратора.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Централизует создание компонентных логгеров.
/// 2. Выводит общие элементы оформления прогона, не привязанные к одному компоненту.
/// 3. Дублирует эти элементы в зеркало консольного техлога.
///
/// Контракты:
/// 1. Методы этого класса не хранят состояние текущего прогона.
/// 2. Форматированный вывод синхронизируется через <see cref="ConsoleSync.Sync"/>.
/// 3. Rule и Result пишут данные и в консоль, и в зеркало техлога.
/// </remarks>
internal static class Log
{
    /// <summary>
    /// Создаёт логгер для компонента проекта.
    /// </summary>
    /// <param name="component">Короткое имя компонента.</param>
    /// <returns>Экземпляр логгера.</returns>
    public static ConsoleLogger For(string component) => new(component);

    /// <summary>
    /// Выводит визуальный разделитель этапа в консоль
    /// и записывает plain-text строку в зеркало техлога.
    /// </summary>
    /// <param name="title">Название этапа.</param>
    public static void Rule(string? title)
    {
        var safeTitle = title?.Trim() ?? string.Empty;

        lock (ConsoleSync.Sync)
        {
            AnsiConsole.Write(
                new Rule($"[bold cyan]{Markup.Escape(safeTitle)}[/]")
                    .RuleStyle("grey37")
                    .LeftJustified());

            ConsoleTranscript.WriteRuleLine(safeTitle);
        }
    }

    /// <summary>
    /// Выводит итоговую панель завершения в консоль
    /// и записывает plain-text итог в зеркало техлога.
    /// </summary>
    /// <param name="isSuccess">Признак успешного завершения.</param>
    /// <param name="title">Короткий итоговый заголовок.</param>
    /// <param name="details">Дополнительная строка пояснения.</param>
    public static void Result(bool isSuccess, string? title, string? details)
    {
        var color = isSuccess ? "green" : "red";
        var markup =
            $"[bold {color}]{Markup.Escape(title ?? string.Empty)}[/]\n" +
            $"[grey]{Markup.Escape(details ?? string.Empty)}[/]";

        lock (ConsoleSync.Sync)
        {
            AnsiConsole.Write(
                new Panel(new Markup(markup))
                    .Border(BoxBorder.Rounded)
                    .BorderStyle(Style.Parse(color))
                    .Expand());

            ConsoleTranscript.WriteResultLine(isSuccess, title, details);
        }
    }
}
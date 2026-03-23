using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;

using ExportIfc.RevitAddin.Batch.Context;
using ExportIfc.RevitAddin.Logging;

namespace ExportIfc.RevitAddin.Startup.Dialogs;

/// <summary>
/// Session-wide guard UI-шумов Revit для batch-запуска add-in.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Логирует всплывающие dialogs Revit в технический startup-лог.
/// 2. Автоматически удаляет warnings из текущего набора failures,
///    если они не требуют интерактивного участия пользователя.
/// 3. Автоматически подтверждает заранее известные безопасные dialogs
///    по точному <c>DialogId</c>.
/// 4. Даёт узкий fallback для редких окон, которые плохо воспроизводятся,
///    но типично ломают unattended batch-сценарий.
///
/// Контракты:
/// 1. Unknown dialogs по умолчанию не закрываются автоматически.
/// 2. Exact-match правила берутся только из <see cref="RevitUiDialogCatalog.AutoDismissDialogs"/>.
/// 3. Fallback-эвристики должны оставаться узкими и применяться только там,
///    где текст окна устойчиво указывает на безопасный выбор.
/// 4. В <c>FailuresProcessing</c> warnings удаляются даже в том случае,
///    если рядом присутствуют nonwarning failures.
/// 5. Guard не должен сам становиться причиной падения batch-сценария:
///    все внутренние сбои пишутся в startup-лог.
/// </remarks>
internal static class RevitUiNoiseGuard
{
    private static UIControlledApplication? _application;
    private static bool _aggressiveAutoDismissDialogs;

    /// <summary>
    /// Подключает session-wide обработчики UI-шумов Revit.
    /// </summary>
    /// <param name="application">Экземпляр <see cref="UIControlledApplication"/> текущего процесса Revit.</param>
    /// <param name="aggressiveAutoDismissDialogs">
    /// Расширенный режим автозакрытия простых информационных message boxes.
    /// Использовать только в полностью unattended batch-прогонах,
    /// где допустимо автоматически подтверждать безобидные окна с кнопкой OK.
    /// </param>
    /// <remarks>
    /// Метод идемпотентен на уровне процесса Revit:
    /// повторное подключение поверх уже активного guard не выполняется.
    /// </remarks>
    public static void Attach(
        UIControlledApplication application,
        bool aggressiveAutoDismissDialogs = false)
    {
        if (_application is not null)
            return;

        _application = application;
        _aggressiveAutoDismissDialogs = aggressiveAutoDismissDialogs;

        application.DialogBoxShowing += OnDialogBoxShowing;
        application.ControlledApplication.FailuresProcessing += OnFailuresProcessing;
    }

    /// <summary>
    /// Отключает ранее подключённые обработчики UI-шумов Revit.
    /// </summary>
    /// <param name="application">Экземпляр <see cref="UIControlledApplication"/> текущего процесса Revit.</param>
    /// <remarks>
    /// Guard отключается только для того экземпляра приложения,
    /// к которому был подключён ранее.
    /// </remarks>
    public static void Detach(UIControlledApplication application)
    {
        if (!ReferenceEquals(_application, application))
            return;

        application.DialogBoxShowing -= OnDialogBoxShowing;
        application.ControlledApplication.FailuresProcessing -= OnFailuresProcessing;

        _application = null;
        _aggressiveAutoDismissDialogs = false;
    }

    /// <summary>
    /// Обрабатывает момент показа dialog/message/task dialog внутри Revit.
    /// </summary>
    /// <param name="sender">Источник события Revit.</param>
    /// <param name="e">Аргументы показа dialog.</param>
    /// <remarks>
    /// Фаза работы обработчика:
    /// 1. Получает рабочую admin-data директорию текущей batch-сессии.
    /// 2. Пишет в startup-лог факт появления окна, его тип, id и доступный текст.
    /// 3. Пытается подобрать безопасный override:
    ///    сначала по exact-match правилу, затем по узкому fallback.
    /// 4. Если правило найдено, вызывает <c>OverrideResult</c> и пишет результат в лог.
    ///
    /// Само наличие <c>DialogId</c> не означает, что окно можно закрывать автоматически.
    /// Решение принимается только через каталог известных окон
    /// или через ограниченные fallback-эвристики.
    /// </remarks>
    private static void OnDialogBoxShowing(object? sender, DialogBoxShowingEventArgs e)
    {
        if (!TryGetAdminDir(out var adminDir))
            return;

        try
        {
            var dialogId = e.DialogId ?? string.Empty;
            var dialogType = e.GetType().Name;
            var message = TryGetDialogMessage(e);
            var messageForLog = NormalizeForLog(
                message,
                RevitUiDialogCatalog.StartupLogMessageMaxLength);
            var messageSuffix = BuildMessageLogSuffix(messageForLog);

            AddinLogs.WriteStartup(
                adminDir,
                $"Обнаружен UI-диалог. Type={dialogType} | DialogId='{dialogId}'{messageSuffix}");

            if (!TryGetOverride(dialogId, e, message, out var resultCode, out var rule))
                return;

            var accepted = e.OverrideResult(resultCode);

            AddinLogs.WriteStartup(
                adminDir,
                $"Попытка автозакрытия UI-диалога. Rule={rule} | Type={dialogType} | DialogId='{dialogId}' " +
                $"| Result={resultCode} | Accepted={accepted}{messageSuffix}");
        }
        catch (Exception ex)
        {
            AddinLogs.WriteStartup(
                adminDir,
                $"Ошибка в обработчике DialogBoxShowing: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Обрабатывает набор failures, сформированный Revit в текущей фазе документа.
    /// </summary>
    /// <param name="sender">Источник события Revit.</param>
    /// <param name="e">Аргументы обработки failures.</param>
    /// <remarks>
    /// Guard не пытается лечить ошибки модели и не принимает решений по nonwarning failures.
    /// Его зона ответственности здесь уже и прагматичнее:
    /// убрать предупреждения, которые засоряют unattended batch-поток
    /// и не требуют интерактивной развилки сценария.
    /// </remarks>
    private static void OnFailuresProcessing(object? sender, FailuresProcessingEventArgs e)
    {
        if (!TryGetAdminDir(out var adminDir))
            return;

        try
        {
            var accessor = e.GetFailuresAccessor();
            var failures = accessor.GetFailureMessages();

            if (failures is null || failures.Count == 0)
                return;

            var warningCount = 0;
            var nonWarningCount = 0;

            foreach (var failure in failures)
            {
                if (failure.GetSeverity() == FailureSeverity.Warning)
                    warningCount++;
                else
                    nonWarningCount++;
            }

            AddinLogs.WriteStartup(
                adminDir,
                $"Обработка FailuresProcessing. Warnings={warningCount} | NonWarnings={nonWarningCount}");

            if (warningCount == 0)
                return;

            accessor.DeleteAllWarnings();
            e.SetProcessingResult(FailureProcessingResult.Continue);

            AddinLogs.WriteStartup(
                adminDir,
                $"Предупреждения Revit удалены автоматически. Deleted={warningCount}");
        }
        catch (Exception ex)
        {
            AddinLogs.WriteStartup(
                adminDir,
                $"Ошибка в обработчике FailuresProcessing: {ex}");
        }
    }

    /// <summary>
    /// Подбирает код результата для текущего окна Revit.
    /// </summary>
    /// <param name="dialogId">Точный <c>DialogId</c> текущего окна, если Revit его предоставил.</param>
    /// <param name="e">Аргументы показа dialog.</param>
    /// <param name="message">Текст окна, если он доступен для данного типа dialog.</param>
    /// <param name="resultCode">
    /// Подобранный код результата для <c>DialogBoxShowingEventArgs.OverrideResult(int)</c>.
    /// </param>
    /// <param name="rule">Имя сработавшего правила для технического лога.</param>
    /// <returns>
    /// <see langword="true"/>, если найдено безопасное правило автозакрытия;
    /// иначе — <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// Приоритет правил:
    /// 1. Exact-match по <c>DialogId</c>.
    /// 2. Ограниченный fallback по типу окна и его тексту.
    ///
    /// Такой порядок важен, чтобы fallback не перехватывал окно,
    /// для которого уже есть более точное и устойчивое правило.
    /// </remarks>
    private static bool TryGetOverride(
        string dialogId,
        DialogBoxShowingEventArgs e,
        string? message,
        out int resultCode,
        out string rule)
    {
        if (!string.IsNullOrWhiteSpace(dialogId) &&
            RevitUiDialogCatalog.AutoDismissDialogs.TryGetValue(dialogId, out resultCode))
        {
            rule = "whitelist:dialogId";
            return true;
        }

        if (TryGetFallbackOverride(e, message, out resultCode, out rule))
            return true;

        resultCode = 0;
        rule = string.Empty;
        return false;
    }

    /// <summary>
    /// Применяет ограниченные fallback-эвристики для редких, но типовых batch-окон.
    /// </summary>
    /// <param name="e">Аргументы показа dialog.</param>
    /// <param name="message">Текст окна, если он доступен.</param>
    /// <param name="resultCode">Подобранный код результата.</param>
    /// <param name="rule">Имя сработавшего правила для технического лога.</param>
    /// <returns>
    /// <see langword="true"/>, если найден безопасный fallback-override;
    /// иначе — <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// Fallback-эвристики применяются только к окнам,
    /// которые встречаются в batch-сценариях, плохо воспроизводятся
    /// и допускают однозначный безопасный выбор по тексту окна.
    /// </remarks>
    private static bool TryGetFallbackOverride(
        DialogBoxShowingEventArgs e,
        string? message,
        out int resultCode,
        out string rule)
    {
        resultCode = 0;
        rule = string.Empty;

        if (string.IsNullOrWhiteSpace(message))
            return false;

        if (e is TaskDialogShowingEventArgs)
        {
            // Это редкое, но неприятное окно:
            // модель была изменена сторонним updater'ом, которого нет на текущей машине.
            // Для unattended batch-потока безопасный путь здесь — продолжить открытие файла,
            // а не зависать на интерактивном выборе.
            if (LooksLikeMissingThirdPartyUpdater(message))
            {
                resultCode = RevitUiDialogCatalog.TaskDialogCommandLink1;
                rule = "fallback:missing-third-party-updater";
                return true;
            }

            // В штатном случае unresolved references должен закрываться по точному DialogId.
            // Этот fallback нужен только как страховка на случай локализации
            // или нестабильного поведения конкретной сборки Revit.
            if (LooksLikeUnresolvedReferences(message))
            {
                resultCode = RevitUiDialogCatalog.TaskDialogCommandLink2;
                rule = "fallback:unresolved-references";
                return true;
            }

            return false;
        }

        if (e is MessageBoxShowingEventArgs)
        {
            if (!_aggressiveAutoDismissDialogs)
                return false;

            // Для обычных MessageBox часто нет устойчивого DialogId.
            // Поэтому в расширенном режиме автоматически подтверждаются
            // только явно безобидные информационные окна с OK-семантикой.
            if (LooksLikeBenignOkMessageBox(message))
            {
                resultCode = RevitUiDialogCatalog.DialogResultOk;
                rule = "aggressive:messagebox-ok";
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Пытается извлечь текст окна из аргументов Revit.
    /// </summary>
    /// <param name="e">Аргументы показа dialog.</param>
    /// <returns>
    /// Текст окна, если он доступен для данного типа dialog; иначе — <see langword="null"/>.
    /// </returns>
    /// <remarks>
    /// Не все descendants <see cref="DialogBoxShowingEventArgs"/> дают одинаковый доступ
    /// к тексту окна. Guard не предполагает, что сообщение доступно всегда.
    /// </remarks>
    private static string? TryGetDialogMessage(DialogBoxShowingEventArgs e)
    {
        if (e is TaskDialogShowingEventArgs taskDialog)
            return taskDialog.Message;

        if (e is MessageBoxShowingEventArgs messageBox)
            return messageBox.Message;

        return null;
    }

    /// <summary>
    /// Проверяет, соответствует ли текст окну об отсутствующем стороннем updater.
    /// </summary>
    /// <param name="message">Текст dialog.</param>
    /// <returns>
    /// <see langword="true"/>, если текст похож на окно missing third-party updater;
    /// иначе — <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// Проверка использует устойчивые текстовые фрагменты по нескольким локалям.
    /// Exact <c>DialogId</c> для этого сценария может отсутствовать
    /// или различаться между сборками Revit.
    /// </remarks>
    private static bool LooksLikeMissingThirdPartyUpdater(string? message)
    {
        var englishMatch =
            ContainsIgnoreCase(message, "third-party updater") ||
            ContainsIgnoreCase(message, "third party updater") ||
            ContainsIgnoreCase(message, "missing third");

        var russianMatch =
            ContainsIgnoreCase(message, "сторонн") &&
            ContainsIgnoreCase(message, "обновл");

        var germanMatch =
            ContainsIgnoreCase(message, "dritt") &&
            ContainsIgnoreCase(message, "aktual");

        return englishMatch || russianMatch || germanMatch;
    }

    /// <summary>
    /// Проверяет, соответствует ли текст окну про неразрешённые ссылки модели.
    /// </summary>
    /// <param name="message">Текст dialog.</param>
    /// <returns>
    /// <see langword="true"/>, если текст похож на unresolved references;
    /// иначе — <see langword="false"/>.
    /// </returns>
    private static bool LooksLikeUnresolvedReferences(string? message)
    {
        return ContainsIgnoreCase(message, "unresolved reference")
               || ContainsIgnoreCase(message, "unresolved references")
               || ContainsIgnoreCase(message, "manage links")
               || (ContainsIgnoreCase(message, "неразреш")
                   && ContainsIgnoreCase(message, "ссыл"));
    }

    /// <summary>
    /// Проверяет, является ли MessageBox безобидным информационным окном с OK-семантикой.
    /// </summary>
    /// <param name="message">Текст MessageBox.</param>
    /// <returns>
    /// <see langword="true"/>, если окно можно безопасно закрыть кодом OK;
    /// иначе — <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// Этот метод используется только в агрессивном режиме.
    /// В обычном batch-потоке guard не должен угадывать поведение
    /// произвольных message boxes по расплывчатым текстовым признакам.
    /// </remarks>
    private static bool LooksLikeBenignOkMessageBox(string? message)
    {
        return ContainsIgnoreCase(message, "completed")
               || ContainsIgnoreCase(message, "finished")
               || ContainsIgnoreCase(message, "success")
               || ContainsIgnoreCase(message, "успеш")
               || ContainsIgnoreCase(message, "заверш");
    }

    /// <summary>
    /// Выполняет нечувствительный к регистру поиск фрагмента в строке.
    /// </summary>
    /// <param name="value">Исходная строка.</param>
    /// <param name="token">Искомый фрагмент.</param>
    /// <returns>
    /// <see langword="true"/>, если фрагмент найден; иначе — <see langword="false"/>.
    /// </returns>
    private static bool ContainsIgnoreCase(string? value, string token)
    {
        if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(token))
            return false;

        return value!.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>
    /// Подготавливает текст dialog для безопасной записи в startup-лог.
    /// </summary>
    /// <param name="message">Исходный текст окна.</param>
    /// <param name="maxLen">Максимально допустимая длина сообщения в логе.</param>
    /// <returns>
    /// Нормализованный однострочный текст, ограниченный по длине.
    /// </returns>
    /// <remarks>
    /// Метод убирает переводы строк, чтобы один dialog не разрывал структуру техлога
    /// на несколько строк и не ухудшал разбор последовательности событий.
    /// </remarks>
    private static string NormalizeForLog(string? message, int maxLen)
    {
        if (string.IsNullOrWhiteSpace(message))
            return string.Empty;

        var normalized = message!
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();

        if (normalized.Length <= maxLen)
            return normalized;

        return normalized.Substring(0, maxLen) + "...";
    }

    /// <summary>
    /// Формирует логовый хвост с текстом dialog.
    /// </summary>
    /// <param name="messageForLog">Уже нормализованный текст окна.</param>
    /// <returns>
    /// Готовый суффикс для строки техлога или пустую строку,
    /// если текста у dialog нет.
    /// </returns>
    private static string BuildMessageLogSuffix(string messageForLog) =>
        string.IsNullOrEmpty(messageForLog)
            ? string.Empty
            : $" | Message='{messageForLog}'";

    /// <summary>
    /// Пытается получить рабочую admin-data директорию текущей batch-сессии.
    /// </summary>
    /// <param name="adminDir">Найденная директория admin-data.</param>
    /// <returns>
    /// <see langword="true"/>, если директория доступна;
    /// иначе — <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// Метод читает каталог логов из текущего batch-окружения.
    /// </remarks>
    private static bool TryGetAdminDir(out string adminDir)
    {
        adminDir = string.Empty;

        try
        {
            var environment = BatchRunEnvironmentSnapshot.Read();
            if (string.IsNullOrWhiteSpace(environment.DirAdminData))
                return false;

            adminDir = environment.DirAdminData;
            return true;
        }
        catch
        {
            return false;
        }
    }
}

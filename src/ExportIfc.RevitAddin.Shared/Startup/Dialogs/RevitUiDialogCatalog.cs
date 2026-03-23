namespace ExportIfc.RevitAddin.Startup.Dialogs;

/// <summary>
/// Каталог известных dialog-контрактов Revit для batch-сценариев add-in.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Централизует коды результатов <c>OverrideResult</c> для стандартных и task dialogs Revit.
/// 2. Централизует точные <c>DialogId</c>, которые add-in считает безопасными для автообработки.
/// 3. Убирает магические числа и строковые литералы из <see cref="RevitUiNoiseGuard"/>.
/// 4. Делает список поддерживаемых UI-сценариев отдельно находимым при сопровождении.
///
/// Контракты:
/// 1. В каталог попадают только те dialogs, для которых заранее выбран безопасный ответ
///    в типовом unattended batch-потоке.
/// 2. Каталог не пытается покрыть все возможные окна Revit.
/// 3. Если для окна нет записи в каталоге, <see cref="RevitUiNoiseGuard"/> не должен
///    автоматически принимать решение только по факту наличия <c>DialogId</c>.
/// 4. Коды результатов относятся к контракту Revit API <c>DialogBoxShowingEventArgs.OverrideResult</c>
///    и должны использоваться только в этом контексте.
/// </remarks>
internal static class RevitUiDialogCatalog
{
    /// <summary>
    /// Код стандартной кнопки OK.
    /// </summary>
    public const int DialogResultOk = 1;

    /// <summary>
    /// Код стандартной кнопки No.
    /// </summary>
    public const int DialogResultNo = 7;

    /// <summary>
    /// Код стандартной кнопки Close.
    /// </summary>
    public const int DialogResultClose = 8;

    /// <summary>
    /// Код первой command-link кнопки task dialog.
    /// </summary>
    public const int TaskDialogCommandLink1 = 1001;

    /// <summary>
    /// Код второй command-link кнопки task dialog.
    /// </summary>
    public const int TaskDialogCommandLink2 = 1002;

    /// <summary>
    /// Код третьей command-link кнопки task dialog.
    /// </summary>
    public const int TaskDialogCommandLink3 = 1003;

    /// <summary>
    /// Максимальная длина текста dialog, сохраняемого в startup-лог.
    /// </summary>
    /// <remarks>
    /// Лимит нужен, чтобы редкие длинные сообщения Revit не раздували техлог
    /// и не ухудшали читаемость последовательности событий batch-сессии.
    /// </remarks>
    public const int StartupLogMessageMaxLength = 420;

    /// <summary>
    /// DialogId окна про worksets, находящиеся в состоянии at risk.
    /// </summary>
    public const string ResolveAtRiskWorksetsDialogId = "TaskDialog_Resolve_At_Risk_Worksets";

    /// <summary>
    /// DialogId окна про неразрешённые ссылки модели.
    /// </summary>
    public const string UnresolvedReferencesDialogId = "TaskDialog_Unresolved_References";

    /// <summary>
    /// DialogId окна про локальные изменения, не синхронизированные с центральной моделью.
    /// </summary>
    public const string LocalChangesNotSynchronizedWithCentralDialogId =
        "TaskDialog_Local_Changes_Not_Synchronized_With_Central";

    /// <summary>
    /// DialogId окна про несохранённые изменения.
    /// </summary>
    public const string ChangesNotSavedDialogId = "TaskDialog_Changes_Not_Saved";

    /// <summary>
    /// DialogId окна про невозможность создать локальную копию.
    /// </summary>
    public const string CannotCreateLocalFileDialogId = "TaskDialog_Cannot_Create_Local_File";

    /// <summary>
    /// DialogId окна про недоступную центральную модель.
    /// </summary>
    public const string CannotFindCentralModelDialogId = "TaskDialog_Cannot_Find_Central_Model";

    /// <summary>
    /// DialogId окна про невозможность сохранения во время показа другого сообщения.
    /// </summary>
    public const string CannotSaveWhileMessageDisplayedDialogId =
        "TaskDialog_Cannot_Save_While_Message_Displayed";

    /// <summary>
    /// DialogId окна подтверждения сохранения файла.
    /// </summary>
    public const string SaveFileDialogId = "TaskDialog_Save_File";

    /// <summary>
    /// Точные <c>DialogId</c>, которые add-in может обрабатывать без участия пользователя.
    /// </summary>
    /// <remarks>
    /// Здесь лежат только exact-match правила.
    /// Для редких сценариев, где <c>DialogId</c> нестабилен или заранее неизвестен,
    /// <see cref="RevitUiNoiseGuard"/> использует отдельные fallback-правила по типу и тексту окна.
    /// </remarks>
    public static readonly IReadOnlyDictionary<string, int> AutoDismissDialogs =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            // Открытие модели / links / worksharing.
            [ResolveAtRiskWorksetsDialogId] = DialogResultClose,
            [UnresolvedReferencesDialogId] = TaskDialogCommandLink2,
            [CannotCreateLocalFileDialogId] = DialogResultClose,
            [CannotFindCentralModelDialogId] = DialogResultClose,

            // Закрытие / сохранение / локальные изменения.
            [LocalChangesNotSynchronizedWithCentralDialogId] = TaskDialogCommandLink3,
            [ChangesNotSavedDialogId] = TaskDialogCommandLink3,
            [CannotSaveWhileMessageDisplayedDialogId] = DialogResultClose,
            [SaveFileDialogId] = DialogResultNo,
        };
}
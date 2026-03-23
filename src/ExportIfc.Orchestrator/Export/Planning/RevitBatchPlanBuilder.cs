using ExportIfc.History;
using ExportIfc.Models;
using ExportIfc.Revit;

namespace ExportIfc.Export.Planning;

/// <summary>
/// Сервис построения плана пакетной обработки моделей по версиям Revit.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Определяет major-версию Revit для каждой модели.
/// 2. Формирует диагностические списки для моделей,
///    у которых версия не определена или не может быть сопоставлена
///    с доступными версиями запуска Revit.
/// 3. Группирует модели по версиям запуска Revit
///    и возвращает готовый план пакетной обработки.
///
/// Контракты:
/// 1. Если версия модели не определена, модель попадает в <see cref="RevitBatchPlan.VersionNotFound"/>.
/// 2. Если версия модели выше всех доступных версий запуска,
///    модель попадает в <see cref="RevitBatchPlan.VersionTooNew"/>.
/// 3. Для модели выбирается первая доступная версия запуска Revit,
///    которая не ниже версии самой модели.
/// 4. Для моделей младше минимально поддерживаемой версии
///    используется минимальная доступная версия запуска.
/// 5. Для моделей с определённой версией вызывается обновление истории.
/// </remarks>
internal sealed class RevitBatchPlanBuilder
{
    private readonly Func<string, int?> _detectRevitMajor;

    /// <summary>
    /// Создаёт сервис построения batch-плана
    /// с рабочим определением версии RVT-модели.
    /// </summary>
    public RevitBatchPlanBuilder()
        : this(RevitVersionDetector.TryGetRevitMajor)
    {
    }

    /// <summary>
    /// Создаёт сервис построения batch-плана
    /// с явно переданной функцией определения версии RVT-модели.
    /// </summary>
    /// <param name="detectRevitMajor">
    /// Функция определения major-версии Revit по пути к RVT.
    /// </param>
    internal RevitBatchPlanBuilder(Func<string, int?> detectRevitMajor)
    {
        ArgumentNullException.ThrowIfNull(detectRevitMajor);
        _detectRevitMajor = detectRevitMajor;
    }

    /// <summary>
    /// Строит план пакетной обработки.
    /// </summary>
    /// <param name="models">Модели, выбранные для выгрузки.</param>
    /// <param name="supportedRevitVersions">Поддерживаемые версии Revit.</param>
    /// <param name="history">Менеджер рабочей истории состояний моделей.</param>
    /// <returns>Готовый план пакетной обработки.</returns>
    public RevitBatchPlan Build(
        IEnumerable<RevitModel> models,
        IReadOnlyList<int> supportedRevitVersions,
        HistoryManager history)
    {
        ArgumentNullException.ThrowIfNull(models);
        ArgumentNullException.ThrowIfNull(supportedRevitVersions);
        ArgumentNullException.ThrowIfNull(history);

        // Нормализация защищает планировщик от прямых вызовов с неотсортированным
        // или дублирующимся списком версий, хотя штатный загрузчик настроек уже
        // отдаёт упорядоченный набор.
        var availableLaunchVersions = supportedRevitVersions
            .Distinct()
            .OrderBy(version => version)
            .ToArray();

        if (availableLaunchVersions.Length == 0)
            throw new InvalidOperationException("В настройках не задан ни один поддерживаемый Revit.");

        var byVersion = new Dictionary<int, List<RevitModel>>();
        var versionNotFound = new List<string>();
        var versionTooNew = new List<string>();

        foreach (var model in models)
        {
            var detectedRevitMajor = _detectRevitMajor(model.RvtPath);
            if (detectedRevitMajor is null)
            {
                versionNotFound.Add(model.RvtPath);
                continue;
            }

            // История обновляется только после того,
            // как версия модели успешно определена по RVT-файлу.
            history.UpdateRecord(model);

            var launchRevitMajor = ResolveLaunchRevitMajor(
                availableLaunchVersions,
                detectedRevitMajor.Value);

            if (launchRevitMajor is null)
            {
                versionTooNew.Add(
                    BuildTooNewMessage(
                        model.RvtPath,
                        detectedRevitMajor.Value,
                        availableLaunchVersions));
                continue;
            }

            if (!byVersion.TryGetValue(launchRevitMajor.Value, out var list))
            {
                list = new List<RevitModel>();
                byVersion[launchRevitMajor.Value] = list;
            }

            list.Add(model);
        }

        var batches = byVersion
            .OrderBy(pair => pair.Key)
            .Select(pair => new RevitBatchPlanItem(
                pair.Key,
                pair.Value
                    .OrderBy(model => model.RvtPath, StringComparer.OrdinalIgnoreCase)
                    .ToArray()))
            .ToArray();

        return new RevitBatchPlan(
            batches,
            versionNotFound
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            versionTooNew
                .OrderBy(message => message, StringComparer.OrdinalIgnoreCase)
                .ToArray());
    }

    /// <summary>
    /// Определяет major-версию Revit,
    /// в которой должна запускаться обработка модели.
    /// </summary>
    /// <param name="supportedRevitVersions">Поддерживаемые версии Revit.</param>
    /// <param name="detectedRevitMajor">Определённая версия модели.</param>
    /// <returns>
    /// Major-версия запуска Revit либо <see langword="null"/>,
    /// если версия модели выше всех доступных версий запуска.
    /// </returns>
    /// <remarks>
    /// Для модели выбирается первая доступная версия запуска Revit,
    /// которая не ниже версии самой модели.
    /// </remarks>
    private static int? ResolveLaunchRevitMajor(
        IReadOnlyList<int> supportedRevitVersions,
        int detectedRevitMajor)
    {
        foreach (var supportedVersion in supportedRevitVersions)
        {
            if (supportedVersion >= detectedRevitMajor)
                return supportedVersion;
        }

        return null;
    }

    /// <summary>
    /// Формирует диагностическое сообщение
    /// для модели со слишком новой версией Revit.
    /// </summary>
    /// <param name="rvtPath">Путь к модели RVT.</param>
    /// <param name="detectedRevitMajor">Определённая версия модели.</param>
    /// <param name="supportedRevitVersions">Поддерживаемые версии Revit.</param>
    /// <returns>Готовое диагностическое сообщение.</returns>
    private static string BuildTooNewMessage(
        string rvtPath,
        int detectedRevitMajor,
        IReadOnlyList<int> supportedRevitVersions)
    {
        return string.Format(
            "{0} | версия Revit {1} выше всех доступных версий запуска: {2}",
            rvtPath,
            detectedRevitMajor,
            string.Join(", ", supportedRevitVersions));
    }
}

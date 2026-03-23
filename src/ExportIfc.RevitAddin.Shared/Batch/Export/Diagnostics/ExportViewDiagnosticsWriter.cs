using Autodesk.Revit.DB;

using ExportIfc.RevitAddin.Logging;
using ExportIfc.RevitAddin.Revit;

namespace ExportIfc.RevitAddin.Batch.Export.Diagnostics;

/// <summary>
/// Записывает техническую диагностику выбранного export-view.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Сконцентрировать диагностику export-view в одном месте.
/// 2. Убрать низкоуровневое чтение параметров вида из orchestration-кода.
/// 3. Вызывать расширенную диагностику только на проблемных сценариях,
///    чтобы не раздувать happy-path startup-лог.
///
/// Контракты:
/// 1. Класс ничего не изменяет в документе и только пишет диагностику.
/// 2. Ошибки чтения отдельных параметров не пробрасываются наружу.
/// 3. Полная диагностика export-view пишется только для проблемных экспортов.
/// </remarks>
internal static class ExportViewDiagnosticsWriter
{
    private const string _noneValue = "<none>";
    private const string _unknownValue = "<unknown>";

    /// <summary>
    /// Пишет расширенную диагностику проблемного export-view.
    /// </summary>
    /// <param name="document">Открытый документ Revit.</param>
    /// <param name="exportView">Выбранный 3D-вид для экспорта.</param>
    /// <param name="dirAdminData">Рабочий каталог admin-data текущего запуска.</param>
    /// <remarks>
    /// Метод вызывается не на каждый успешный экспорт, а только тогда,
    /// когда есть повод дополнительно объяснить поведение IFC-выгрузки.
    /// </remarks>
    public static void WriteForProblemExport(
        Document document,
        View3D exportView,
        string dirAdminData)
    {
        var viewId = RevitElementIds.ToLogValue(exportView.Id);

        var templateName = GetElementDisplayName(document, exportView.ViewTemplateId);
        var phaseName = GetViewParameterElementDisplayName(document, exportView, BuiltInParameter.VIEW_PHASE);
        var phaseFilterName = GetViewParameterElementDisplayName(document, exportView, BuiltInParameter.VIEW_PHASE_FILTER);

        var visibleElements = TryCountVisibleElements(document, exportView, out var countError);
        var sectionBoxSuffix = exportView.IsSectionBoxActive ? " | SectionBoxActive=True" : string.Empty;

        AddinLogs.WriteStartup(
            dirAdminData,
            $"Диагностика проблемного вида экспорта. ViewId={viewId} | Template='{templateName}' | VisibleElements={visibleElements} | Phase='{phaseName}' | PhaseFilter='{phaseFilterName}'{sectionBoxSuffix}");

        if (!string.IsNullOrWhiteSpace(countError))
        {
            AddinLogs.WriteStartup(
                dirAdminData,
                $"Не удалось корректно посчитать видимые элементы вида экспорта. ViewId={viewId} | Error={countError}");
        }
    }

    /// <summary>
    /// Пытается посчитать количество видимых элементов в виде экспорта.
    /// </summary>
    /// <param name="document">Открытый документ Revit.</param>
    /// <param name="exportView">Выбранный 3D-вид для экспорта.</param>
    /// <param name="error">Текст ошибки подсчёта, если она возникла.</param>
    /// <returns>Количество элементов в пределах view collector или -1 при ошибке.</returns>
    private static int TryCountVisibleElements(
        Document document,
        View3D exportView,
        out string? error)
    {
        error = null;

        try
        {
            return new FilteredElementCollector(document, exportView.Id)
                .WhereElementIsNotElementType()
                .ToElementIds()
                .Count;
        }
        catch (Exception ex)
        {
            error = ex.ToString();
            return -1;
        }
    }

    /// <summary>
    /// Возвращает человекочитаемое имя элемента параметра вида.
    /// </summary>
    /// <param name="document">Открытый документ Revit.</param>
    /// <param name="exportView">Вид, из которого читается параметр.</param>
    /// <param name="parameterId">Идентификатор встроенного параметра.</param>
    /// <returns>
    /// Имя элемента параметра, <c>&lt;none&gt;</c> при отсутствии значения
    /// или <c>&lt;unknown&gt;</c>, если элемент не удалось разрешить.
    /// </returns>
    private static string GetViewParameterElementDisplayName(
        Document document,
        View3D exportView,
        BuiltInParameter parameterId)
    {
        return GetElementDisplayName(
            document,
            TryGetViewParameterElementId(exportView, parameterId));
    }

    /// <summary>
    /// Возвращает человекочитаемое имя элемента документа.
    /// </summary>
    /// <param name="document">Открытый документ Revit.</param>
    /// <param name="elementId">Идентификатор элемента.</param>
    /// <returns>
    /// Имя элемента, <c>&lt;none&gt;</c> при отсутствии значения
    /// или <c>&lt;unknown&gt;</c>, если элемент не удалось разрешить.
    /// </returns>
    private static string GetElementDisplayName(
        Document document,
        ElementId? elementId)
    {
        if (elementId is null || elementId == ElementId.InvalidElementId)
            return _noneValue;

        return TryGetElementName(document, elementId) ?? _unknownValue;
    }

    /// <summary>
    /// Пытается получить <see cref="Autodesk.Revit.DB.ElementId"/> параметра вида.
    /// </summary>
    /// <param name="exportView">Вид, из которого читается параметр.</param>
    /// <param name="parameterId">Идентификатор встроенного параметра.</param>
    /// <returns>Идентификатор элемента параметра или <see langword="null"/>.</returns>
    private static ElementId? TryGetViewParameterElementId(
        View3D exportView,
        BuiltInParameter parameterId)
    {
        try
        {
            var parameter = exportView.get_Parameter(parameterId);
            if (parameter is null)
                return null;

            return parameter.AsElementId();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Пытается получить имя элемента документа по его идентификатору.
    /// </summary>
    /// <param name="document">Открытый документ Revit.</param>
    /// <param name="elementId">Идентификатор элемента.</param>
    /// <returns>Имя элемента или <see langword="null"/>.</returns>
    private static string? TryGetElementName(Document document, ElementId elementId)
    {
        try
        {
            return document.GetElement(elementId)?.Name;
        }
        catch
        {
            return null;
        }
    }
}

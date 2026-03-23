using System.Globalization;

using Autodesk.Revit.DB;

namespace ExportIfc.RevitAddin.Revit;

/// <summary>
/// Платформенно-зависимые операции с <see cref="Autodesk.Revit.DB.ElementId"/> для ветки Net8.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Изолировать различия API <see cref="Autodesk.Revit.DB.ElementId"/> между платформенными ветками add-in.
/// 2. Давать shared-слою готовое строковое представление идентификатора для логирования.
///
/// Контракты:
/// 1. Метод возвращает значение идентификатора в invariant-формате.
/// 2. Класс используется только как узкий платформенный helper и не хранит состояние.
/// </remarks>
internal static class RevitElementIds
{
    /// <summary>
    /// Возвращает строковое значение идентификатора для логирования.
    /// </summary>
    /// <param name="elementId">Идентификатор элемента Revit.</param>
    /// <returns>Строковое представление идентификатора.</returns>
    public static string ToLogValue(ElementId elementId)
    {
        if (elementId is null)
            throw new ArgumentNullException(nameof(elementId));

        return elementId.Value.ToString(CultureInfo.InvariantCulture);
    }
}

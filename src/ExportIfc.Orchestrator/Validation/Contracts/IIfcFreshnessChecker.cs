using ExportIfc.Models;

namespace ExportIfc.Validation;

/// <summary>
/// Контракт проверки актуальности IFC-файлов для модели Revit.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Определяет, требуется ли повторная выгрузка IFC для модели Revit.
/// 2. Разводит проверки по направлению с маппингом и по направлению без маппинга.
///
/// Контракты:
/// 1. Проверка с маппингом и без маппинга может иметь разную семантику
///    для отсутствующего ожидаемого IFC-пути.
/// 2. Реализация интерфейса возвращает только признак актуальности
///    и не изменяет модель или файловую систему.
/// </remarks>
public interface IIfcFreshnessChecker
{
    /// <summary>
    /// Проверяет актуальность IFC по направлению с маппингом.
    /// </summary>
    /// <param name="model">Модель Revit для проверки.</param>
    /// <returns>
    /// <see langword="true"/>, если IFC по направлению с маппингом считается актуальным;
    /// иначе <see langword="false"/>.
    /// </returns>
    bool IsIfcUpToDateMapping(RevitModel model);

    /// <summary>
    /// Проверяет актуальность IFC по направлению без маппинга.
    /// </summary>
    /// <param name="model">Модель Revit для проверки.</param>
    /// <returns>
    /// <see langword="true"/>, если IFC по направлению без маппинга считается актуальным;
    /// иначе <see langword="false"/>.
    /// </returns>
    bool IsIfcUpToDateNoMap(RevitModel model);
}
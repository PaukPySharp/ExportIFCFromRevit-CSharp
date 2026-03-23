namespace ExportIfc.RevitAddin.Config.Ifc;

/// <summary>
/// Ключи JSON-контракта IFC-конфигурации, используемые add-in при подготовке
/// настроек для <c>IFCExportConfiguration</c>.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Централизует имена полей IFC JSON-конфига.
/// 2. Убирает строковые литералы из платформенных фабрик IFC-опций.
/// 3. Делает JSON-контракт находимым в одном месте.
///
/// Контракты:
/// 1. Класс содержит только имена полей IFC JSON-конфигурации.
/// 2. Эти ключи используются и в ветке Net48, и в ветке Net8.
/// </remarks>
internal static class IfcConfigJsonKeys
{
    /// <summary>
    /// Ключ идентификатора активной фазы экспорта.
    /// </summary>
    public const string ActivePhaseId = "ActivePhaseId";

    /// <summary>
    /// Ключ вложенного блока настроек классификации.
    /// </summary>
    public const string ClassificationSettings = "ClassificationSettings";

    /// <summary>
    /// Ключ даты редакции классификации.
    /// </summary>
    public const string ClassificationEditionDate = "ClassificationEditionDate";

    /// <summary>
    /// Ключ вложенного блока адреса проекта.
    /// </summary>
    public const string ProjectAddress = "ProjectAddress";
}
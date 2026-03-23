using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

using Autodesk.Revit.DB;
using BIM.IFC.Export.UI;

using ExportIfc.Config;
using ExportIfc.IO;
using ExportIfc.RevitAddin.Config.Ifc;

namespace ExportIfc.RevitAddin.Revit;

/// <summary>
/// Строит <see cref="IFCExportOptions"/> из JSON-конфигурации IFC
/// и txt-файла сопоставления категорий Revit и классов IFC.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Прочитать JSON настроек IFC.
/// 2. Нормализовать значения, формат которых ожидает <see cref="IFCExportConfiguration"/>.
/// 3. Подготовить итоговый экземпляр <see cref="IFCExportOptions"/>.
///
/// Контракты:
/// 1. JSON читается в UTF-8.
/// 2. Поле ClassificationEditionDate приводится к <see cref="DateTime"/>.
/// 3. Вложенные объекты, используемые конфигурацией, приводятся
///    к <see cref="Dictionary{TKey,TValue}"/>.
/// </remarks>
internal sealed class IfcExportOptionsFactory : IIfcExportOptionsFactory
{
    private static readonly JavaScriptSerializer _serializer = new();

    /// <summary>
    /// Создаёт и настраивает <see cref="IFCExportOptions"/> для заданного 3D-вида.
    /// </summary>
    /// <param name="document">Открытый документ Revit, из которого выполняется экспорт.</param>
    /// <param name="ifcClassMappingFile">Путь к txt-файлу сопоставления категорий Revit и классов IFC.</param>
    /// <param name="configJsonPath">Путь к JSON-файлу настроек IFC.</param>
    /// <param name="viewId">Идентификатор 3D-вида для экспорта.</param>
    /// <returns>Готовые опции экспорта IFC.</returns>
    public IFCExportOptions Create(
        Document document,
        string ifcClassMappingFile,
        string configJsonPath,
        ElementId viewId)
    {
        if (document is null)
            throw new ArgumentNullException(nameof(document));

        var fullIfcClassMappingFile = FileSystemEx.NormalizeExistingFilePath(
            ifcClassMappingFile,
            "файл сопоставления категорий Revit и классов IFC");

        var fullConfigJsonPath = FileSystemEx.NormalizeExistingFilePath(
            configJsonPath,
            "JSON-файл настроек IFC");

        var configData = LoadChangeConfig(fullConfigJsonPath);

        var configuration = IFCExportConfiguration.CreateDefaultConfiguration();
        configuration.DeserializeFromJson(configData, _serializer);

        var options = new IFCExportOptions
        {
            FamilyMappingFile = fullIfcClassMappingFile
        };

        /*
        Аварийный обход для диагностики export-конфигурации.

        Строка ниже принудительно отключает фазу, полученную из JSON,
        и передаёт в exporter значение "без активной фазы".
        Блок сохранён рядом с вызовом UpdateOptions, потому что именно здесь
        конфигурация переводится в итоговый набор IFC-опций.

        Используется только как локальный артефакт для временной проверки
        влияния настройки фазы на результат экспорта.

        //configuration.ActivePhaseId = ElementId.InvalidElementId.IntegerValue;
        */

        UpdateOptionsWithIfcUiDocumentContext(
            document,
            configuration,
            options,
            viewId);

        return options;
    }

    /// <summary>
    /// Загружает JSON-конфигурацию IFC и нормализует поля,
    /// которые ожидаются <see cref="IFCExportConfiguration"/>.
    /// </summary>
    /// <param name="configJsonPath">Путь к JSON-файлу настроек IFC.</param>
    /// <returns>Подготовленный корневой словарь конфигурации.</returns>
    /// <exception cref="System.InvalidOperationException">
    /// Выбрасывается, если корень JSON не является объектом.
    /// </exception>
    private static IDictionary<string, object> LoadChangeConfig(string configJsonPath)
    {
        var json = File.ReadAllText(configJsonPath, ProjectEncodings.Utf8NoBom);

        if (_serializer.DeserializeObject(json) is not Dictionary<string, object> root)
        {
            throw new InvalidOperationException(
                $"JSON настроек IFC должен содержать корневой объект: {configJsonPath}");
        }

        NormalizeClassificationEditionDate(root);
        NormalizeNestedDictionary(root, IfcConfigJsonKeys.ClassificationSettings);
        NormalizeNestedDictionary(root, IfcConfigJsonKeys.ProjectAddress);

        return root;
    }

    /// <summary>
    /// Приводит поле ClassificationEditionDate к типу <see cref="DateTime"/>,
    /// если оно пришло строкой с миллисекундами Unix Epoch.
    /// </summary>
    /// <param name="root">Корневой словарь конфигурации.</param>
    private static void NormalizeClassificationEditionDate(Dictionary<string, object> root)
    {
        if (!TryGetDictionary(root, IfcConfigJsonKeys.ClassificationSettings, out var classificationSettings))
            return;

        if (!classificationSettings.TryGetValue(IfcConfigJsonKeys.ClassificationEditionDate, out var rawValue))
            return;

        if (rawValue is not string rawDate)
            return;

        var match = Regex.Match(rawDate, IfcConfigDefaults.UnixEpochMillisecondsPattern);
        var millis = match.Success && long.TryParse(match.Value, out var parsedMillis)
            ? parsedMillis
            : 0L;

        classificationSettings[IfcConfigJsonKeys.ClassificationEditionDate] =
            IfcConfigDefaults.UnixEpochStart.AddMilliseconds(millis);
    }

    /// <summary>
    /// Гарантирует, что вложенное значение конфигурации представлено как словарь.
    /// </summary>
    /// <param name="root">Корневой словарь конфигурации.</param>
    /// <param name="key">Имя вложенного объекта.</param>
    private static void NormalizeNestedDictionary(
        Dictionary<string, object> root,
        string key)
    {
        _ = TryGetDictionary(root, key, out _);
    }

    /// <summary>
    /// Пытается получить вложенный словарь по ключу.
    /// </summary>
    /// <param name="root">Корневой словарь конфигурации.</param>
    /// <param name="key">Имя вложенного объекта.</param>
    /// <param name="dictionary">Найденный вложенный словарь.</param>
    /// <returns>
    /// <see langword="true"/>, если значение существует и представлено словарём;
    /// иначе <see langword="false"/>.
    /// </returns>
    private static bool TryGetDictionary(
        Dictionary<string, object> root,
        string key,
        out Dictionary<string, object> dictionary)
    {
        dictionary = null!;

        if (!root.TryGetValue(key, out var value) || value is null)
            return false;

        if (value is Dictionary<string, object> exactDictionary)
        {
            dictionary = exactDictionary;
            return true;
        }

        if (value is IDictionary<string, object> genericDictionary)
        {
            dictionary = new Dictionary<string, object>(genericDictionary);
            root[key] = dictionary;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Подготавливает IFC-опции внутри UI-контекста exporter'а.
    /// </summary>
    /// <param name="document">Открытый документ Revit.</param>
    /// <param name="configuration">Конфигурация IFC после загрузки JSON.</param>
    /// <param name="options">Итоговые IFC-опции экспорта.</param>
    /// <param name="viewId">Идентификатор export-view.</param>
    /// <exception cref="System.InvalidOperationException">
    /// Выбрасывается, если у exporter'а недоступно статическое свойство
    /// <c>IFCCommandOverrideApplication.TheDocument</c>.
    /// </exception>
    private static void UpdateOptionsWithIfcUiDocumentContext(
        Document document,
        IFCExportConfiguration configuration,
        IFCExportOptions options,
        ElementId viewId)
    {
        var property = typeof(IFCCommandOverrideApplication).GetProperty(
            "TheDocument",
            BindingFlags.Static | BindingFlags.Public);

        var getter = property?.GetGetMethod();
        var setter = property?.GetSetMethod(true);

        if (property is null || getter is null || setter is null)
        {
            throw new InvalidOperationException(
                "Не удалось получить доступ к IFCCommandOverrideApplication.TheDocument.");
        }

        var previousDocument = getter.Invoke(null, null) as Document;

        try
        {
            // Старый IFC exporter читает текущий документ через static state,
            // а не только из аргументов UpdateOptions.
            setter.Invoke(null, new object?[] { document });

            // После подстановки документа exporter получает тот контекст,
            // который обычно уже существует при запуске из штатного UI.
            configuration.UpdateOptions(options, viewId);
        }
        finally
        {
            // Статическое состояние нельзя оставлять протекшим в следующий экспорт.
            setter.Invoke(null, new object?[] { previousDocument });
        }
    }
}

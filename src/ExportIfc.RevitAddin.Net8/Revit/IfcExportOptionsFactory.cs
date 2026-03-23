using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

using Autodesk.Revit.DB;
using BIM.IFC.Export.UI;

using ExportIfc.Config;
using ExportIfc.IO;
using ExportIfc.RevitAddin.Config.Ifc;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ExportIfc.RevitAddin.Revit;

/// <summary>
/// Строит <see cref="IFCExportOptions"/> из JSON-конфигурации IFC
/// и txt-файла сопоставления категорий Revit и классов IFC.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Прочитать JSON настроек IFC.
/// 2. Нормализовать значения, формат которых ожидает <see cref="IFCExportConfiguration"/>.
/// 3. Импортировать и активировать шаблон сопоставления категорий Revit и классов IFC.
/// 4. Подготовить итоговый экземпляр <see cref="IFCExportOptions"/>.
///
/// Контракты:
/// 1. JSON читается в UTF-8.
/// 2. Поле ClassificationEditionDate приводится к <see cref="DateTime"/>.
/// 3. Вложенные объекты конфигурации приводятся к <see cref="JObject"/>.
/// 4. Шаблон сопоставления категорий Revit и классов IFC применяется только к modifiable-документу.
/// </remarks>
internal sealed class IfcExportOptionsFactory : IIfcExportOptionsFactory
{
    private static readonly JsonSerializer _serializer = JsonSerializer.CreateDefault();

    /// <summary>
    /// Создаёт и настраивает <see cref="IFCExportOptions"/> для заданного 3D-вида.
    /// </summary>
    /// <param name="document">Открытый документ Revit, из которого выполняется экспорт.</param>
    /// <param name="ifcClassMappingFile">Путь к txt-файлу сопоставления категорий Revit и классов IFC.</param>
    /// <param name="configJsonPath">Путь к JSON-файлу настроек IFC.</param>
    /// <param name="viewId">Идентификатор 3D-вида для экспорта.</param>
    /// <returns>Готовые опции экспорта IFC.</returns>
    /// <remarks>
    /// Метод вызывается внутри открытой транзакции экспортного сценария.
    /// Применение <see cref="IFCCategoryTemplate"/> выполняется к modifiable-документу.
    /// </remarks>
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

        /*
        Аварийный обход для диагностики export-конфигурации.

        Строка ниже принудительно отключает фазу, полученную из JSON,
        и передаёт в exporter значение "без активной фазы".
        Блок сохранён рядом с вызовом UpdateOptions, потому что именно здесь
        конфигурация переводится в итоговый набор IFC-опций.

        Используется только как локальный артефакт для временной проверки
        влияния настройки фазы на результат экспорта.

        //configuration.ActivePhaseId = ElementId.InvalidElementId.Value;
        */

        ApplyIfcCategoryMappingTemplate(
            document,
            configuration,
            fullIfcClassMappingFile);

        var options = new IFCExportOptions();

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
    /// <returns>Подготовленный корневой JSON-объект конфигурации.</returns>
    /// <exception cref="System.InvalidOperationException">
    /// Выбрасывается, если корень JSON не является объектом.
    /// </exception>
    private static JObject LoadChangeConfig(string configJsonPath)
    {
        var json = File.ReadAllText(configJsonPath, ProjectEncodings.Utf8NoBom);

        if (JToken.Parse(json) is not JObject root)
        {
            throw new InvalidOperationException(
                $"JSON настроек IFC должен содержать корневой объект: {configJsonPath}");
        }

        NormalizeClassificationEditionDate(root);
        NormalizeNestedObject(root, IfcConfigJsonKeys.ClassificationSettings);
        NormalizeNestedObject(root, IfcConfigJsonKeys.ProjectAddress);

        return root;
    }

    /// <summary>
    /// Приводит поле ClassificationEditionDate к типу <see cref="DateTime"/>,
    /// если оно пришло строкой с миллисекундами Unix Epoch.
    /// </summary>
    /// <param name="root">Корневой JSON-объект конфигурации.</param>
    private static void NormalizeClassificationEditionDate(JObject root)
    {
        if (!TryGetObject(root, IfcConfigJsonKeys.ClassificationSettings, out var classificationSettings))
            return;

        if (!classificationSettings.TryGetValue(IfcConfigJsonKeys.ClassificationEditionDate, out var rawValue))
            return;

        if (rawValue is not JValue rawDateValue)
            return;

        if (rawDateValue.Type != JTokenType.String || rawDateValue.Value is not string rawDate)
            return;

        var match = Regex.Match(rawDate, IfcConfigDefaults.UnixEpochMillisecondsPattern);
        var millis = match.Success && long.TryParse(match.Value, out var parsedMillis)
            ? parsedMillis
            : 0L;

        classificationSettings[IfcConfigJsonKeys.ClassificationEditionDate] =
            JToken.FromObject(IfcConfigDefaults.UnixEpochStart.AddMilliseconds(millis));
    }

    /// <summary>
    /// Гарантирует, что вложенное значение конфигурации представлено как <see cref="JObject"/>.
    /// </summary>
    /// <param name="root">Корневой JSON-объект конфигурации.</param>
    /// <param name="key">Имя вложенного объекта.</param>
    private static void NormalizeNestedObject(
        JObject root,
        string key)
    {
        _ = TryGetObject(root, key, out _);
    }

    /// <summary>
    /// Пытается получить вложенный JSON-объект по ключу.
    /// </summary>
    /// <param name="root">Корневой JSON-объект конфигурации.</param>
    /// <param name="key">Имя вложенного объекта.</param>
    /// <param name="jsonObject">Найденный вложенный объект.</param>
    /// <returns>
    /// <see langword="true"/>, если значение существует и является JSON-объектом;
    /// иначе <see langword="false"/>.
    /// </returns>
    private static bool TryGetObject(
        JObject root,
        string key,
        out JObject jsonObject)
    {
        jsonObject = null!;

        if (!root.TryGetValue(key, out var value) || value is null)
            return false;

        if (value is JObject exactObject)
        {
            jsonObject = exactObject;
            return true;
        }

        if (value.Type == JTokenType.Object)
        {
            jsonObject = (JObject)value;
            root[key] = jsonObject;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Импортирует txt-шаблон сопоставления категорий Revit и классов IFC
    /// в текущий документ и синхронизирует конфигурацию экспорта с этим шаблоном.
    /// </summary>
    /// <param name="document">Открытый документ Revit.</param>
    /// <param name="configuration">Конфигурация IFC, полученная из JSON.</param>
    /// <param name="fullIfcClassMappingFile">Полный путь к txt-файлу сопоставления категорий Revit и классов IFC.</param>
    /// <exception cref="System.InvalidOperationException">
    /// Выбрасывается, если документ не находится в modifiable-состоянии.
    /// </exception>
    private static void ApplyIfcCategoryMappingTemplate(
        Document document,
        IFCExportConfiguration configuration,
        string fullIfcClassMappingFile)
    {
        if (!document.IsModifiable)
        {
            throw new InvalidOperationException(
                "Сопоставление категорий Revit и классов IFC должно применяться внутри уже открытой транзакции экспорта.");
        }

        var templateName = BuildCategoryMappingTemplateName(fullIfcClassMappingFile);
        var template = GetOrImportCategoryMappingTemplate(
            document,
            fullIfcClassMappingFile,
            templateName);

        template.SetActiveTemplate();
        configuration.CategoryMapping = template.Name;
    }

    /// <summary>
    /// Возвращает существующий шаблон сопоставления категорий Revit и классов IFC
    /// либо импортирует его из txt-файла.
    /// </summary>
    /// <param name="document">Открытый документ Revit.</param>
    /// <param name="fullIfcClassMappingFile">Полный путь к txt-файлу сопоставления категорий Revit и классов IFC.</param>
    /// <param name="templateName">Детерминированное имя шаблона внутри документа.</param>
    /// <returns>Существующий или только что импортированный шаблон.</returns>
    /// <exception cref="System.InvalidOperationException">
    /// Выбрасывается, если импорт шаблона завершился без результата.
    /// </exception>
    private static IFCCategoryTemplate GetOrImportCategoryMappingTemplate(
        Document document,
        string fullIfcClassMappingFile,
        string templateName)
    {
        var template = IFCCategoryTemplate.FindByName(document, templateName)
            ?? IFCCategoryTemplate.ImportFromFile(document, fullIfcClassMappingFile, templateName);

        if (template is null)
        {
            throw new InvalidOperationException(
                $"Не удалось импортировать IFC-шаблон сопоставления категорий Revit и классов IFC: {fullIfcClassMappingFile}");
        }

        return template;
    }

    /// <summary>
    /// Подготавливает IFC-опции внутри UI-контекста exporter'а.
    /// </summary>
    /// <param name="document">Открытый документ Revit.</param>
    /// <param name="configuration">Конфигурация IFC после загрузки JSON.</param>
    /// <param name="options">Итоговые IFC-опции экспорта.</param>
    /// <param name="viewId">Идентификатор export-view.</param>
    private static void UpdateOptionsWithIfcUiDocumentContext(
        Document document,
        IFCExportConfiguration configuration,
        IFCExportOptions options,
        ElementId viewId)
    {
        var previousDocument = IFCCommandOverrideApplication.TheDocument;

        try
        {
            // Exporter читает документ не из аргументов метода, а через статическое поле
            // override-приложения. Перед UpdateOptions в это поле записывается текущий
            // документ, а после вызова исходное значение восстанавливается.
            IFCCommandOverrideApplication.TheDocument = document;
            configuration.UpdateOptions(options, viewId);
        }
        finally
        {
            IFCCommandOverrideApplication.TheDocument = previousDocument;
        }
    }

    /// <summary>
    /// Строит детерминированное имя IFC-шаблона по имени файла
    /// и хэшу его содержимого.
    /// </summary>
    /// <param name="fullIfcClassMappingFile">Полный путь к txt-файлу сопоставления категорий Revit и классов IFC.</param>
    /// <returns>Имя шаблона для импорта в документ.</returns>
    private static string BuildCategoryMappingTemplateName(string fullIfcClassMappingFile)
    {
        var fileName = Path.GetFileNameWithoutExtension(fullIfcClassMappingFile);

        var fileBytes = File.ReadAllBytes(fullIfcClassMappingFile);
        var hashBytes = SHA256.HashData(fileBytes);
        var hash = Convert.ToHexString(hashBytes[..4]);

        return $"ExportIFCFromRevit-CSharp_{fileName}_{hash}";
    }
}

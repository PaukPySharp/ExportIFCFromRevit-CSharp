using Newtonsoft.Json;

using ExportIfc.Config;
using ExportIfc.Models;

namespace ExportIfc.Transfer;

/// <summary>
/// Реализация чтения и записи служебных файлов передачи заданий между оркестратором
/// и add-in.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Централизует контракт файла <see cref="ProjectFileNames.TmpJson"/>.
/// 2. Централизует контракт <see cref="ProjectFileNames.TaskFileDisplayName"/> пакетов.
/// 3. Убирает дублирование между внешним оркестратором и add-in.
///
/// Контракты:
/// 1. <see cref="ProjectFileNames.TmpJson"/> записывается в UTF-8 без BOM.
/// 2. <see cref="ProjectFileNames.TaskFileDisplayName"/> пакетов записываются и читаются в UTF-8 без BOM.
/// 3. Порядок моделей в <see cref="ProjectFileNames.TaskFileDisplayName"/> сохраняется.
/// 4. Ключ словаря заданий — полный путь к модели без учёта регистра.
/// 5. Отключённый маршрут экспорта кодируется прежде всего отсутствием каталога выгрузки
///    для соответствующего направления.
///    Остальные конфигурационные поля модели могут сохраняться в transport-пакете.
/// </remarks>
public sealed class TransferStore : ITransferStore
{
    /// <inheritdoc />
    /// <remarks>
    /// Маршруты экспорта переносятся в transport-пакет без дополнительной перекодировки.
    /// Если направление выключено в <see cref="RevitModel"/>,
    /// в пакете обнуляется каталог соответствующего маршрута,
    /// а остальные связанные значения сохраняются как часть конфигурации модели.
    ///
    /// На стороне writer дополнительно проверяется минимальный идентификационный контракт пакета:
    /// положительный <c>RevitMajor</c>, непустой <c>RunId</c> и непустой путь к каждой модели.
    /// </remarks>
    public TransferEnvelope BuildEnvelope(
        int revitMajor,
        string runId,
        IEnumerable<RevitModel> models)
    {
        if (revitMajor <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(revitMajor),
                "Значение RevitMajor должно быть положительным.");
        }

        if (string.IsNullOrWhiteSpace(runId))
            throw new ArgumentException("Значение RunId не должно быть пустым.", nameof(runId));

        if (models is null)
            throw new ArgumentNullException(nameof(models));

        var materializedModels = models.ToList();

        // Пакет должен оставаться пригодным к однозначной привязке к конкретному RVT-списку
        // ещё на стороне writer. Это даёт fail-fast раньше, чем неконсистентный tmp.json
        // попадёт в реальный batch-запуск add-in.
        for (var index = 0; index < materializedModels.Count; index++)
        {
            var model = materializedModels[index] ?? throw new ArgumentException(
                    $"Коллекция моделей содержит пустой элемент в позиции {index + 1}.",
                    nameof(models));

            if (string.IsNullOrWhiteSpace(model.RvtPath))
            {
                throw new ArgumentException(
                    $"Модель в позиции {index + 1} должна содержать непустой путь к RVT.",
                    nameof(models));
            }
        }

        return new TransferEnvelope
        {
            RunId = runId,
            RevitMajor = revitMajor,
            CreatedUtc = DateTime.UtcNow,
            Items = materializedModels
                .Select(model => new TransferItem
                {
                    ModelPath = model.RvtPath,
                    OutputDirMapping = model.OutputDirMapping,
                    MappingJson = model.MappingJson,
                    IfcClassMappingFile = model.IfcClassMappingFile,
                    OutputDirNoMap = model.OutputDirNoMap,
                    NoMapJson = model.NoMapJson
                })
                .ToList()
        };
    }

    /// <inheritdoc />
    public void WriteEnvelope(string tmpJsonPath, TransferEnvelope envelope)
    {
        var json = JsonConvert.SerializeObject(
            envelope,
            Formatting.Indented);

        File.WriteAllText(tmpJsonPath, json, ProjectEncodings.Utf8NoBom);
    }

    /// <inheritdoc />
    public bool TryReadEnvelope(
        string tmpJsonPath,
        out TransferEnvelope? envelope)
    {
        try
        {
            var json = File.ReadAllText(tmpJsonPath, ProjectEncodings.Utf8NoBom);
            var parsed = JsonConvert.DeserializeObject<TransferEnvelope>(json);

            // Здесь проверяется только минимальная структурная пригодность пакета.
            // Поля маршрутов выгрузки не валидируются специально:
            // отключённое направление может быть представлено значением null.
            if (!IsStructurallyValidEnvelope(parsed))
            {
                envelope = null;
                return false;
            }

            envelope = parsed;
            return true;
        }
        catch
        {
            envelope = null;
            return false;
        }
    }

    /// <inheritdoc />
    public void WriteTaskModels(
        string taskFilePath,
        IEnumerable<string> modelPaths)
    {
        File.WriteAllLines(taskFilePath, modelPaths, ProjectEncodings.Utf8NoBom);
    }

    /// <inheritdoc />
    public string[] ReadTaskModels(string taskFilePath)
    {
        return File.ReadAllLines(taskFilePath, ProjectEncodings.Utf8NoBom)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();
    }

    /// <inheritdoc />
    public string? DescribeTaskModelMismatch(
        TransferEnvelope envelope,
        IReadOnlyList<string> taskModels)
    {
        if (envelope is null)
            throw new ArgumentNullException(nameof(envelope));

        if (taskModels is null)
            throw new ArgumentNullException(nameof(taskModels));

        if (envelope.Items.Count != taskModels.Count)
        {
            return string.Format(
                "Количество моделей не совпадает: tmp.json={0}, task={1}.",
                envelope.Items.Count,
                taskModels.Count);
        }

        for (var index = 0; index < envelope.Items.Count; index++)
        {
            var jsonPath = envelope.Items[index].ModelPath;
            var taskPath = taskModels[index];

            if (!string.Equals(jsonPath, taskPath, StringComparison.OrdinalIgnoreCase))
            {
                return string.Format(
                    "Расхождение в позиции {0}: tmp.json='{1}', task='{2}'.",
                    index + 1,
                    jsonPath,
                    taskPath);
            }
        }

        return null;
    }

    /// <summary>
    /// Проверяет минимальную структурную пригодность transport-пакета.
    /// </summary>
    /// <param name="envelope">Десериализованный пакет.</param>
    /// <returns>
    /// <see langword="true"/>, если пакет можно безопасно использовать дальше;
    /// иначе <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// Метод проверяет только те поля, без которых пакет теряет идентичность:
    /// major-версию Revit, список элементов и путь к модели в каждом элементе.
    ///
    /// Поля маршрутов выгрузки здесь не валидируются специально:
    /// <see langword="null"/> для них может быть частью
    /// нормального transport-контракта.
    /// </remarks>
    private static bool IsStructurallyValidEnvelope(TransferEnvelope? envelope)
    {
        if (envelope is null)
            return false;

        if (string.IsNullOrWhiteSpace(envelope.RunId))
            return false;

        if (envelope.RevitMajor <= 0)
            return false;

        if (envelope.Items is null)
            return false;

        foreach (var item in envelope.Items)
        {
            if (item is null)
                return false;

            if (string.IsNullOrWhiteSpace(item.ModelPath))
                return false;
        }

        return true;
    }
}

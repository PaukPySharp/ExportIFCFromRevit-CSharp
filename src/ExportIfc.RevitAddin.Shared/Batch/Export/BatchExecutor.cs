using Autodesk.Revit.UI;

using ExportIfc.RevitAddin.Batch.Context;
using ExportIfc.RevitAddin.Batch.Input;
using ExportIfc.RevitAddin.Logging;

namespace ExportIfc.RevitAddin.Batch.Export;

/// <summary>
/// Координирует пакетный обход моделей внутри add-in.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Пройти по уже подготовленному набору моделей текущего batch-пакета.
/// 2. Делегировать экспорт каждой модели исполнителю уровня одной модели.
/// 3. Собрать итоговый результат пакетной обработки.
///
/// Контракты:
/// 1. Класс не открывает документы Revit и не выполняет экспорт напрямую.
/// 2. В обработку передаются только элементы, уже вошедшие в рабочий JSON-контракт batch-запуска.
/// 3. Ошибка по любой отдельной модели переводит итог пакета в частичный статус.
/// </remarks>
internal sealed class BatchExecutor
{
    private readonly IModelExportExecutor _modelExportExecutor;

    /// <summary>
    /// Создаёт координатор пакетной обработки.
    /// </summary>
    /// <param name="modelExportExecutor">
    /// Исполнитель экспорта одной модели внутри текущей сессии Revit.
    /// </param>
    internal BatchExecutor(IModelExportExecutor modelExportExecutor)
    {
        _modelExportExecutor = modelExportExecutor ?? throw new ArgumentNullException(nameof(modelExportExecutor));
    }

    /// <summary>
    /// Выполняет пакетную обработку моделей.
    /// </summary>
    /// <param name="uiApp">Текущий UI-контекст Revit.</param>
    /// <param name="context">Контекст batch-запуска add-in.</param>
    /// <param name="input">Подготовленный набор моделей текущего пакета.</param>
    /// <returns>Итоговый результат пакетной обработки.</returns>
    public BatchRunResult Execute(
        UIApplication uiApp,
        BatchRunContext context,
        BatchRunInput input)
    {
        var hasErrors = false;
        var processedCount = 0;

        foreach (var item in input.Items)
        {
            processedCount++;

            if (!_modelExportExecutor.ExportOne(uiApp, item, context))
                hasErrors = true;
        }

        AddinLogs.WriteStartup(
            context.DirAdminData,
            $"Обработано моделей: {processedCount} | Моделей в task-файле: {input.TaskModels.Length}");

        return new BatchRunResult(hasErrors, processedCount);
    }
}
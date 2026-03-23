using Autodesk.Revit.DB;

using ExportIfc.Config;
using ExportIfc.RevitAddin.Batch.Export.Diagnostics;
using ExportIfc.RevitAddin.Config.Export;
using ExportIfc.RevitAddin.Batch.Export.Routes;
using ExportIfc.RevitAddin.Logging;
using ExportIfc.RevitAddin.Revit;

namespace ExportIfc.RevitAddin.Batch.Export.Execution;

/// <summary>
/// Выполняет одну попытку экспорта уже открытой модели по всем задействованным маршрутам.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Изолировать исполнение одной попытки экспорта от верхнего orchestration-класса.
/// 2. Собрать итог попытки по всем маршрутам IFC-экспорта.
///
/// Контракты:
/// 1. Класс работает только с уже открытым документом и найденным export-view.
/// 2. Класс не открывает и не закрывает документ.
/// 3. Класс не решает retry-политику и не знает о batch-жизненном цикле целиком.
/// 4. Попытка экспорта выполняется внутри транзакции, которая затем откатывается.
/// </remarks>
internal sealed class ModelExportAttemptExecutor
{
    private readonly IIfcExportOptionsFactory _ifcExportOptionsFactory;

    /// <summary>
    /// Создаёт исполнитель одной попытки экспорта модели.
    /// </summary>
    /// <param name="ifcExportOptionsFactory">
    /// Фабрика построения готовых IFC-опций для текущей платформенной ветки add-in.
    /// </param>
    public ModelExportAttemptExecutor(IIfcExportOptionsFactory ifcExportOptionsFactory)
    {
        _ifcExportOptionsFactory = ifcExportOptionsFactory
            ?? throw new ArgumentNullException(nameof(ifcExportOptionsFactory));
    }

    /// <summary>
    /// Выполняет одну попытку экспорта модели по всем разрешённым маршрутам.
    /// </summary>
    /// <param name="context">Контекст уже открытой модели и export-view.</param>
    /// <returns>Итог попытки экспорта.</returns>
    /// <remarks>
    /// Экспорт выполняется внутри транзакции Revit, которая после завершения попытки
    /// всегда откатывается. Метод не оставляет изменений модели после выполнения.
    /// </remarks>
    public ExportAttemptResult Execute(ModelExportExecutionContext context)
    {
        try
        {
            using var transaction = new Transaction(context.Document, RevitConstants.IfcExportTransactionName);
            transaction.Start();

            var result = ExecuteRoutes(context);

            transaction.RollBack();
            return result;
        }
        catch (Exception ex)
        {
            AddinLogs.WriteDaily(
                context.DirAdminData,
                LogFiles.ExportErrors,
                $"{context.Item.ModelPath} - ошибка экспорта: {ex}");

            return ExportAttemptResult.FailedWithoutExports();
        }
    }

    /// <summary>
    /// Выполняет все маршруты IFC-экспорта, разрешённые текущим контекстом модели.
    /// </summary>
    /// <param name="context">Контекст уже открытой модели и export-view.</param>
    /// <returns>Итог попытки экспорта по всем маршрутам.</returns>
    private ExportAttemptResult ExecuteRoutes(ModelExportExecutionContext context)
    {
        var result = ExportAttemptResult.Start();
        var hasRoutes = false;

        foreach (var route in BuildRoutes(context))
        {
            hasRoutes = true;
            var attemptResult = ExportRoute(context, route);
            result = result.Merge(attemptResult);
        }

        if (hasRoutes)
            return result;

        AddinLogs.WriteStartup(
            context.DirAdminData,
            $"{context.Item.ModelPath} - для модели не найдено ни одного настроенного маршрута IFC-экспорта.");

        return ExportAttemptResult.FailedWithoutExports();
    }

    /// <summary>
    /// Строит маршруты IFC-экспорта, разрешённые текущим item и настройками batch-запуска.
    /// </summary>
    /// <param name="context">Контекст уже открытой модели и export-view.</param>
    /// <returns>Последовательность маршрутов IFC-экспорта.</returns>
    private static IEnumerable<ExportRouteRequest> BuildRoutes(ModelExportExecutionContext context)
    {
        var item = context.Item;
        var outputDirMapping = item.OutputDirMapping;
        var mappingJson = item.MappingJson;

        if (!string.IsNullOrWhiteSpace(outputDirMapping)
            && !string.IsNullOrWhiteSpace(mappingJson))
        {
            var mappingOutputDirectory = outputDirMapping!;
            var mappingConfigJsonPath = mappingJson!;

            yield return new ExportRouteRequest(
                mappingOutputDirectory,
                context.IfcName,
                item.IfcClassMappingFile,
                mappingConfigJsonPath,
                "mapping");
        }

        var outputDirNoMap = item.OutputDirNoMap;
        var noMapJson = item.NoMapJson;

        if (context.BatchContext.EnableUnmappedExport
            && !string.IsNullOrWhiteSpace(outputDirNoMap)
            && !string.IsNullOrWhiteSpace(noMapJson))
        {
            var noMapOutputDirectory = outputDirNoMap!;
            var noMapConfigJsonPath = noMapJson!;

            yield return new ExportRouteRequest(
                noMapOutputDirectory,
                context.IfcName,
                item.IfcClassMappingFile,
                noMapConfigJsonPath,
                "nomap");
        }
    }

    /// <summary>
    /// Выполняет один маршрут IFC-экспорта и проверяет результат по созданному файлу.
    /// </summary>
    /// <param name="context">Контекст уже открытой модели и export-view.</param>
    /// <param name="route">Описание маршрута IFC-экспорта.</param>
    /// <returns>Итог одного маршрута IFC-экспорта.</returns>
    private ExportAttemptResult ExportRoute(
        ModelExportExecutionContext context,
        ExportRouteRequest route)
    {
        var ifcPath = BuildIfcPath(route);
        string? backupPath = null;

        try
        {
            backupPath = BackupExistingIfc(ifcPath);
            ExportIfc(context, route);

            var fileInfo = new FileInfo(ifcPath);
            var sizeBytes = fileInfo.Exists ? fileInfo.Length : 0;

            AddinLogs.WriteStartup(
                context.DirAdminData,
                $"Экспорт {route.ExportMode}. File='{ifcPath}' | SizeBytes={sizeBytes}");

            if (!fileInfo.Exists || sizeBytes == 0)
            {
                TryRestoreBackup(context, route, ifcPath, backupPath);

                AddinLogs.WriteDaily(
                    context.DirAdminData,
                    LogFiles.ExportErrors,
                    $"{context.Item.ModelPath} - после экспорта {route.ExportMode} IFC-файл не создан или пустой ({ifcPath})");

                return ExportAttemptResult.FailedWithExports();
            }

            TryDeleteBackup(context, route, backupPath);

            if (IsSuspiciousSmallIfc(sizeBytes))
            {
                WriteSuspiciousExportSize(context, route, ifcPath, sizeBytes);
                return ExportAttemptResult.SuccessfulSuspicious();
            }

            return ExportAttemptResult.SuccessfulNormal();
        }
        catch (Exception ex)
        {
            TryRestoreBackup(context, route, ifcPath, backupPath);

            AddinLogs.WriteDaily(
                context.DirAdminData,
                LogFiles.ExportErrors,
                $"{context.Item.ModelPath} - ошибка экспорта {route.ExportMode}: {ex}");

            return ExportAttemptResult.FailedWithExports();
        }
    }

    /// <summary>
    /// Выполняет IFC-экспорт документа по заданному маршруту.
    /// </summary>
    /// <param name="context">Контекст уже открытой модели и export-view.</param>
    /// <param name="route">Описание маршрута IFC-экспорта.</param>
    private void ExportIfc(
        ModelExportExecutionContext context,
        ExportRouteRequest route)
    {
        Directory.CreateDirectory(route.OutputDirectory);

        var options = _ifcExportOptionsFactory.Create(
            context.Document,
            route.IfcClassMappingFile,
            route.ConfigJsonPath,
            context.ExportView.Id);

        context.Document.Export(route.OutputDirectory, route.IfcName, options);
    }

    /// <summary>
    /// Строит полный путь к ожидаемому IFC-файлу маршрута.
    /// </summary>
    /// <param name="route">Описание маршрута IFC-экспорта.</param>
    /// <returns>Полный путь к ожидаемому IFC-файлу.</returns>
    private static string BuildIfcPath(ExportRouteRequest route)
        => Path.Combine(route.OutputDirectory, route.IfcName + ProjectFileExtensions.Ifc);

    /// <summary>
    /// Убирает ранее существовавший IFC во временную резервную копию,
    /// чтобы старый файл не мог быть принят за результат текущего экспорта.
    /// </summary>
    /// <param name="ifcPath">Путь к итоговому IFC-файлу.</param>
    /// <returns>
    /// Путь к резервной копии прежнего IFC либо <see langword="null"/>,
    /// если файла до экспорта не было.
    /// </returns>
    private static string? BackupExistingIfc(string ifcPath)
    {
        if (!File.Exists(ifcPath))
            return null;

        var backupPath = ifcPath + ".preexport-backup";

        if (File.Exists(backupPath))
            File.Delete(backupPath);

        File.Move(ifcPath, backupPath);
        return backupPath;
    }

    /// <summary>
    /// Удаляет временную резервную копию после успешного экспорта.
    /// </summary>
    /// <param name="context">Контекст текущей попытки экспорта.</param>
    /// <param name="route">Описание маршрута IFC-экспорта.</param>
    /// <param name="backupPath">Путь к резервной копии прежнего IFC.</param>
    private static void TryDeleteBackup(
        ModelExportExecutionContext context,
        ExportRouteRequest route,
        string? backupPath)
    {
        if (string.IsNullOrWhiteSpace(backupPath) || !File.Exists(backupPath))
            return;

        try
        {
            File.Delete(backupPath);
        }
        catch (Exception ex)
        {
            AddinLogs.WriteStartup(
                context.DirAdminData,
                $"Экспорт {route.ExportMode}. Не удалось удалить резервную копию '{backupPath}': {ex.Message}");
        }
    }

    /// <summary>
    /// Возвращает на место прежний IFC, если текущий экспорт не завершился валидным файлом.
    /// </summary>
    /// <param name="context">Контекст текущей попытки экспорта.</param>
    /// <param name="route">Описание маршрута IFC-экспорта.</param>
    /// <param name="ifcPath">Путь к итоговому IFC-файлу.</param>
    /// <param name="backupPath">Путь к резервной копии прежнего IFC.</param>
    private static void TryRestoreBackup(
        ModelExportExecutionContext context,
        ExportRouteRequest route,
        string ifcPath,
        string? backupPath)
    {
        if (string.IsNullOrWhiteSpace(backupPath) || !File.Exists(backupPath))
            return;

        try
        {
            if (File.Exists(ifcPath))
                File.Delete(ifcPath);

            File.Move(backupPath, ifcPath);
        }
        catch (Exception ex)
        {
            AddinLogs.WriteDaily(
                context.DirAdminData,
                LogFiles.ExportErrors,
                $"{context.Item.ModelPath} - не удалось восстановить предыдущий IFC после ошибки экспорта {route.ExportMode}: {ex.Message}");
        }
    }

    /// <summary>
    /// Определяет, относится ли размер IFC к подозрительно маленькому результату.
    /// </summary>
    /// <param name="sizeBytes">Размер IFC-файла в байтах.</param>
    /// <returns>
    /// <see langword="true"/>, если размер файла меньше диагностического порога;
    /// иначе <see langword="false"/>.
    /// </returns>
    private static bool IsSuspiciousSmallIfc(long sizeBytes)
        => sizeBytes < AddinExportThresholds.SuspiciousSmallIfcBytes;

    /// <summary>
    /// Пишет предупреждение о подозрительно маленьком IFC и дополняет его
    /// расширенной диагностикой export-view.
    /// </summary>
    /// <param name="context">Контекст уже открытой модели и export-view.</param>
    /// <param name="route">Описание маршрута IFC-экспорта.</param>
    /// <param name="ifcPath">Полный путь к IFC-файлу.</param>
    /// <param name="sizeBytes">Размер IFC-файла в байтах.</param>
    private static void WriteSuspiciousExportSize(
        ModelExportExecutionContext context,
        ExportRouteRequest route,
        string ifcPath,
        long sizeBytes)
    {
        AddinLogs.WriteStartup(
            context.DirAdminData,
            $"Подозрительно маленький IFC. Mode={route.ExportMode} | File='{ifcPath}' | SizeBytes={sizeBytes}");

        ExportViewDiagnosticsWriter.WriteForProblemExport(
            context.Document,
            context.ExportView,
            context.DirAdminData);
    }
}

using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;

namespace ExportIfc.RevitAddin.Revit;

internal static partial class RevitDocuments
{
    /// <summary>
    /// Собирает набор опций Revit для batch-открытия модели.
    /// </summary>
    /// <param name="openMode">Целевой режим открытия документа.</param>
    /// <returns>Настроенный набор опций открытия документа.</returns>
    /// <remarks>
    /// Для режима <see cref="BatchOpenMode.Detached"/> метод включает detach
    /// от центральной модели и открытие всех рабочих наборов.
    /// </remarks>
    private static OpenOptions BuildOpenOptions(BatchOpenMode openMode)
    {
        var options = new OpenOptions
        {
            Audit = false,
            AllowOpeningLocalByWrongUser = true,
            IgnoreExtensibleStorageSchemaConflict = true,
            OpenForeignOption = OpenForeignOption.Open
        };

        if (openMode != BatchOpenMode.Detached)
            return options;

        options.DetachFromCentralOption = DetachFromCentralOption.DetachAndPreserveWorksets;

        var worksets = new WorksetConfiguration(
            WorksetConfigurationOption.OpenAllWorksets);

        options.SetOpenWorksetsConfiguration(worksets);
        return options;
    }

    /// <summary>
    /// Читает BasicFileInfo и извлекает из него признак workshared-модели.
    /// </summary>
    /// <param name="modelPath">Полный путь к RVT-файлу.</param>
    /// <returns>Результат чтения BasicFileInfo для текущей модели.</returns>
    /// <remarks>
    /// Ошибка чтения метаданных не прерывает batch-обработку:
    /// она фиксируется в техническом логе, а вызывающий код выбирает
    /// безопасный fallback-режим открытия.
    /// </remarks>
    private static BasicFileInfoProbeResult ProbeBasicFileInfo(string modelPath)
    {
        try
        {
            var info = BasicFileInfo.Extract(modelPath);

            return new BasicFileInfoProbeResult(
                extractSucceeded: true,
                isWorkshared: info is not null && info.IsWorkshared);
        }
        catch (Exception ex)
        {
            var summary = FormatExceptionSummary(ex);

            WriteStartup(
                $"BasicFileInfo.Extract завершился ошибкой. " +
                $"Model='{modelPath}' | Error='{summary}'");

            return new BasicFileInfoProbeResult(
                extractSucceeded: false,
                isWorkshared: false);
        }
    }

    /// <summary>
    /// Пытается открыть модель в одном конкретном режиме и фиксирует
    /// диагностический след в техническом логе.
    /// </summary>
    /// <param name="application">Экземпляр приложения Revit.</param>
    /// <param name="revitModelPath">Путь модели в формате Revit API.</param>
    /// <param name="modelPath">Исходный путь к RVT-файлу.</param>
    /// <param name="attemptLabel">Метка попытки для технического лога.</param>
    /// <param name="openMode">Целевой режим открытия.</param>
    /// <param name="probe">Результат предварительного чтения BasicFileInfo.</param>
    /// <param name="error">Исключение Revit API, если попытка завершилась ошибкой.</param>
    /// <param name="returnedNull">
    /// <see langword="true"/>, если вызов Revit API <c>OpenDocumentFile(...)</c>
    /// вернул <see langword="null"/>.
    /// </param>
    /// <returns>Открытый документ либо <see langword="null"/>.</returns>
    private static Document? TryOpenDocumentWithMode(
        Application application,
        ModelPath revitModelPath,
        string modelPath,
        string attemptLabel,
        BatchOpenMode openMode,
        BasicFileInfoProbeResult probe,
        out Exception? error,
        out bool returnedNull)
    {
        error = null;
        returnedNull = false;

        WriteStartup(
            $"Подготовка открытия модели. " +
            $"Model='{modelPath}' | Attempt={attemptLabel} | " +
            $"BasicFileInfo={GetProbeStatusLabel(probe)} | " +
            $"IsWorkshared={GetIsWorksharedLabel(probe)} | " +
            $"SelectedOpenMode={ToLogValue(openMode)}");

        try
        {
            var document = application.OpenDocumentFile(
                revitModelPath,
                BuildOpenOptions(openMode));

            if (document is null)
            {
                returnedNull = true;

                WriteStartup(
                    $"OpenDocumentFile вернул null. " +
                    $"Model='{modelPath}' | Attempt={attemptLabel} | " +
                    $"OpenMode={ToLogValue(openMode)}");

                return null;
            }

            return document;
        }
        catch (Exception ex)
        {
            error = ex;

            WriteStartup(
                $"Попытка открытия модели завершилась ошибкой. " +
                $"Model='{modelPath}' | Attempt={attemptLabel} | " +
                $"OpenMode={ToLogValue(openMode)} | Error='{FormatExceptionSummary(ex)}'");

            return null;
        }
    }

    /// <summary>
    /// Формирует исключение верхнего уровня после неуспешной попытки открытия
    /// без fallback-повтора.
    /// </summary>
    /// <param name="modelPath">Путь к модели.</param>
    /// <param name="openMode">Режим неуспешной попытки.</param>
    /// <param name="error">Исключение попытки.</param>
    /// <param name="returnedNull">Признак null-результата попытки.</param>
    /// <returns>Исключение для вызывающего кода.</returns>
    private static Exception BuildSingleModeOpeningFailureException(
        string modelPath,
        BatchOpenMode openMode,
        Exception? error,
        bool returnedNull)
    {
        if (error is not null)
            return error;

        if (returnedNull)
        {
            return new InvalidOperationException(
                $"OpenDocumentFile вернул null при открытии модели '{modelPath}' " +
                $"в режиме {ToLogValue(openMode)}.");
        }

        return new InvalidOperationException(
            $"Не удалось открыть модель '{modelPath}' в режиме {ToLogValue(openMode)}.");
    }

    /// <summary>
    /// Формирует исключение верхнего уровня после двух неуспешных попыток открытия.
    /// </summary>
    /// <param name="modelPath">Путь к модели.</param>
    /// <param name="primaryMode">Режим основной попытки.</param>
    /// <param name="fallbackMode">Режим fallback-попытки.</param>
    /// <param name="primaryError">Исключение основной попытки.</param>
    /// <param name="primaryReturnedNull">Признак null-результата основной попытки.</param>
    /// <param name="fallbackError">Исключение fallback-попытки.</param>
    /// <param name="fallbackReturnedNull">Признак null-результата fallback-попытки.</param>
    /// <returns>Исключение для вызывающего кода.</returns>
    private static Exception BuildFallbackOpeningFailureException(
        string modelPath,
        BatchOpenMode primaryMode,
        BatchOpenMode fallbackMode,
        Exception? primaryError,
        bool primaryReturnedNull,
        Exception? fallbackError,
        bool fallbackReturnedNull)
    {
        if (fallbackError is not null)
            return fallbackError;

        if (primaryError is not null)
        {
            return new InvalidOperationException(
                $"Не удалось открыть модель '{modelPath}' ни в режиме {ToLogValue(primaryMode)}, " +
                $"ни в режиме {ToLogValue(fallbackMode)}.",
                primaryError);
        }

        if (primaryReturnedNull || fallbackReturnedNull)
        {
            return new InvalidOperationException(
                $"OpenDocumentFile вернул null при открытии модели '{modelPath}' " +
                $"в режимах {ToLogValue(primaryMode)} и {ToLogValue(fallbackMode)}.");
        }

        return new InvalidOperationException(
            $"Не удалось открыть модель '{modelPath}' ни в режиме {ToLogValue(primaryMode)}, " +
            $"ни в режиме {ToLogValue(fallbackMode)}.");
    }
}

/*
Идея, отключённая в консервативном режиме:

Если основная попытка открытия была выполнена в режиме Detached и завершилась ошибкой,
можно было бы один раз повторить открытие в режиме Direct по аналогии с веткой
Direct -> Detached.

Пример логики:

if (primaryMode == BatchOpenMode.Detached)
{
    var fallbackMode = BatchOpenMode.Direct;

    WriteStartup(
        $"Основная попытка открытия модели завершилась неуспешно. " +
        $"Model='{modelPath}' | OpenMode={ToLogValue(primaryMode)} | " +
        $"Result={BuildAttemptResultLabel(primaryError, primaryReturnedNull)} | " +
        $"FallbackOpenMode={ToLogValue(fallbackMode)}");

    var fallbackDocument = TryOpenDocumentWithMode(
        application,
        revitModelPath,
        modelPath,
        attemptLabel: \"Fallback\",
        openMode: fallbackMode,
        probe,
        out var fallbackError,
        out var fallbackReturnedNull);

    if (fallbackDocument is not null)
        return fallbackDocument;

    throw BuildFallbackOpeningFailureException(
        modelPath,
        primaryMode,
        fallbackMode,
        primaryError,
        primaryReturnedNull,
        fallbackError,
        fallbackReturnedNull);
}

В рабочем коде эта ветка отключена.
Повтор Detached -> Direct переводит обработку workshared-модели в менее
консервативный режим открытия, чем retry в сторону Detached.

Блок сохранён как reference-алгоритм:
1. показывает точку встраивания альтернативной попытки;
2. сохраняет схему техлогирования primary/fallback-попыток;
3. позволяет быстро вернуть экспериментальную ветку под отдельный флаг,
   не восстанавливая логику по памяти.
*/

using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;

namespace ExportIfc.RevitAddin.Revit;

/// <summary>
/// Вспомогательные операции открытия и закрытия документов Revit.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Централизовать настройки открытия моделей для batch-обработки.
/// 2. Убрать низкоуровневую работу с документами из batch-исполнителя.
/// 3. Дать одно место для штатного закрытия документа после экспорта.
///
/// Контракты:
/// 1. Режим открытия сначала выбирается по BasicFileInfo.
/// 2. При сбое определения метаданных или сбое открытия в режиме direct
///    выполняется одна повторная попытка в режиме detach.
/// 3. Для режима detach открываются все рабочие наборы.
/// 4. Документ закрывается без сохранения.
/// 5. Ошибки очистки CLR и Revit API-объектов не пробрасываются наружу.
/// </remarks>
internal static partial class RevitDocuments
{
    /// <summary>
    /// Открывает документ Revit с опциями, рассчитанными на batch-обработку.
    /// </summary>
    /// <param name="application">Экземпляр приложения Revit.</param>
    /// <param name="modelPath">Полный путь к RVT-файлу.</param>
    /// <returns>Открытый документ Revit.</returns>
    public static Document OpenForBatchProcessing(
        Application application,
        string modelPath)
    {
        var revitModelPath =
            ModelPathUtils.ConvertUserVisiblePathToModelPath(modelPath);

        var probe = ProbeBasicFileInfo(modelPath);
        var primaryMode = probe.IsWorkshared
            ? BatchOpenMode.Detached
            : BatchOpenMode.Direct;

        var primaryDocument = TryOpenDocumentWithMode(
            application,
            revitModelPath,
            modelPath,
            attemptLabel: "Primary",
            openMode: primaryMode,
            probe,
            out var primaryError,
            out var primaryReturnedNull);

        if (primaryDocument is not null)
            return primaryDocument;

        if (primaryMode != BatchOpenMode.Direct)
        {
            throw BuildSingleModeOpeningFailureException(
                modelPath,
                primaryMode,
                primaryError,
                primaryReturnedNull);
        }

        var fallbackMode = BatchOpenMode.Detached;

        WriteStartup(
            $"Основная попытка открытия модели завершилась неуспешно. " +
            $"Model='{modelPath}' | OpenMode={ToLogValue(primaryMode)} | " +
            $"Result={BuildAttemptResultLabel(primaryError, primaryReturnedNull)} | " +
            $"FallbackOpenMode={ToLogValue(fallbackMode)}");

        var fallbackDocument = TryOpenDocumentWithMode(
            application,
            revitModelPath,
            modelPath,
            attemptLabel: "Fallback",
            openMode: fallbackMode,
            probe,
            out var fallbackError,
            out var fallbackReturnedNull);

        if (fallbackDocument is not null)
        {
            WriteStartup(
                $"Повторное открытие с альтернативным режимом выполнено успешно. " +
                $"Model='{modelPath}' | OpenMode={ToLogValue(fallbackMode)}");

            return fallbackDocument;
        }

        throw BuildFallbackOpeningFailureException(
            modelPath,
            primaryMode,
            fallbackMode,
            primaryError,
            primaryReturnedNull,
            fallbackError,
            fallbackReturnedNull);
    }
}

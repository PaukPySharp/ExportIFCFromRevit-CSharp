namespace ExportIfc.RevitAddin.Revit;

internal static partial class RevitDocuments
{
    /// <summary>
    /// Режим открытия модели для batch-обработки.
    /// </summary>
    /// <remarks>
    /// <see cref="Direct"/> используется для обычных non-workshared-моделей.
    /// <see cref="Detached"/> используется для workshared-моделей, которые
    /// открываются независимо от центрального файла.
    /// </remarks>
    private enum BatchOpenMode
    {
        /// <summary>
        /// Обычное открытие файла без detach от центральной модели.
        /// </summary>
        Direct,

        /// <summary>
        /// Открытие workshared-модели с detach от центральной модели.
        /// </summary>
        Detached
    }

    /// <summary>
    /// Результат предварительного чтения метаданных BasicFileInfo.
    /// </summary>
    /// <remarks>
    /// Структура отделяет сам факт успешного чтения BasicFileInfo
    /// от извлечённого признака <c>IsWorkshared</c>.
    /// </remarks>
    private readonly struct BasicFileInfoProbeResult
    {
        /// <summary>
        /// Создаёт результат предварительного чтения BasicFileInfo.
        /// </summary>
        /// <param name="extractSucceeded">
        /// Признак того, что <c>BasicFileInfo.Extract(...)</c> завершился без исключения.
        /// </param>
        /// <param name="isWorkshared">
        /// Признак workshared-модели, извлечённый из BasicFileInfo.
        /// </param>
        public BasicFileInfoProbeResult(
            bool extractSucceeded,
            bool isWorkshared)
        {
            ExtractSucceeded = extractSucceeded;
            IsWorkshared = isWorkshared;
        }

        /// <summary>
        /// Показывает, удалось ли штатно прочитать BasicFileInfo.
        /// </summary>
        public bool ExtractSucceeded { get; }

        /// <summary>
        /// Показывает, распознана ли модель как workshared.
        /// </summary>
        public bool IsWorkshared { get; }
    }
}

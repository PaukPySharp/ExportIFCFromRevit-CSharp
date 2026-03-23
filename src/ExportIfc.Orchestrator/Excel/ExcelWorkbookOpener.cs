using ClosedXML.Excel;

namespace ExportIfc.Excel;

/// <summary>
/// Открытие Excel-книг для сценариев чтения и сохранения.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Централизует технические способы открытия workbook-файлов через ClosedXML.
/// 2. Предоставляет отдельные режимы для shared-read чтения и для сценария сохранения.
/// 3. Формирует единообразные ошибки открытия workbook-файлов.
///
/// Контракты:
/// 1. Для shared-read чтения исходный файл открывается через
///    <see cref="FileMode.Open"/>, <see cref="FileAccess.Read"/> и
///    <see cref="FileShare.ReadWrite"/> | <see cref="FileShare.Delete"/>.
/// 2. Shared-read режим не должен оставлять после себя открытый file handle
///    на исходный workbook-файл.
/// 3. Для сценария сохранения существующая workbook-книга открывается по полному пути,
///    а при отсутствии файла создаётся новая пустая книга.
/// 4. При ошибке создания <see cref="XLWorkbook"/> временные stream-объекты освобождаются.
/// 5. Ошибки открытия workbook-файлов упаковываются в <see cref="IOException"/>
///    с понятным именем workbook-файла в сообщении.
/// </remarks>
internal static class ExcelWorkbookOpener
{
    /// <summary>
    /// Открывает Excel-книгу в режиме чтения с разрешением совместного доступа.
    /// </summary>
    /// <param name="workbookPath">Полный путь к workbook-файлу.</param>
    /// <param name="workbookDisplayName">
    /// Человекочитаемое имя workbook-файла для текста ошибки.
    /// </param>
    /// <returns>Открытая Excel-книга.</returns>
    /// <exception cref="ArgumentException">
    /// Выбрасывается, если путь или display-name не заданы.
    /// </exception>
    /// <exception cref="IOException">
    /// Выбрасывается, если файл не удалось открыть для чтения.
    /// </exception>
    /// <remarks>
    /// Workbook создаётся не поверх исходного <see cref="FileStream"/>, а поверх
    /// его in-memory копии.
    /// После возврата из метода у процесса не остаётся открытого file handle
    /// на исходный workbook-файл. Это исключает конфликт с последующим сохранением
    /// книги в тот же путь в рамках текущего процесса.
    /// </remarks>
    public static XLWorkbook OpenForSharedRead(
        string workbookPath,
        string workbookDisplayName)
    {
        ValidateArguments(workbookPath, workbookDisplayName);

        try
        {
            using var fileStream = new FileStream(
                workbookPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);

            var memoryStream = new MemoryStream();

            try
            {
                // ClosedXML открывается на основе MemoryStream, а не исходного FileStream.
                // Это разрывает связь между живым экземпляром workbook и файловым handle
                // исходного xlsx-файла.
                fileStream.CopyTo(memoryStream);
                memoryStream.Position = 0;

                return new XLWorkbook(memoryStream);
            }
            catch
            {
                memoryStream.Dispose();
                throw;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw CreateOpenException(
                workbookDisplayName,
                workbookPath,
                purposeSuffix: string.Empty,
                ex);
        }
    }

    /// <summary>
    /// Открывает существующую Excel-книгу для сценария сохранения
    /// или создаёт новую пустую книгу, если файл ещё не существует.
    /// </summary>
    /// <param name="workbookPath">Полный путь к workbook-файлу.</param>
    /// <param name="workbookDisplayName">
    /// Человекочитаемое имя workbook-файла для текста ошибки.
    /// </param>
    /// <param name="openedExistingFile">
    /// Признак того, что метод открыл уже существующий workbook-файл.
    /// <see langword="false"/> означает, что файл ещё не существовал
    /// и была создана новая пустая workbook-книга.
    /// </param>
    /// <returns>Открытая существующая книга или новая пустая workbook-книга.</returns>
    /// <exception cref="ArgumentException">
    /// Выбрасывается, если путь или display-name не заданы.
    /// </exception>
    /// <exception cref="IOException">
    /// Выбрасывается, если существующий файл не удалось открыть для сохранения.
    /// </exception>
    /// <remarks>
    /// Метод определяет, какой режим сохранения должен использовать вызывающий код:
    /// 1. для существующего workbook-файла — <c>Save()</c>;
    /// 2. для новой workbook-книги — <c>SaveAs(...)</c>.
    /// </remarks>
    public static XLWorkbook OpenOrCreateForSave(
        string workbookPath,
        string workbookDisplayName,
        out bool openedExistingFile)
    {
        ValidateArguments(workbookPath, workbookDisplayName);

        if (!File.Exists(workbookPath))
        {
            openedExistingFile = false;
            return new XLWorkbook();
        }

        try
        {
            var workbook = new XLWorkbook(workbookPath);
            openedExistingFile = true;
            return workbook;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw CreateOpenException(
                workbookDisplayName,
                workbookPath,
                purposeSuffix: " для сохранения",
                ex);
        }
    }

    /// <summary>
    /// Проверяет обязательные аргументы методов открытия workbook-файлов.
    /// </summary>
    /// <param name="workbookPath">Полный путь к workbook-файлу.</param>
    /// <param name="workbookDisplayName">
    /// Человекочитаемое имя workbook-файла для текста ошибки.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Выбрасывается, если путь или display-name не заданы.
    /// </exception>
    private static void ValidateArguments(
        string workbookPath,
        string workbookDisplayName)
    {
        if (string.IsNullOrWhiteSpace(workbookPath))
        {
            throw new ArgumentException(
                "Не задан путь к workbook-файлу.",
                nameof(workbookPath));
        }

        if (string.IsNullOrWhiteSpace(workbookDisplayName))
        {
            throw new ArgumentException(
                "Не задано display-имя workbook-файла.",
                nameof(workbookDisplayName));
        }
    }

    /// <summary>
    /// Создаёт единообразное исключение открытия workbook-файла.
    /// </summary>
    /// <param name="workbookDisplayName">
    /// Человекочитаемое имя workbook-файла.
    /// </param>
    /// <param name="workbookPath">Полный путь к workbook-файлу.</param>
    /// <param name="purposeSuffix">
    /// Дополнение к действию открытия, например <c>для сохранения</c>.
    /// </param>
    /// <param name="innerException">Исходное исключение открытия.</param>
    /// <returns>Исключение с прикладным сообщением проекта.</returns>
    private static IOException CreateOpenException(
        string workbookDisplayName,
        string workbookPath,
        string purposeSuffix,
        Exception innerException)
    {
        var reason = innerException switch
        {
            UnauthorizedAccessException =>
                "недостаточно прав доступа",
            _ =>
                "файл занят другим процессом или временно недоступен"
        };

        return new IOException(
            $"Не удалось открыть {workbookDisplayName}{purposeSuffix}: {reason}. Путь: {workbookPath}",
            innerException);
    }
}

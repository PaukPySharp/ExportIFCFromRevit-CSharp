namespace ExportIfc.IO;

public static partial class FileSystemEx
{
    /// <summary>
    /// Проверяет, что путь указывает на существующий файл.
    /// </summary>
    /// <param name="path">Проверяемый путь.</param>
    /// <param name="description">Описание файла для текста ошибки.</param>
    /// <exception cref="ArgumentException">
    /// Выбрасывается, если путь или описание не заданы.
    /// </exception>
    /// <exception cref="FileNotFoundException">
    /// Выбрасывается, если файл не найден.
    /// </exception>
    /// <exception cref="IOException">
    /// Выбрасывается, если по указанному пути найден каталог, а не файл.
    /// </exception>
    public static void EnsureExistingFile(string path, string description)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException(
                "Не задан путь к файлу.",
                nameof(path));
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException(
                "Не задано описание проверяемого файла.",
                nameof(description));
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Не найден {description}: {path}",
                path);
        }

        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.Directory) != 0)
        {
            throw new IOException(
                $"Ожидался файл ({description}), но найден каталог: {path}");
        }
    }

    /// <summary>
    /// Проверяет, что путь либо отсутствует, либо указывает на каталог.
    /// </summary>
    /// <param name="path">Проверяемый путь.</param>
    /// <param name="description">Описание пути для текста ошибки.</param>
    /// <exception cref="ArgumentException">
    /// Выбрасывается, если путь или описание не заданы.
    /// </exception>
    /// <exception cref="IOException">
    /// Выбрасывается, если по указанному пути найден файл, а ожидался каталог.
    /// </exception>
    public static void EnsureDirectoryOrMissing(string path, string description)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException(
                "Не задан путь для проверки каталога.",
                nameof(path));
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException(
                "Не задано описание проверяемого пути.",
                nameof(description));
        }

        if (!File.Exists(path) && !Directory.Exists(path))
            return;

        if (Directory.Exists(path))
            return;

        throw new IOException(
            $"Некорректный путь: ожидался каталог ({description}), но найден файл: {path}");
    }

    /// <summary>
    /// Нормализует путь и проверяет, что он указывает на существующий файл.
    /// </summary>
    /// <param name="path">Исходный путь к файлу.</param>
    /// <param name="description">Описание файла для текста ошибки.</param>
    /// <returns>Нормализованный полный путь к существующему файлу.</returns>
    /// <exception cref="ArgumentException">
    /// Выбрасывается, если путь или описание не заданы,
    /// либо путь не может быть приведён к корректному полному виду.
    /// </exception>
    /// <exception cref="FileNotFoundException">
    /// Выбрасывается, если файл не найден.
    /// </exception>
    /// <exception cref="IOException">
    /// Выбрасывается, если по указанному пути найден каталог, а не файл.
    /// </exception>
    public static string NormalizeExistingFilePath(string path, string description)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException(
                $"Не задан путь к {description}.",
                nameof(path));
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException(
                "Не задано описание проверяемого файла.",
                nameof(description));
        }

        var fullPath = Path.GetFullPath(path.Trim());
        EnsureExistingFile(fullPath, description);

        return fullPath;
    }
}
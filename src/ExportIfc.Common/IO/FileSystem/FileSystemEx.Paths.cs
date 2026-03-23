namespace ExportIfc.IO;

public static partial class FileSystemEx
{
    /// <summary>
    /// Пробует нормализовать только уже заданный абсолютный путь.
    /// </summary>
    /// <param name="path">Исходная строка пути.</param>
    /// <returns>Нормализованный абсолютный путь или <see langword="null"/>.</returns>
    /// <remarks>
    /// Метод подходит для значений из Excel и настроек, где важно отличать
    /// явно заданный абсолютный путь от строки, которая станет абсолютной
    /// только после привязки к текущему каталогу или диску процесса.
    /// </remarks>
    public static string? TryNormalizeAbsolutePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        var trimmed = path.Trim();
        if (!IsFullyQualifiedAbsolutePath(trimmed))
            return null;

        try
        {
            return Path.GetFullPath(trimmed);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Нормализует путь, если это возможно.
    /// </summary>
    /// <param name="path">Исходный путь.</param>
    /// <returns>Нормализованный путь либо исходная строка без крайних пробелов.</returns>
    public static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        var trimmed = path.Trim();

        try
        {
            return Path.GetFullPath(trimmed);
        }
        catch
        {
            return trimmed;
        }
    }

    /// <summary>
    /// Нормализует путь и приводит расширение файла к нижнему регистру.
    /// </summary>
    /// <param name="path">Исходный путь.</param>
    /// <returns>Нормализованный путь с расширением в нижнем регистре.</returns>
    /// <remarks>
    /// Используется в сценариях сравнения путей и дедупликации,
    /// когда регистр расширения не должен влиять на результат.
    /// </remarks>
    public static string NormalizePathWithLowerExtension(string path)
    {
        var fullPath = NormalizePath(path);
        if (fullPath.Length == 0)
            return fullPath;

        var extension = Path.GetExtension(fullPath);
        if (string.IsNullOrWhiteSpace(extension))
            return fullPath;

        return Path.ChangeExtension(fullPath, extension.ToLowerInvariant()) ?? fullPath;
    }

    /// <summary>
    /// Ищет файл с относительным путём, поднимаясь вверх по дереву каталогов.
    /// </summary>
    /// <param name="startDirectory">Стартовая директория поиска.</param>
    /// <param name="relativePath">Относительный путь к искомому файлу.</param>
    /// <returns>Полный путь к найденному файлу или <see langword="null"/>.</returns>
    /// <remarks>
    /// Метод ожидает именно относительный путь к файлу
    /// относительно каждой проверяемой директории.
    /// </remarks>
    public static string? FindFileUpwards(string? startDirectory, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(startDirectory) || string.IsNullOrWhiteSpace(relativePath))
            return null;

        var trimmedRelativePath = relativePath.Trim();
        if (Path.IsPathRooted(trimmedRelativePath))
            return null;

        var trimmedStartDirectory = startDirectory!.Trim();

        DirectoryInfo? currentDirectory;
        try
        {
            currentDirectory = new DirectoryInfo(Path.GetFullPath(trimmedStartDirectory));
        }
        catch
        {
            return null;
        }

        while (currentDirectory != null)
        {
            string candidate;
            try
            {
                candidate = Path.GetFullPath(
                    Path.Combine(currentDirectory!.FullName, trimmedRelativePath));
            }
            catch
            {
                return null;
            }

            if (File.Exists(candidate))
                return candidate;

            currentDirectory = currentDirectory.Parent;
        }

        return null;
    }
}

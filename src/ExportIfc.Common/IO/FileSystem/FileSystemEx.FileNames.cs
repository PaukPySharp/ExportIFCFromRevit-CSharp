using ExportIfc.Config;

using System.Text.RegularExpressions;

namespace ExportIfc.IO;

public static partial class FileSystemEx
{
    /// <summary>
    /// Распознаёт суффикс резервной RVT-копии Revit.
    /// </summary>
    private static readonly Regex _reRevitBackupSuffix =
        new(@"\.0\d{3}\.rvt$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Гарантирует наличие указанного расширения у имени файла.
    /// </summary>
    /// <param name="name">Имя файла без гарантированного расширения.</param>
    /// <param name="defaultExtension">Требуемое расширение, с точкой или без неё.</param>
    /// <returns>Имя файла с гарантированным расширением.</returns>
    /// <remarks>
    /// Метод не переписывает имя файла и не заменяет уже существующее совпадающее расширение.
    /// Если расширение не задано, оно просто добавляется в конец строки.
    /// </remarks>
    public static string EnsureExtension(string name, string defaultExtension)
    {
        var trimmedName = (name ?? string.Empty).Trim();
        if (trimmedName.Length == 0)
            return trimmedName;

        var normalizedExtension = NormalizeExtension(defaultExtension);
        if (normalizedExtension.Length == 0)
            return trimmedName;

        if (trimmedName.EndsWith(normalizedExtension, StringComparison.OrdinalIgnoreCase))
            return trimmedName;

        return trimmedName + normalizedExtension;
    }

    /// <summary>
    /// Пытается нормализовать имя файла без каталогов.
    /// </summary>
    /// <param name="value">Исходное значение.</param>
    /// <param name="defaultExtension">Требуемое расширение, с точкой или без неё.</param>
    /// <returns>
    /// Нормализованное имя файла с гарантированным расширением
    /// либо <see langword="null"/>, если значение не является корректным именем файла.
    /// </returns>
    /// <remarks>
    /// Метод предназначен для значений из Excel и настроек, где ожидается именно имя файла,
    /// а не путь. Проверка отсекает каталоги, абсолютные пути, специальные имена
    /// <c>.</c> и <c>..</c>, недопустимые символы, зарезервированные имена Windows
    /// и имена с недопустимыми завершающими точками или пробелами.
    /// </remarks>
    public static string? TryNormalizeFileName(string value, string defaultExtension)
    {
        var trimmedValue = (value ?? string.Empty).Trim();
        if (trimmedValue.Length == 0)
            return null;

        if (trimmedValue == "." || trimmedValue == "..")
            return null;

        if (!string.Equals(
                trimmedValue,
                Path.GetFileName(trimmedValue),
                StringComparison.Ordinal))
        {
            return null;
        }

        if (trimmedValue.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return null;

        if (EndsWithDotOrSpace(trimmedValue))
            return null;

        var normalizedFileName = EnsureExtension(trimmedValue, defaultExtension);
        var stem = Path.GetFileNameWithoutExtension(normalizedFileName);

        return _windowsReservedFileNames.Contains(stem)
            ? null
            : normalizedFileName;
    }

    /// <summary>
    /// Проверяет, что файл является обычной RVT-моделью, а не известным
    /// служебным файлом Revit.
    /// </summary>
    /// <param name="path">Путь к файлу.</param>
    /// <returns>
    /// <see langword="true"/>, если имя файла оканчивается на
    /// <see cref="ProjectFileExtensions.Rvt"/> и не совпадает с известными
    /// служебными шаблонами Revit;
    /// иначе <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// Метод отсекает временные файлы и имена вида:
    /// <c>~$model.rvt</c>, <c>model.0001.rvt</c>, <c>model.ifc.rvt</c>.
    /// </remarks>
    public static bool IsPureRvt(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var fileName = Path.GetFileName(path.Trim());
        if (string.IsNullOrWhiteSpace(fileName))
            return false;

        if (!string.Equals(
                Path.GetExtension(fileName),
                ProjectFileExtensions.Rvt,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !LooksLikeRevitServiceRvt(fileName);
    }

    /// <summary>
    /// Проверяет, соответствует ли имя файла известным служебным шаблонам Revit.
    /// </summary>
    /// <param name="fileName">Имя файла без каталога.</param>
    /// <returns>
    /// <see langword="true"/>, если имя совпадает с известным служебным RVT-шаблоном;
    /// иначе <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// Метод распознаёт временные lock-файлы с префиксом <c>~$</c>,
    /// временные RVT-файлы IFC-обработки с суффиксом <c>.ifc.rvt</c>
    /// и резервные копии Revit с суффиксом, распознаваемым
    /// регулярным выражением резервной RVT-копии Revit.
    /// </remarks>
    private static bool LooksLikeRevitServiceRvt(string fileName)
    {
        return fileName.StartsWith("~$", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".ifc.rvt", StringComparison.OrdinalIgnoreCase)
            || _reRevitBackupSuffix.IsMatch(fileName);
    }

    /// <summary>
    /// Подготавливает безопасный фрагмент имени файла.
    /// </summary>
    /// <param name="value">Исходная строка.</param>
    /// <returns>Строка, пригодная для использования в имени файла.</returns>
    /// <remarks>
    /// Метод:
    /// 1. Заменяет недопустимые символы на подчёркивание.
    /// 2. Убирает недопустимые завершающие точки и пробелы.
    /// 3. Защищает от зарезервированных системных имён Windows.
    /// </remarks>
    public static string SanitizeFileNamePart(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        var invalidChars = Path.GetInvalidFileNameChars();
        var buffer = new char[value.Length];
        var hasChanges = false;

        for (var index = 0; index < value.Length; index++)
        {
            var currentChar = value[index];
            var isInvalid = Array.IndexOf(invalidChars, currentChar) >= 0;

            buffer[index] = isInvalid ? '_' : currentChar;
            hasChanges |= isInvalid;
        }

        var sanitized = hasChanges
            ? new string(buffer)
            : value;

        sanitized = ReplaceTrailingDotsAndSpaces(sanitized);
        sanitized = ProtectReservedWindowsFileNames(sanitized);

        return sanitized.Length == 0
            ? "_"
            : sanitized;
    }
}

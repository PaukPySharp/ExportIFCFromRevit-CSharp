using Microsoft.Win32;
using System.Security;

using ExportIfc.Config;

namespace ExportIfc.Revit;

/// <summary>
/// Ищет установленный <see cref="RevitConstants.ExecutableFileName"/> для заданной major-версии Revit.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Находит локальный exe нужной версии Revit.
/// 2. Ищет установку сначала в Autodesk-ветках реестра Windows, затем в стандартных каталогах Autodesk.
/// 3. Нормализует сырые registry-значения до полного пути к существующему exe-файлу.
///
/// Контракты:
/// 1. Поиск всегда привязан к переданному <c>revitMajor</c>.
/// 2. Возвращается только существующий путь к <see cref="RevitConstants.ExecutableFileName"/>.
/// 3. Вспомогательный метод чтения глобального override через
///    <see cref="EnvironmentVariableNames.RevitExe"/> объявлен отдельно от рабочего поиска.
///    Источник не участвует в основном алгоритме <see cref="TryFind(int)"/>,
///    потому что задаёт один общий путь без привязки к целевой major-версии.
/// </remarks>
internal sealed class RevitExeLocator : IRevitExeLocator
{
    private static readonly RegistryView[] _registryViews =
    [
        RegistryView.Registry64,
        RegistryView.Registry32
    ];

    private static readonly RegistryHive[] _registryHives =
    [
        RegistryHive.LocalMachine,
        RegistryHive.CurrentUser
    ];

    private static readonly string[] _installLocationValueNames =
    [
        "InstallLocation",
        "InstallationLocation",
        "InstallPath",
        "Location"
    ];

    private static readonly string[] _secondaryPathHintValueNameTokens =
    [
        "path",
        "location",
        "exe"
    ];

    /// <inheritdoc />
    public string? TryFind(int revitMajor)
    {
        // Глобальный override через переменную окружения сохранён как артефакт.
        // Источник задаёт один общий путь без связи с revitMajor, поэтому в рабочем
        // поиске не используется и не подменяет version-aware разрешение пути.
        // var fromEnvironment = TryFromEnvironmentOverride();
        // if (fromEnvironment is not null)
        //     return fromEnvironment;

        var fromRegistry = TryFromRegistry(revitMajor);
        if (fromRegistry is not null)
            return fromRegistry;

        return TryFromStandardDirectories(revitMajor);
    }

    /// <summary>
    /// Пробует найти путь к <see cref="RevitConstants.ExecutableFileName"/> через Autodesk-ветки реестра Windows.
    /// </summary>
    /// <param name="revitMajor">Целевая major-версия Revit.</param>
    /// <returns>Полный путь к exe или <see langword="null"/>.</returns>
    private static string? TryFromRegistry(int revitMajor)
    {
        foreach (var registryHive in _registryHives)
        {
            foreach (var registryView in _registryViews)
            {
                try
                {
                    using var baseKey = RegistryKey.OpenBaseKey(registryHive, registryView);

                    var exePath = TryFromAutodeskRevitKeys(baseKey, revitMajor);
                    if (exePath is not null)
                        return exePath;
                }
                catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
                {
                    // Один недоступный hive/view не должен обрывать поиск по остальным источникам.
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Ищет путь к <see cref="RevitConstants.ExecutableFileName"/> в типовых Autodesk-ветках для заданной версии.
    /// </summary>
    /// <param name="baseKey">Базовый ключ выбранного hive/view.</param>
    /// <param name="revitMajor">Целевая major-версия Revit.</param>
    /// <returns>Полный путь к exe или <see langword="null"/>.</returns>
    /// <remarks>
    /// Сначала проверяются два распространённых верхних ключа версии,
    /// затем выполняется fallback-поиск по дочерним ключам ветки Revit,
    /// имя которых похоже на нужную major-версию.
    /// </remarks>
    private static string? TryFromAutodeskRevitKeys(RegistryKey baseKey, int revitMajor)
    {
        var exactKeyCandidates = new[]
        {
            $@"SOFTWARE\Autodesk\Revit\Autodesk Revit {revitMajor}",
            $@"SOFTWARE\Autodesk\Revit\{revitMajor}"
        };

        foreach (var subKeyPath in exactKeyCandidates)
        {
            var exePath = TryReadExeFromRegistrySubtree(baseKey, subKeyPath);
            if (exePath is not null)
                return exePath;
        }

        using var revitRoot = baseKey.OpenSubKey(@"SOFTWARE\Autodesk\Revit");
        if (revitRoot is null)
            return null;

        foreach (var subKeyName in revitRoot.GetSubKeyNames())
        {
            if (!IsMatchingRevitSubKeyName(subKeyName, revitMajor))
                continue;

            var exePath = TryReadExeFromRegistrySubtree(revitRoot, subKeyName);
            if (exePath is not null)
                return exePath;
        }

        return null;
    }

    /// <summary>
    /// Открывает указанную подветку и ищет в ней путь к <see cref="RevitConstants.ExecutableFileName"/>
    /// с обходом дочерних ключей.
    /// </summary>
    /// <param name="root">Корневой ключ, относительно которого открывается подветка.</param>
    /// <param name="subKeyPath">Путь к подветке с данными установки.</param>
    /// <returns>Полный путь к exe или <see langword="null"/>.</returns>
    private static string? TryReadExeFromRegistrySubtree(RegistryKey root, string subKeyPath)
    {
        using var key = root.OpenSubKey(subKeyPath);
        return key is null
            ? null
            : TryReadExeFromRegistrySubtree(key);
    }

    /// <summary>
    /// Ищет путь к <see cref="RevitConstants.ExecutableFileName"/> в открытом ключе реестра
    /// и его дочерних ключах.
    /// </summary>
    /// <param name="key">Открытый registry-ключ.</param>
    /// <returns>Полный путь к exe или <see langword="null"/>.</returns>
    /// <remarks>
    /// Install-related значения у Revit могут лежать как в ключе версии,
    /// так и в его внутренних подветках.
    /// Поиск выполняется по всему subtree.
    /// </remarks>
    private static string? TryReadExeFromRegistrySubtree(RegistryKey key)
    {
        var fromCurrentKey = TryReadExeFromCurrentRegistryKey(key);
        if (fromCurrentKey is not null)
            return fromCurrentKey;

        string[] subKeyNames;
        try
        {
            subKeyNames = key.GetSubKeyNames();
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
        {
            return null;
        }

        foreach (var subKeyName in subKeyNames)
        {
            try
            {
                using var childKey = key.OpenSubKey(subKeyName);
                if (childKey is null)
                    continue;

                var exePath = TryReadExeFromRegistrySubtree(childKey);
                if (exePath is not null)
                    return exePath;
            }
            catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
            {
                // Пропускаем только проблемную ветку и продолжаем поиск.
            }
        }

        return null;
    }

    /// <summary>
    /// Пробует извлечь путь к <see cref="RevitConstants.ExecutableFileName"/> из значений
    /// текущего registry-ключа.
    /// </summary>
    /// <param name="key">Открытый registry-ключ.</param>
    /// <returns>Полный путь к exe или <see langword="null"/>.</returns>
    /// <remarks>
    /// Сначала проверяются известные install-related имена значений,
    /// затем default value и только после этого — остальные строковые значения,
    /// чьи имена похожи на носители пути к install-location или exe.
    /// </remarks>
    private static string? TryReadExeFromCurrentRegistryKey(RegistryKey key)
    {
        try
        {
            foreach (var valueName in _installLocationValueNames)
            {
                var exePath = NormalizeExePath(key.GetValue(valueName) as string);
                if (exePath is not null)
                    return exePath;
            }

            var fromDefaultValue = NormalizeExePath(key.GetValue(string.Empty) as string);
            if (fromDefaultValue is not null)
                return fromDefaultValue;

            foreach (var valueName in key.GetValueNames())
            {
                if (_installLocationValueNames.Contains(valueName, StringComparer.OrdinalIgnoreCase))
                    continue;

                if (!LooksLikePathCarrierValueName(valueName))
                    continue;

                var exePath = NormalizeExePath(key.GetValue(valueName) as string);
                if (exePath is not null)
                    return exePath;
            }

            return null;
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
        {
            return null;
        }
    }

    /// <summary>
    /// Ищет путь к <see cref="RevitConstants.ExecutableFileName"/> в стандартных каталогах Autodesk.
    /// </summary>
    /// <param name="revitMajor">Целевая major-версия Revit.</param>
    /// <returns>Полный путь к exe или <see langword="null"/>.</returns>
    /// <remarks>
    /// Сначала проверяется ожидаемый каталог <c>Autodesk\Revit {major}</c>,
    /// затем выполняется fallback-поиск по каталогам, начинающимся с <c>Revit {major}</c>.
    /// </remarks>
    private static string? TryFromStandardDirectories(int revitMajor)
    {
        foreach (var baseDir in new[]
                 {
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
                 })
        {
            if (string.IsNullOrWhiteSpace(baseDir))
                continue;

            var directCandidate = Path.Combine(
                baseDir,
                "Autodesk",
                $"Revit {revitMajor}",
                RevitConstants.ExecutableFileName);

            if (IsExistingRevitExe(directCandidate))
                return Path.GetFullPath(directCandidate);

            var autodeskDir = Path.Combine(baseDir, "Autodesk");
            if (!Directory.Exists(autodeskDir))
                continue;

            IEnumerable<string> revitDirs;
            try
            {
                revitDirs = Directory.EnumerateDirectories(
                    autodeskDir,
                    $"Revit {revitMajor}*",
                    SearchOption.TopDirectoryOnly);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var revitDir in revitDirs)
            {
                var exeCandidate = Path.Combine(revitDir, RevitConstants.ExecutableFileName);
                if (IsExistingRevitExe(exeCandidate))
                    return Path.GetFullPath(exeCandidate);
            }
        }

        return null;
    }

    /// <summary>
    /// Проверяет, похоже ли имя Autodesk-подключа на заданную major-версию Revit.
    /// </summary>
    private static bool IsMatchingRevitSubKeyName(string subKeyName, int revitMajor)
    {
        var majorText = revitMajor.ToString();

        return string.Equals(subKeyName, majorText, StringComparison.OrdinalIgnoreCase)
               || string.Equals(subKeyName, $"Autodesk Revit {majorText}", StringComparison.OrdinalIgnoreCase)
               || subKeyName.Contains($" {majorText}", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Проверяет, стоит ли рассматривать имя значения как возможный носитель пути.
    /// </summary>
    private static bool LooksLikePathCarrierValueName(string valueName)
    {
        if (string.IsNullOrWhiteSpace(valueName))
            return false;

        foreach (var token in _secondaryPathHintValueNameTokens)
        {
            if (valueName.Contains(token, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Приводит строку к полному пути к существующему
    /// <see cref="RevitConstants.ExecutableFileName"/>.
    /// </summary>
    /// <param name="pathOrDirectory">
    /// Исходная строка, которая может указывать либо на каталог установки,
    /// либо сразу на <see cref="RevitConstants.ExecutableFileName"/>.
    /// </param>
    /// <returns>Полный путь к exe или <see langword="null"/>.</returns>
    /// <remarks>
    /// Метод принимает только два практических формата:
    /// 1. полный путь к <see cref="RevitConstants.ExecutableFileName"/>;
    /// 2. каталог установки, внутри которого находится этот exe.
    ///
    /// Другие exe-файлы, включая installer и setup, не рассматриваются.
    /// </remarks>
    private static string? NormalizeExePath(string? pathOrDirectory)
    {
        if (string.IsNullOrWhiteSpace(pathOrDirectory))
            return null;

        var raw = pathOrDirectory.Trim();

        var directExePath = TryExtractRevitExePath(raw);
        if (directExePath is not null)
        {
            if (!Path.IsPathRooted(directExePath))
                return null;

            return IsExistingRevitExe(directExePath)
                ? Path.GetFullPath(directExePath)
                : null;
        }

        var trimmedDirectory = raw.Trim().Trim('"');
        if (!Path.IsPathRooted(trimmedDirectory))
            return null;

        var combinedExePath = Path.Combine(trimmedDirectory, RevitConstants.ExecutableFileName);
        return IsExistingRevitExe(combinedExePath)
            ? Path.GetFullPath(combinedExePath)
            : null;
    }

    /// <summary>
    /// Выделяет прямой путь именно к <see cref="RevitConstants.ExecutableFileName"/>
    /// из сырой registry-строки.
    /// </summary>
    /// <param name="rawValue">Сырая строка из реестра.</param>
    /// <returns>Путь к <see cref="RevitConstants.ExecutableFileName"/> или <see langword="null"/>.</returns>
    /// <remarks>
    /// Поддерживаются практические форматы вроде:
    /// 1. <c>"C:\...\Revit.exe,0"</c>;
    /// 2. <c>"C:\...\Revit.exe" /something</c>.
    ///
    /// Для locator'а нужна только exe-часть строки и только для точного
    /// <see cref="RevitConstants.ExecutableFileName"/>.
    /// </remarks>
    private static string? TryExtractRevitExePath(string rawValue)
    {
        var quotedPath = TryExtractQuotedPath(rawValue);
        if (HasExactRevitExeFileName(quotedPath))
            return quotedPath;

        var fileName = RevitConstants.ExecutableFileName;
        var fileNameIndex = rawValue.LastIndexOf(fileName, StringComparison.OrdinalIgnoreCase);
        if (fileNameIndex < 0)
            return null;

        var nextCharIndex = fileNameIndex + fileName.Length;
        var hasValidPrefix = fileNameIndex == 0
            || IsExecutablePathPrefixBoundary(rawValue[fileNameIndex - 1]);

        var hasValidSuffix = nextCharIndex == rawValue.Length
            || IsExecutablePathSuffixBoundary(rawValue[nextCharIndex]);

        if (!hasValidPrefix || !hasValidSuffix)
            return null;

        var path = rawValue.Substring(0, nextCharIndex)
            .Trim()
            .Trim('"');

        return HasExactRevitExeFileName(path)
            ? path
            : null;
    }

    /// <summary>
    /// Возвращает содержимое первой пары кавычек из строки.
    /// </summary>
    private static string? TryExtractQuotedPath(string rawValue)
    {
        var firstQuote = rawValue.IndexOf('"');
        if (firstQuote < 0)
            return null;

        var secondQuote = rawValue.IndexOf('"', firstQuote + 1);
        if (secondQuote <= firstQuote)
            return null;

        var path = rawValue.Substring(firstQuote + 1, secondQuote - firstQuote - 1).Trim();
        return string.IsNullOrWhiteSpace(path)
            ? null
            : path;
    }

    /// <summary>
    /// Проверяет, указывает ли путь точно на <see cref="RevitConstants.ExecutableFileName"/>.
    /// </summary>
    private static bool HasExactRevitExeFileName(string? path)
    {
        return !string.IsNullOrWhiteSpace(path)
               && string.Equals(
                   Path.GetFileName(path),
                   RevitConstants.ExecutableFileName,
                   StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Проверяет, существует ли путь и указывает ли он именно на
    /// <see cref="RevitConstants.ExecutableFileName"/>.
    /// </summary>
    private static bool IsExistingRevitExe(string path)
    {
        return HasExactRevitExeFileName(path) && File.Exists(path);
    }

    /// <summary>
    /// Проверяет, допустим ли символ перед именем exe внутри сырой строки.
    /// </summary>
    private static bool IsExecutablePathPrefixBoundary(char character)
    {
        return character == Path.DirectorySeparatorChar
               || character == Path.AltDirectorySeparatorChar
               || character == '"';
    }

    /// <summary>
    /// Проверяет, допустим ли символ после имени exe внутри сырой строки.
    /// </summary>
    private static bool IsExecutablePathSuffixBoundary(char character)
    {
        return character == '"'
               || character == ','
               || char.IsWhiteSpace(character);
    }

    /*
    /// <summary>
    /// Заготовка для возможного будущего сценария явного override пути
    /// к <see cref="RevitConstants.ExecutableFileName"/>.
    /// </summary>
    /// <returns>
    /// Полный путь к exe-файлу из переменной окружения
    /// либо <see langword="null"/>, если значение отсутствует или некорректно.
    /// </returns>
    /// <remarks>
    /// Вспомогательный метод чтения глобального override пути
    /// к <see cref="RevitConstants.ExecutableFileName"/>.
    /// Метод не связывает значение переменной окружения с requested major-версией.
    /// </remarks>
    private static string? TryFromEnvironmentOverride()
    {
        var overrideExe = Environment.GetEnvironmentVariable(EnvironmentVariableNames.RevitExe);
        return NormalizeExePath(overrideExe);
    }
    */
}

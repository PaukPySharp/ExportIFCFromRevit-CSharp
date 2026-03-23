namespace ExportIfc.Config;

/// <summary>
/// Имена каталогов, связанных с txt-логами проекта.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Централизует имена каталогов для обычных и технических логов.
/// 2. Убирает лог-специфичные имена из <see cref="ProjectPaths"/>.
/// 3. Делает структуру логов отдельно находимой внутри конфиг-слоя.
///
/// Контракты:
/// 1. Здесь задаются только имена подпапок, без абсолютных путей.
/// 2. Каталог <see cref="Tech"/> строится внутри каталога <see cref="Logs"/>.
/// 3. Каталог <see cref="Console"/> строится внутри каталога <see cref="Tech"/>
///    и используется для зеркал консольного вывода.
/// </remarks>
public static class LogDirectoryNames
{
    /// <summary>
    /// Имя подпапки логов внутри <see cref="ProjectDirectoryNames.AdminData"/>.
    /// </summary>
    public const string Logs = "_logs";

    /// <summary>
    /// Имя подпапки технических логов внутри <see cref="Logs"/>.
    /// </summary>
    public const string Tech = "_tech";

    /// <summary>
    /// Имя подпапки зеркал консольного вывода внутри <see cref="Tech"/>.
    /// </summary>
    public const string Console = "_console";
}

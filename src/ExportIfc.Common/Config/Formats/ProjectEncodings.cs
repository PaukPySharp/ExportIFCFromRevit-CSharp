using System.Text;

namespace ExportIfc.Config;

/// <summary>
/// Общие текстовые кодировки проекта.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Централизует кодировки для txt/json артефактов проекта.
/// 2. Убирает повторяющиеся локальные экземпляры <see cref="UTF8Encoding"/>.
/// 3. Делает файловый контракт по кодировкам явно находимым в config-слое.
///
/// Контракты:
/// 1. <see cref="Utf8NoBom"/> используется для проектных файлов, которые должны писаться без BOM.
/// 2. Изменение этой кодировки влияет на совместимость логов, transport-файлов и служебного JSON.
/// </remarks>
public static class ProjectEncodings
{
    /// <summary>
    /// UTF-8 без BOM для проектных txt/json файлов.
    /// </summary>
    public static readonly UTF8Encoding Utf8NoBom = new(false);
}

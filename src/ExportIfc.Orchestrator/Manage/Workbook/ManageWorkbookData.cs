using ExportIfc.Config;
using ExportIfc.Models;

namespace ExportIfc.Manage;

/// <summary>
/// Результат загрузки управляющей Excel-книги
/// <see cref="ProjectFileNames.ManageWorkbook"/>.
/// </summary>
/// <remarks>
/// Назначение:
/// Хранит итоговые данные, собранные из управляющей Excel-книги:
/// список моделей для дальнейшей обработки, ignore-список
/// и диагностические сообщения по проблемам с временем модификации.
///
/// Контракты:
/// 1. Экземпляр представляет собой уже собранный результат чтения workbook,
///    готовый для передачи в orchestration-слой.
/// 2. Коллекция <see cref="Models"/> содержит только модели,
///    прошедшие разбор и базовую валидацию.
/// 3. Коллекция <see cref="Ignore"/> хранит нормализованные полные пути
///    к моделям, исключённым из обработки.
/// 4. Коллекция <see cref="MTimeIssues"/> содержит диагностические сообщения
///    по файлам, для которых не удалось определить время модификации.
/// </remarks>
public sealed class ManageWorkbookData
{
    /// <summary>
    /// Список моделей, подготовленных к дальнейшей обработке.
    /// </summary>
    public List<RevitModel> Models { get; init; } = [];

    /// <summary>
    /// Набор полных путей к моделям, исключённым из обработки.
    /// </summary>
    public HashSet<string> Ignore { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Сообщения о моделях, для которых не удалось определить время модификации.
    /// </summary>
    public List<string> MTimeIssues { get; init; } = [];
}

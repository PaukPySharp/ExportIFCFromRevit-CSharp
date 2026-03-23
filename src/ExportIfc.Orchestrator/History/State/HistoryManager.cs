using ExportIfc.IO;
using ExportIfc.Models;

namespace ExportIfc.History;

/// <summary>
/// Менеджер рабочей истории состояний моделей.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Хранит в памяти рабочую историю состояний RVT-моделей в виде пар
///    «нормализованный путь + время модификации с точностью до минуты».
/// 2. Позволяет быстро проверять, считается ли модель уже актуальной
///    относительно последнего известного состояния истории.
/// 3. Обновляет рабочую историю состояния модели,
///    не зная ничего о конкретном способе хранения на диске.
///
/// Контракты:
/// 1. Пути внутри менеджера всегда хранятся в нормализованном виде.
/// 2. Время модификации внутри менеджера всегда хранится без секунд,
///    через <see cref="FileTime.NormalizeMinute(DateTime)"/>.
/// 3. Точный дубль записи повторно не добавляется.
/// 4. Если по модели пришло более раннее время, чем уже есть в истории,
///    записи «из будущего» по этому пути удаляются.
/// 5. Класс не зависит от Excel, workbook-файлов и других деталей persistence-слоя.
/// </remarks>
internal sealed class HistoryManager
{
    private static readonly RowComparer _rowEqualityComparer = new();

    private readonly List<HistoryRow> _rows = [];
    private readonly Dictionary<string, DateTime> _last = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<HistoryRow> _seen = new(_rowEqualityComparer);

    /// <summary>
    /// Создаёт пустой менеджер истории.
    /// </summary>
    /// <remarks>
    /// Основная точка входа — фабричный метод
    /// <see cref="FromRows(IEnumerable{HistoryRow})"/>.
    /// Метод создаёт и заполняет менеджер начальными данными.
    /// </remarks>
    private HistoryManager()
    {
    }

    /// <summary>
    /// Создаёт менеджер истории из ранее сохранённых строк.
    /// </summary>
    /// <param name="rows">Строки истории, прочитанные из внешнего хранилища.</param>
    /// <returns>Заполненный экземпляр менеджера истории.</returns>
    /// <remarks>
    /// Входные строки дополнительно проходят через внутреннюю нормализацию.
    /// Нормализация охватывает регистр пути, секунды времени и точные дубли.
    /// </remarks>
    internal static HistoryManager FromRows(IEnumerable<HistoryRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var history = new HistoryManager();

        foreach (var row in rows)
        {
            var normalizedPath = FileSystemEx.NormalizePath(row.Path);
            var normalizedMinute = FileTime.NormalizeMinute(row.LastModifiedMinute);
            history.Add(normalizedPath, normalizedMinute);
        }

        return history;
    }

    /// <summary>
    /// Проверяет, считается ли модель уже актуальной по истории.
    /// </summary>
    /// <param name="model">Модель Revit, которую нужно проверить.</param>
    /// <returns>
    /// <see langword="true"/>, если для этого пути в истории уже есть
    /// последнее состояние и оно совпадает с текущим временем модификации модели;
    /// иначе <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// Проверка опирается только на нормализованный путь модели
    /// и время модификации с точностью до минуты.
    /// Если истории по пути ещё нет, модель считается неактуализированной.
    /// </remarks>
    public bool IsUpToDate(RevitModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return _last.TryGetValue(model.RvtPath, out var lastKnownMinute)
               && lastKnownMinute == model.LastModifiedMinute;
    }

    /// <summary>
    /// Обновляет историю по результатам обработки модели.
    /// </summary>
    /// <param name="model">Модель, для которой нужно обновить историю.</param>
    /// <remarks>
    /// Если модель уже присутствует в истории с тем же временем модификации,
    /// метод ничего не меняет.
    ///
    /// Если время модели оказалось меньше последнего сохранённого,
    /// это трактуется как откат состояния файла или замена модели
    /// более старой версией. В этом случае более поздние записи по тому же пути
    /// удаляются, после чего добавляется фактическое состояние модели.
    /// </remarks>
    public void UpdateRecord(RevitModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var path = model.RvtPath;
        var lastModifiedMinute = model.LastModifiedMinute;

        if (_last.TryGetValue(path, out var lastKnownMinute))
        {
            if (lastModifiedMinute == lastKnownMinute)
                return;

            if (lastModifiedMinute < lastKnownMinute)
                PruneFutureRecords(path, lastModifiedMinute);
        }

        Add(path, lastModifiedMinute);
    }

    /// <summary>
    /// Возвращает текущий снимок истории в детерминированном порядке.
    /// </summary>
    /// <returns>
    /// Набор строк истории, отсортированный по пути модели
    /// и затем по времени модификации в обратном порядке.
    /// </returns>
    /// <remarks>
    /// Такой порядок удобен для стабильного сохранения в workbook-файл
    /// и уменьшает визуальный шум при сравнении результатов между прогонами.
    /// </remarks>
    internal IReadOnlyList<HistoryRow> GetRowsSnapshot()
    {
        return _rows
            .OrderBy(row => row.Path, StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(row => row.LastModifiedMinute)
            .ToArray();
    }

    /// <summary>
    /// Добавляет запись истории после нормализации пути и времени.
    /// </summary>
    /// <param name="normalizedPath">Путь к модели.</param>
    /// <param name="normalizedMinute">Время модификации модели.</param>
    /// <remarks>
    /// Это единая точка добавления уже нормализованной записи в память.
    /// Метод:
    /// 1. принимает нормализованный путь;
    /// 2. принимает время модификации, уже приведённое к точности минуты;
    /// 3. отбрасывает точные дубли;
    /// 4. обновляет индекс последних состояний.
    /// </remarks>
    private void Add(string normalizedPath, DateTime normalizedMinute)
    {
        var row = new HistoryRow(normalizedPath, normalizedMinute);
        if (!_seen.Add(row))
            return;

        _rows.Add(row);

        if (!_last.TryGetValue(normalizedPath, out var currentLast)
            || normalizedMinute > currentLast)
        {
            _last[normalizedPath] = normalizedMinute;
        }
    }

    /// <summary>
    /// Удаляет записи, которые стали новее фактического состояния модели.
    /// </summary>
    /// <param name="path">Нормализованный путь к модели.</param>
    /// <param name="threshold">
    /// Фактическое время модели после отката.
    /// Все записи новее этого значения будут удалены.
    /// </param>
    /// <remarks>
    /// Метод используется только в сценарии отката времени модели.
    /// После удаления он заново собирает набор точных записей
    /// и перестраивает индекс последнего состояния по этому пути.
    /// </remarks>
    private void PruneFutureRecords(string path, DateTime threshold)
    {
        _rows.RemoveAll(row =>
            row.Path.Equals(path, StringComparison.OrdinalIgnoreCase)
            && row.LastModifiedMinute > threshold);

        _seen.Clear();
        foreach (var row in _rows)
            _seen.Add(row);

        ReindexLast(path);
    }

    /// <summary>
    /// Перестраивает индекс последнего времени по одному пути модели.
    /// </summary>
    /// <param name="path">Нормализованный путь к модели.</param>
    /// <remarks>
    /// Метод пересчитывает индекс последнего времени по пути
    /// на основании текущего набора строк истории.
    /// Если по пути отсутствуют строки, индекс для него удаляется.
    /// </remarks>
    private void ReindexLast(string path)
    {
        var dates = _rows
            .Where(row => row.Path.Equals(path, StringComparison.OrdinalIgnoreCase))
            .Select(row => row.LastModifiedMinute)
            .ToArray();

        if (dates.Length == 0)
        {
            _last.Remove(path);
            return;
        }

        _last[path] = dates.Max();
    }

    /// <summary>
    /// Сравнение строк истории без учёта регистра пути.
    /// </summary>
    /// <remarks>
    /// Используется только для <see cref="_seen"/>,
    /// который защищает менеджер от повторного добавления
    /// одной и той же записи истории.
    /// </remarks>
    private sealed class RowComparer : IEqualityComparer<HistoryRow>
    {
        /// <summary>
        /// Проверяет равенство двух строк истории.
        /// </summary>
        /// <param name="x">Первая строка.</param>
        /// <param name="y">Вторая строка.</param>
        /// <returns>
        /// <see langword="true"/>, если путь совпадает без учёта регистра
        /// и время модификации одинаково; иначе <see langword="false"/>.
        /// </returns>
        public bool Equals(HistoryRow x, HistoryRow y)
            => string.Equals(x.Path, y.Path, StringComparison.OrdinalIgnoreCase)
               && x.LastModifiedMinute == y.LastModifiedMinute;

        /// <summary>
        /// Возвращает hash-код строки истории по правилу сравнения без учёта регистра пути.
        /// </summary>
        /// <param name="obj">Строка истории.</param>
        /// <returns>Hash-код для использования в <see cref="HashSet{T}"/>.</returns>
        public int GetHashCode(HistoryRow obj)
            => HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Path),
                obj.LastModifiedMinute);
    }
}

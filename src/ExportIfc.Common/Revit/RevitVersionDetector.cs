using System.Text;
using System.Text.RegularExpressions;

namespace ExportIfc.Revit;

/// <summary>
/// Быстрое определение версии Revit по бинарному содержимому RVT-файла
/// без запуска Revit.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Пытается извлечь major-версию Revit и номер сборки прямо из бинарного содержимого файла.
/// 2. Ищет текстовые маркеры в UTF-16 LE и UTF-16 BE.
///    Служебные строки внутри RVT могут встречаться в обеих кодировках.
/// 3. Сначала читает только префикс файла, чтобы не тащить в память весь RVT без необходимости.
/// 4. При неполном результате на префиксе сначала выполняет потоковый проход по файлу
///    ограниченными окнами и только затем при необходимости переходит к чтению
///    полного содержимого.
///
/// Контракты:
/// 1. Методы семейства Try* не выбрасывают исключения наружу: на любой ошибке возвращается <see langword="null"/>.
/// 2. Валидным major-годом считается только значение в диапазоне <c>2000..2100</c>.
/// 3. Отсутствие build-номера не считается ошибкой, если год версии удалось определить.
/// 4. Класс не открывает Revit и не зависит от Revit API.
/// </remarks>
public static class RevitVersionDetector
{
    // RVT хранит служебные текстовые фрагменты в UTF-16.
    // Проверяем обе распространённые формы: little-endian и big-endian.
    private static readonly Encoding _encLe = Encoding.Unicode;

    private static readonly Encoding _encBe = Encoding.BigEndianUnicode;

    // Основные сигнатуры, по которым можно вытащить год версии и build.
    // Маркеры ищутся как сырые байты, чтобы не декодировать весь файл целиком.
    private static readonly byte[] _fmtLe = _encLe.GetBytes("Format:");

    private static readonly byte[] _fmtBe = _encBe.GetBytes("Format:");
    private static readonly byte[] _bldLe = _encLe.GetBytes("Build:");
    private static readonly byte[] _bldBe = _encBe.GetBytes("Build:");
    private static readonly byte[] _autLe = _encLe.GetBytes("Autodesk Revit");
    private static readonly byte[] _autBe = _encBe.GetBytes("Autodesk Revit");

    // Объём быстрого чтения начального фрагмента файла.
    private const int _readHeadBytes = 128 * 1024;

    // Параметры потокового fallback-поиска.
    // Читаем файл ограниченными окнами и сохраняем overlap,
    // чтобы не терять маркеры на границе чанков.
    private const int _streamChunkBytes = 128 * 1024;

    private const int _streamOverlapBytes = 512;

    // Размеры хвостов после найденных маркеров.
    // Они берутся с запасом, достаточным для извлечения коротких текстовых значений
    // без попытки декодировать большой фрагмент бинарного мусора.
    private const int _yearTailBytes = 32;

    private const int _buildTailBytes = 64;
    private const int _autodeskSuffixBytes = 128;

    private const int _minYear = 2000;
    private const int _maxYear = 2100;

    // Извлекаем только год формата 20xx.
    private static readonly Regex _reYear =
        new(@"\b(20\d{2})\b", RegexOptions.Compiled);

    // Build допускает цифры, точки и подчёркивания.
    // Например: 24.0.4.427
    private static readonly Regex _reBuild =
        new(@"[\d._]+", RegexOptions.Compiled);

    /// <summary>
    /// Результат распознавания версии RVT-файла.
    /// </summary>
    /// <param name="Year">Major-год версии Revit.</param>
    /// <param name="Build">Номер сборки, если его удалось извлечь.</param>
    public sealed record RevitVersionInfo(int Year, string? Build);

    /// <summary>
    /// Возвращает major-версию Revit по RVT-файлу.
    /// </summary>
    /// <param name="rvtPath">Путь к RVT-файлу.</param>
    /// <returns>
    /// Год версии Revit, например <c>2022</c>,
    /// либо <see langword="null"/>, если год не удалось определить.
    /// </returns>
    /// <remarks>
    /// Метод извлекает только major-версию.
    /// Сценарий используется при batch-планировании и маршрутизации моделей
    /// по версиям Revit, где build-номер не участвует в принятии решения.
    /// </remarks>
    public static int? TryGetRevitMajor(string rvtPath)
    {
        var info = TryReadVersionInfo(rvtPath, includeBuild: false);
        return info?.Year;
    }

    /// <summary>
    /// Возвращает major-версию Revit и build-номер по RVT-файлу.
    /// </summary>
    /// <param name="rvtPath">Путь к RVT-файлу.</param>
    /// <returns>
    /// Объект с годом версии и build-номером, если удалось извлечь год;
    /// иначе <see langword="null"/>.
    /// </returns>
    /// <remarks>
    /// Метод работает в режиме best effort:
    /// 1. Сначала пытается найти год и build в префиксе файла.
    /// 2. Если хотя бы одно из значений не найдено, выполняет потоковый проход по файлу
    ///    и добирает только недостающие части версии.
    /// 3. Если потокового прохода недостаточно, использует чтение полного файла
    ///    как последний резерв.
    ///
    /// Build не является обязательным результатом.
    /// Если год найден, но build отсутствует, метод всё равно возвращает результат.
    /// </remarks>
    public static RevitVersionInfo? TryGetInfo(string rvtPath) =>
        TryReadVersionInfo(rvtPath, includeBuild: true);

    /// <summary>
    /// Общий конвейер чтения версии RVT-файла.
    /// </summary>
    /// <param name="rvtPath">Путь к RVT-файлу.</param>
    /// <param name="includeBuild">
    /// Нужно ли дополнительно извлекать build-номер.
    /// Если флаг выключен, извлечение build полностью пропускается.
    /// </param>
    /// <returns>
    /// Результат распознавания версии либо <see langword="null"/>,
    /// если не удалось определить год версии Revit.
    /// </returns>
    /// <remarks>
    /// Этот метод нужен для двух сценариев:
    /// 1. Быстро получить только major-год без лишней работы по build.
    /// 2. При необходимости тем же алгоритмом получить и год, и build.
    ///
    /// Важно, что алгоритм распознавания года не меняется:
    /// сначала ищем данные в префиксе файла, затем при необходимости выполняем
    /// потоковый проход, а чтение полного файла оставляем последним резервом.
    /// Меняется только то, подключается ли извлечение build в текущем вызове.
    /// </remarks>
    private static RevitVersionInfo? TryReadVersionInfo(string rvtPath, bool includeBuild)
    {
        try
        {
            // 1) Быстрый путь: читаем только "голову" файла.
            var head = ReadPrefix(rvtPath, _readHeadBytes);
            var partial = ExtractVersionData(head, includeBuild);

            // Если на префиксе уже получено всё, что требуется в этом сценарии,
            // полный файл читать не нужно.
            if (HasEnoughData(partial.Year, partial.Build, includeBuild))
                return new RevitVersionInfo(partial.Year!.Value, partial.Build);

            // 2) Безопасный fallback: читаем файл потоково ограниченными окнами.
            var streamed = ReadByChunksAndMergeMissingVersionData(rvtPath, partial, includeBuild);
            if (HasEnoughData(streamed.Year, streamed.Build, includeBuild))
                return new RevitVersionInfo(streamed.Year!.Value, streamed.Build);

            // 3) Последний резерв: читаем весь файл только тогда,
            // когда префикс и потоковый проход не дали достаточного результата.
            var data = File.ReadAllBytes(rvtPath);
            var (Year, Build) = MergeMissingVersionData(streamed, data, includeBuild);

            return Year is null
                ? null
                : new RevitVersionInfo(Year.Value, Build);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Извлекает доступные части версии из указанного участка данных.
    /// </summary>
    /// <param name="data">Бинарные данные RVT-файла или его префикса.</param>
    /// <param name="includeBuild">
    /// Нужно ли дополнительно пытаться извлечь build.
    /// </param>
    /// <returns>Пара "год / build", где любая часть может отсутствовать.</returns>
    /// <remarks>
    /// Год и build здесь извлекаются независимо:
    /// - год ищется сначала по <c>Format:</c>, затем через fallback по <c>Autodesk Revit</c>;
    /// - build ищется только если это явно запрошено вызывающим кодом.
    /// </remarks>
    private static (int? Year, string? Build) ExtractVersionData(byte[] data, bool includeBuild)
    {
        var year = ExtractYear(data) ?? ExtractYearFromAutodesk(data);
        var build = includeBuild ? ExtractBuild(data) : null;

        return (year, build);
    }

    /// <summary>
    /// Дочитывает только недостающие части версии из полного содержимого файла.
    /// </summary>
    /// <param name="partial">Частичный результат, полученный на префиксе файла.</param>
    /// <param name="data">Полное содержимое RVT-файла.</param>
    /// <param name="includeBuild">
    /// Нужно ли пытаться добрать build-номер.
    /// </param>
    /// <returns>Объединённый результат после дозаполнения недостающих частей.</returns>
    /// <remarks>
    /// Метод не пересчитывает уже найденные значения без причины:
    /// - если год найден на префиксе, он сохраняется;
    /// - если build найден на префиксе, он тоже сохраняется;
    /// - из полного файла дочитываются только отсутствующие части.
    /// </remarks>
    private static (int? Year, string? Build) MergeMissingVersionData(
        (int? Year, string? Build) partial,
        byte[] data,
        bool includeBuild)
    {
        var year = partial.Year ?? ExtractYear(data) ?? ExtractYearFromAutodesk(data);
        var build = includeBuild
            ? partial.Build ?? ExtractBuild(data)
            : null;

        return (year, build);
    }

    /// <summary>
    /// Проверяет, достаточно ли данных для завершения текущего сценария распознавания.
    /// </summary>
    /// <param name="year">Найденный год версии.</param>
    /// <param name="build">Найденный build-номер.</param>
    /// <param name="includeBuild">
    /// Признак сценария, в котором build обязателен для "полного" результата.
    /// </param>
    /// <returns>
    /// <see langword="true"/>, если повторное чтение полного файла уже не нужно.
    /// </returns>
    /// <remarks>
    /// Для сценария "только major" достаточно найти год.
    /// Для сценария "год + build" быстрый путь считается полным
    /// только если удалось найти и год, и build.
    /// </remarks>
    private static bool HasEnoughData(int? year, string? build, bool includeBuild) =>
        year is not null && (!includeBuild || build is not null);

    /// <summary>
    /// Читает начальный фрагмент файла.
    /// </summary>
    /// <param name="path">Путь к файлу.</param>
    /// <param name="bytes">Максимальное число байт для чтения.</param>
    /// <returns>Массив байт фактически прочитанного префикса.</returns>
    /// <remarks>
    /// Поток может вернуть только часть запрошенного объёма за один вызов <c>Read</c>.
    /// Чтение выполняется циклом до исчерпания данных или достижения лимита.
    /// </remarks>
    private static byte[] ReadPrefix(string path, int bytes)
    {
        using var fs = File.OpenRead(path);

        var length = (int)Math.Min(fs.Length, bytes);
        var buffer = new byte[length];

        var offset = 0;
        while (offset < length)
        {
            var read = fs.Read(buffer, offset, length - offset);
            if (read == 0)
                break;

            offset += read;
        }

        if (offset == length)
            return buffer;

        var actual = new byte[offset];
        Buffer.BlockCopy(buffer, 0, actual, 0, offset);
        return actual;
    }

    /// <summary>
    /// Читает RVT потоково ограниченными окнами и дозаполняет только недостающие
    /// части распознанной версии.
    /// </summary>
    /// <param name="path">Путь к RVT-файлу.</param>
    /// <param name="partial">Частичный результат, полученный на быстром префиксе.</param>
    /// <param name="includeBuild">Нужно ли искать build.</param>
    /// <returns>Обновлённая пара "год / build" после потокового прохода.</returns>
    /// <remarks>
    /// Метод не заменяет уже найденные значения без необходимости.
    /// Он нужен как безопасный по памяти fallback перед чтением всего файла целиком.
    /// </remarks>
    private static (int? Year, string? Build) ReadByChunksAndMergeMissingVersionData(
        string path,
        (int? Year, string? Build) partial,
        bool includeBuild)
    {
        using var fs = File.OpenRead(path);

        // Общий буфер содержит overlap от предыдущего окна и новый прочитанный чанк.
        var buffer = new byte[_streamChunkBytes + _streamOverlapBytes];
        var carryLength = 0;
        var current = partial;

        while (true)
        {
            // Чтение начинается после overlap-части: её байты уже лежат в начале буфера.
            var read = fs.Read(buffer, carryLength, _streamChunkBytes);
            if (read == 0)
                return current;

            var windowLength = carryLength + read;

            // Выделяем окно точной длины, чтобы поиск не видел остаточные байты
            // вне фактически прочитанного диапазона.
            var window = new byte[windowLength];
            Buffer.BlockCopy(buffer, 0, window, 0, windowLength);

            // Дозаполняем только недостающие части версии, не перезаписывая уже найденные значения.
            current = MergeMissingVersionData(current, window, includeBuild);
            if (HasEnoughData(current.Year, current.Build, includeBuild))
                return current;

            // Сохраняем хвост окна: маркер может пересечь границу соседних чанков.
            carryLength = Math.Min(_streamOverlapBytes, windowLength);
            Buffer.BlockCopy(
                buffer,
                windowLength - carryLength,
                buffer,
                0,
                carryLength);
        }
    }

    /// <summary>
    /// Извлекает major-год версии из участка данных по маркеру <c>Format:</c>.
    /// </summary>
    /// <param name="data">Бинарные данные RVT-файла или его префикса.</param>
    /// <returns>Год версии Revit либо <see langword="null"/>.</returns>
    /// <remarks>
    /// Основной сценарий — найти текстовую сигнатуру <c>Format:</c>,
    /// вырезать короткий хвост после неё, декодировать его в нужной UTF-16-кодировке
    /// и извлечь год регулярным выражением.
    /// </remarks>
    private static int? ExtractYear(byte[] data)
    {
        var found = FindMarker(
            data,
            (_fmtLe, _encLe, _fmtLe.Length),
            (_fmtBe, _encBe, _fmtBe.Length));

        if (found.Index < 0)
            return null;

        var start = found.Index + found.MarkerLength;
        var tail = SliceSafe(data, start, _yearTailBytes);
        var text = found.Encoding.GetString(tail);

        var match = _reYear.Match(text);
        if (!match.Success)
            return null;

        if (!int.TryParse(match.Groups[1].Value, out var year))
            return null;

        return year is >= _minYear and <= _maxYear
            ? year
            : null;
    }

    /// <summary>
    /// Извлекает build-номер из участка данных по маркеру <c>Build:</c>.
    /// </summary>
    /// <param name="data">Бинарные данные RVT-файла или его префикса.</param>
    /// <returns>Строка build-номера либо <see langword="null"/>.</returns>
    /// <remarks>
    /// После маркера берётся небольшой хвост, который декодируется в текст.
    /// Из него убираются типичные разделители и бинарные нули, после чего
    /// регулярное выражение вытаскивает первый подходящий build-фрагмент.
    /// </remarks>
    private static string? ExtractBuild(byte[] data)
    {
        var found = FindMarker(
            data,
            (_bldLe, _encLe, _bldLe.Length),
            (_bldBe, _encBe, _bldBe.Length));

        if (found.Index < 0)
            return null;

        var start = found.Index + found.MarkerLength;
        var tail = SliceSafe(data, start, _buildTailBytes);
        var text = found.Encoding.GetString(tail);

        // После build в декодированном хвосте часто идут служебные символы,
        // бинарные нули или закрывающая скобка. Их убираем до regex-поиска.
        text = text.Replace("\0", " ");
        text = text.Split(')')[0].Split('\r')[0].Split('\n')[0].Trim();

        var match = _reBuild.Match(text);
        return match.Success ? match.Value : null;
    }

    /// <summary>
    /// Пытается извлечь год версии из фрагмента после маркера <c>Autodesk Revit</c>.
    /// </summary>
    /// <param name="data">Бинарные данные RVT-файла или его префикса.</param>
    /// <returns>Год версии Revit либо <see langword="null"/>.</returns>
    /// <remarks>
    /// Это fallback-сценарий на случай, когда явного маркера <c>Format:</c> нет,
    /// но рядом с подписью <c>Autodesk Revit</c> присутствует год.
    /// Поиск по <c>Autodesk Revit</c> выполняется после основной попытки.
    /// </remarks>
    private static int? ExtractYearFromAutodesk(byte[] data)
    {
        foreach (var (marker, encoding) in new[] { (_autLe, _encLe), (_autBe, _encBe) })
        {
            var index = IndexOfBytes(data, marker);
            if (index < 0)
                continue;

            var start = index + marker.Length;
            var fragment = SliceSafe(data, start, _autodeskSuffixBytes);
            var text = encoding.GetString(fragment);

            var match = _reYear.Match(text);
            if (!match.Success)
                continue;

            if (!int.TryParse(match.Groups[1].Value, out var year))
                continue;

            if (year is >= _minYear and <= _maxYear)
                return year;
        }

        return null;
    }

    /// <summary>
    /// Ищет первый найденный вариант маркера в массиве байт.
    /// </summary>
    /// <param name="data">Массив, в котором выполняется поиск.</param>
    /// <param name="variants">
    /// Набор вариантов маркера вместе с кодировкой,
    /// в которой нужно декодировать следующий за маркером фрагмент.
    /// </param>
    /// <returns>
    /// Индекс найденного маркера, его кодировку и длину,
    /// либо индекс <c>-1</c>, если ни один вариант не найден.
    /// </returns>
    /// <remarks>
    /// Метод связывает маркер и кодировку в одну точку.
    /// Поиск байтов и декодирование хвоста выполняются в согласованной кодировке.
    /// </remarks>
    private static (int Index, Encoding Encoding, int MarkerLength) FindMarker(
        byte[] data,
        params (byte[] Marker, Encoding Encoding, int MarkerLength)[] variants)
    {
        foreach (var variant in variants)
        {
            var index = IndexOfBytes(data, variant.Marker);
            if (index >= 0)
                return (index, variant.Encoding, variant.MarkerLength);
        }

        return (-1, _encLe, 0);
    }

    /// <summary>
    /// Безопасно вырезает фрагмент из массива байт.
    /// </summary>
    /// <param name="data">Исходный массив.</param>
    /// <param name="start">Начальная позиция.</param>
    /// <param name="length">Желаемая длина фрагмента.</param>
    /// <returns>Новый массив с доступным диапазоном байт.</returns>
    /// <remarks>
    /// Метод не бросает исключение при выходе за границы.
    /// Если стартовая позиция недопустима, возвращается пустой массив.
    /// Если длина выходит за пределы массива, фрагмент просто укорачивается.
    /// </remarks>
    private static byte[] SliceSafe(byte[] data, int start, int length)
    {
        if (start < 0 || start >= data.Length)
            return Array.Empty<byte>();

        var actualLength = Math.Min(length, data.Length - start);
        var result = new byte[actualLength];
        Buffer.BlockCopy(data, start, result, 0, actualLength);
        return result;
    }

    /// <summary>
    /// Ищет подмассив байт внутри другого массива.
    /// </summary>
    /// <param name="haystack">Массив, в котором выполняется поиск.</param>
    /// <param name="needle">Искомый маркер.</param>
    /// <returns>Индекс первого вхождения либо <c>-1</c>.</returns>
    /// <remarks>
    /// Используется прямой поиск O(n * m).
    /// Поиск выполняется по коротким маркерам и ограниченным фрагментам данных:
    /// по начальному префиксу файла, по потоковым окнам fallback-чтения
    /// или по коротким срезам после найденного маркера.
    /// </remarks>
    private static int IndexOfBytes(byte[] haystack, byte[] needle)
    {
        if (needle.Length == 0)
            return 0;

        if (needle.Length > haystack.Length)
            return -1;

        for (var i = 0; i <= haystack.Length - needle.Length; i++)
        {
            var matches = true;

            for (var j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j])
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
                return i;
        }

        return -1;
    }
}

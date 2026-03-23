using Autodesk.Revit.DB;

namespace ExportIfc.RevitAddin.Revit;

/// <summary>
/// Поиск 3D-видов для экспорта IFC.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Найти 3D-вид по точному имени.
/// 2. Исключить шаблонные виды.
/// 3. Сначала попытаться сузить выборку фильтром Revit API,
///    затем выполнить резервный полный перебор.
/// 4. При нескольких совпадениях предпочесть наиболее пригодный
///    для экспорта вид.
///
/// Контракты:
/// 1. Рассматриваются только элементы класса <see cref="View3D"/>.
/// 2. Имя итогово проверяется по точному совпадению с учётом регистра.
/// 3. Шаблонные виды исключаются.
/// 4. При отсутствии подходящего вида возвращается <see langword="null"/>.
/// </remarks>
internal static class RevitViews
{
    /// <summary>
    /// Ищет 3D-вид по точному имени.
    /// </summary>
    /// <param name="doc">Документ Revit.</param>
    /// <param name="name">Имя вида для точного сравнения.</param>
    /// <returns>Найденный 3D-вид или <see langword="null"/>.</returns>
    public static View3D? FindView3DByName(Document doc, string name)
    {
        if (doc is null)
            throw new ArgumentNullException(nameof(doc));

        if (string.IsNullOrWhiteSpace(name))
            return null;

        var byFilter = ChooseBestCandidate(GetByParameterFilter(doc, name), name);
        if (byFilter is not null)
            return byFilter;

        return ChooseBestCandidate(GetByFullScan(doc), name);
    }

    /// <summary>
    /// Получает кандидатов через фильтр Revit API по имени вида.
    /// </summary>
    /// <param name="doc">Документ Revit.</param>
    /// <param name="name">Имя вида для фильтрации.</param>
    /// <returns>Последовательность найденных 3D-видов.</returns>
    /// <remarks>
    /// Метод использует параметр <see cref="BuiltInParameter.VIEW_NAME"/>
    /// как предварительное сужение выборки.
    /// </remarks>
    private static IEnumerable<View3D> GetByParameterFilter(Document doc, string name)
    {
        var provider = new ParameterValueProvider(
            new ElementId(BuiltInParameter.VIEW_NAME));

        var rule = new FilterStringRule(
            provider,
            new FilterStringEquals(),
            name);

        var nameFilter = new ElementParameterFilter(rule);

        var collector = new FilteredElementCollector(doc)
            .OfClass(typeof(View3D))
            .WherePasses(nameFilter)
            .WhereElementIsNotElementType();

        foreach (var element in collector)
        {
            if (element is View3D view)
                yield return view;
        }
    }

    /// <summary>
    /// Получает всех кандидатов класса <see cref="View3D"/> полным перебором документа.
    /// </summary>
    /// <param name="doc">Документ Revit.</param>
    /// <returns>Последовательность найденных 3D-видов.</returns>
    private static IEnumerable<View3D> GetByFullScan(Document doc)
    {
        var collector = new FilteredElementCollector(doc)
            .OfClass(typeof(View3D))
            .WhereElementIsNotElementType();

        foreach (var element in collector)
        {
            if (element is View3D view)
                yield return view;
        }
    }

    /// <summary>
    /// Выбирает лучший 3D-вид среди кандидатов с точным совпадением имени.
    /// </summary>
    /// <param name="candidates">Кандидаты на экспортный вид.</param>
    /// <param name="name">Ожидаемое точное имя вида.</param>
    /// <returns>Наиболее подходящий 3D-вид или <see langword="null"/>.</returns>
    /// <remarks>
    /// Приоритет выбора:
    /// 1. неперспективный и печатаемый вид;
    /// 2. любой неперспективный вид;
    /// 3. любой печатаемый вид;
    /// 4. первый точный кандидат.
    /// </remarks>
    private static View3D? ChooseBestCandidate(IEnumerable<View3D> candidates, string name)
    {
        var exact = candidates
            .Where(view =>
                !view.IsTemplate &&
                string.Equals(view.Name, name, StringComparison.Ordinal))
            .ToList();

        return exact.FirstOrDefault(view => !view.IsPerspective && view.CanBePrinted)
            ?? exact.FirstOrDefault(view => !view.IsPerspective)
            ?? exact.FirstOrDefault(view => view.CanBePrinted)
            ?? exact.FirstOrDefault();
    }
}
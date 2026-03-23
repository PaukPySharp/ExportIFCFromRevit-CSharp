using ExportIfc.Config;

namespace ExportIfc.Revit;

/// <summary>
/// Контракт поиска установленного исполняемого файла Revit.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Отделяет код запуска Revit от конкретного способа поиска
///    <see cref="RevitConstants.ExecutableFileName"/>.
/// 2. Позволяет оркестратору запрашивать exe по целевой major-версии Revit.
/// 3. Централизует правила поиска локальной установки Revit.
///
/// Контракты:
/// 1. Поиск выполняется для конкретной major-версии Revit.
/// 2. Возвращаемый путь должен указывать на существующий локальный
///    <see cref="RevitConstants.ExecutableFileName"/>.
/// 3. Если подходящая установка не найдена, метод возвращает <see langword="null"/>.
/// </remarks>
public interface IRevitExeLocator
{
    /// <summary>
    /// Пробует найти путь к <see cref="RevitConstants.ExecutableFileName"/>
    /// для заданной версии Revit.
    /// </summary>
    /// <param name="revitMajor">Целевая major-версия Revit.</param>
    /// <returns>Полный путь к exe или <see langword="null"/>.</returns>
    string? TryFind(int revitMajor);
}
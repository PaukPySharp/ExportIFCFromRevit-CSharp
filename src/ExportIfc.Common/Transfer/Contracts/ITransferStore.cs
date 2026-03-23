using ExportIfc.Config;
using ExportIfc.Models;

namespace ExportIfc.Transfer;

/// <summary>
/// Контракт чтения и записи служебных файлов передачи заданий между оркестратором
/// и add-in.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Отделяет orchestration-слой от деталей сериализации transport-пакета.
/// 2. Централизует контракт файлов <see cref="ProjectFileNames.TmpJson"/>
///    и <see cref="ProjectFileNames.TaskFileDisplayName"/> пакетов.
/// 3. Гарантирует единые правила сверки состава и порядка моделей.
///
/// Контракты:
/// 1. <see cref="ProjectFileNames.TmpJson"/> хранит transport-пакет batch-запуска.
/// 2. <see cref="ProjectFileNames.TaskFileDisplayName"/> хранит только порядок и состав моделей текущего пакета.
/// 3. Пустой путь выгрузки может быть валидной частью transport-контракта,
///    если он означает отключённое направление экспорта.
/// </remarks>
public interface ITransferStore
{
    /// <summary>
    /// Создаёт пакет передачи для одной версии Revit.
    /// </summary>
    /// <param name="revitMajor">Целевая major-версия Revit.</param>
    /// <param name="runId">Идентификатор текущего запуска оркестратора.</param>
    /// <param name="models">Модели текущего пакета.</param>
    /// <returns>Готовый контейнер передачи.</returns>
    TransferEnvelope BuildEnvelope(
        int revitMajor,
        string runId,
        IEnumerable<RevitModel> models);

    /// <summary>
    /// Записывает <see cref="ProjectFileNames.TmpJson"/>.
    /// </summary>
    /// <param name="tmpJsonPath">Полный путь к <see cref="ProjectFileNames.TmpJson"/>.</param>
    /// <param name="envelope">Пакет передачи.</param>
    void WriteEnvelope(string tmpJsonPath, TransferEnvelope envelope);

    /// <summary>
    /// Пробует прочитать <see cref="ProjectFileNames.TmpJson"/>.
    /// </summary>
    /// <param name="tmpJsonPath">Полный путь к <see cref="ProjectFileNames.TmpJson"/>.</param>
    /// <param name="envelope">Результат чтения.</param>
    /// <returns>
    /// <see langword="true"/>, если файл успешно прочитан и удовлетворяет
    /// минимальному структурному контракту пакета;
    /// иначе <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// Метод проверяет только структурную пригодность transport-пакета:
    /// наличие контейнера, списка элементов и базовых идентификаторов модели.
    /// Пустые пути выгрузки допустимы в transport-контракте
    /// и соответствуют отключённому маршруту экспорта.
    /// </remarks>
    bool TryReadEnvelope(
        string tmpJsonPath,
        out TransferEnvelope? envelope);

    /// <summary>
    /// Записывает <see cref="ProjectFileNames.TaskFileDisplayName"/> текущего пакета.
    /// </summary>
    /// <param name="taskFilePath">Полный путь к <see cref="ProjectFileNames.TaskFileDisplayName"/> текущего пакета.</param>
    /// <param name="modelPaths">Полные пути к моделям в порядке обработки.</param>
    void WriteTaskModels(
        string taskFilePath,
        IEnumerable<string> modelPaths);

    /// <summary>
    /// Читает <see cref="ProjectFileNames.TaskFileDisplayName"/> текущего пакета.
    /// </summary>
    /// <param name="taskFilePath">Полный путь к <see cref="ProjectFileNames.TaskFileDisplayName"/> текущего пакета.</param>
    /// <returns>Массив путей к моделям в порядке обработки.</returns>
    string[] ReadTaskModels(string taskFilePath);

    /// <summary>
    /// Сравнивает <see cref="ProjectFileNames.TaskFileDisplayName"/> с составом и порядком элементов в <see cref="TransferEnvelope"/>.
    /// </summary>
    /// <param name="envelope">Пакет передачи текущего batch-запуска.</param>
    /// <param name="taskModels">Модели из <see cref="ProjectFileNames.TaskFileDisplayName"/>.</param>
    /// <returns>
    /// <see langword="null"/>, если последовательности совпадают;
    /// иначе строку с описанием первого найденного расхождения.
    /// </returns>
    string? DescribeTaskModelMismatch(
        TransferEnvelope envelope,
        IReadOnlyList<string> taskModels);
}

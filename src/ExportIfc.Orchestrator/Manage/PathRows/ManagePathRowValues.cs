using ExportIfc.Excel;

namespace ExportIfc.Manage;

/// <summary>
/// Сырые значения одной строки листа Path.
/// </summary>
/// <remarks>
/// Назначение:
/// Хранит исходные строковые значения ячеек, считанные из Excel,
/// до этапа нормализации, валидации путей и разрешения конфигурационных файлов.
///
/// Контракты:
/// 1. Экземпляр отражает одну строку листа Path без попытки интерпретации значений.
/// 2. Пустая строка определяется только по содержимому всех полей записи.
/// 3. Проверка корректности путей и обязательности значений выполняется
///    отдельным парсером, а не этим типом.
/// </remarks>
internal readonly record struct ManagePathRowValues(
    string RvtDirRaw,
    string OutputDirMappingRaw,
    string MappingDirectoryRaw,
    string IfcClassMappingRaw,
    string OutputDirNoMapRaw,
    string NoMapJsonRaw)
{
    /// <summary>
    /// Проверяет, что строка полностью пуста.
    /// </summary>
    /// <returns>
    /// <see langword="true"/>, если все поля строки пусты;
    /// иначе <see langword="false"/>.
    /// </returns>
    public bool IsBlank()
        => ExcelCells.IsBlankRow(
            RvtDirRaw,
            OutputDirMappingRaw,
            MappingDirectoryRaw,
            IfcClassMappingRaw,
            OutputDirNoMapRaw,
            NoMapJsonRaw);
}
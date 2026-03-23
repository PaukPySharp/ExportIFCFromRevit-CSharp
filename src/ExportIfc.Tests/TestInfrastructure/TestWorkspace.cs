namespace ExportIfc.Tests.TestInfrastructure;

/// <summary>
/// Временное рабочее пространство теста с автоматической очисткой.
/// </summary>
/// <remarks>
/// Helper изолирует файловые сценарии друг от друга и скрывает техническую механику
/// создания временных каталогов, чтобы сами тесты оставались сфокусированными на доменном контракте.
/// </remarks>
internal sealed class TestWorkspace : IDisposable
{
    private TestWorkspace(string root)
    {
        Root = root;
    }

    /// <summary>
    /// Корневая директория временного тестового окружения.
    /// </summary>
    public string Root { get; }

    /// <summary>
    /// Создаёт новое временное рабочее пространство теста.
    /// </summary>
    /// <returns>Подготовленное рабочее пространство с существующим корневым каталогом.</returns>
    public static TestWorkspace Create()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return new TestWorkspace(root);
    }

    /// <summary>
    /// Строит путь внутри временного рабочего пространства.
    /// </summary>
    /// <param name="parts">Сегменты относительного пути.</param>
    /// <returns>Полный путь внутри <see cref="Root"/>.</returns>
    public string GetPath(params string[] parts)
    {
        if (parts is null || parts.Length == 0)
            return Root;

        return parts.Aggregate(Root, System.IO.Path.Combine);
    }

    /// <summary>
    /// Создаёт каталог внутри временного рабочего пространства.
    /// </summary>
    /// <param name="parts">Сегменты относительного пути.</param>
    /// <returns>Полный путь к созданному каталогу.</returns>
    public string CreateDirectory(params string[] parts)
    {
        var path = GetPath(parts);
        Directory.CreateDirectory(path);
        return path;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
        catch
        {
            // Временное тестовое окружение очищается best-effort.
        }
    }
}

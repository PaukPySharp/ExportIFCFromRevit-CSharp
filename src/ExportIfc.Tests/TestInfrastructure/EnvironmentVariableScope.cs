namespace ExportIfc.Tests.TestInfrastructure;

/// <summary>
/// Временное переопределение набора переменных окружения на время теста.
/// </summary>
/// <remarks>
/// Helper сохраняет исходные значения указанных переменных,
/// применяет тестовые значения и затем восстанавливает предыдущее состояние.
/// </remarks>
internal sealed class EnvironmentVariableScope : IDisposable
{
    private readonly Dictionary<string, string?> _previousValues =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Создаёт область временных значений переменных окружения.
    /// </summary>
    /// <param name="assignments">Набор переопределений вида «имя -> значение».</param>
    public EnvironmentVariableScope(params (string Name, string? Value)[] assignments)
    {
        ArgumentNullException.ThrowIfNull(assignments);

        foreach (var (name, value) in assignments)
        {
            if (_previousValues.ContainsKey(name))
                throw new ArgumentException($"Повторное имя переменной окружения: {name}", nameof(assignments));

            _previousValues[name] = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var pair in _previousValues)
            Environment.SetEnvironmentVariable(pair.Key, pair.Value);
    }
}

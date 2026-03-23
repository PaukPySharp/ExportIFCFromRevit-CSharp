using System.Globalization;
using Microsoft.Extensions.Configuration;

using ExportIfc.Config;

namespace ExportIfc.Settings.Loading;

/// <summary>
/// Вспомогательное чтение значений из ini-конфигурации.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Централизует повторяющееся чтение строковых значений из <see cref="IConfiguration"/>.
/// 2. Приводит строки ini к рабочим типам настроек.
/// 3. Формирует единообразные ошибки для обязательных и некорректных параметров.
///
/// Контракты:
/// 1. Методы Required* выбрасывают исключение, если значение отсутствует или пустое.
/// 2. Методы Default* возвращают значение по умолчанию, если параметр отсутствует или пустой.
/// 3. Некорректное значение типа не подменяется молча — чтение завершается ошибкой.
/// 4. Строковые значения нормализуются один раз: внешние пробелы удаляются сразу после чтения.
/// </remarks>
internal static class AppSettingsValueReader
{
    /// <summary>
    /// Читает обязательное строковое значение.
    /// </summary>
    /// <param name="configuration">Источник ini-значений.</param>
    /// <param name="key">Ключ параметра.</param>
    /// <returns>Непустое строковое значение.</returns>
    /// <exception cref="KeyNotFoundException">
    /// Выбрасывается, если параметр отсутствует или содержит только пробелы.
    /// </exception>
    public static string ReadRequired(IConfiguration configuration, string key)
    {
        return ReadNormalized(configuration, key)
            ?? throw new KeyNotFoundException(
                $"В {ProjectFileNames.SettingsIni} отсутствует обязательный параметр: {key}");
    }

    /// <summary>
    /// Читает строковое значение с подстановкой значения по умолчанию.
    /// </summary>
    /// <param name="configuration">Источник ini-значений.</param>
    /// <param name="key">Ключ параметра.</param>
    /// <param name="defaultValue">Значение по умолчанию.</param>
    /// <returns>Значение параметра либо <paramref name="defaultValue"/>.</returns>
    public static string ReadDefault(
        IConfiguration configuration,
        string key,
        string defaultValue)
    {
        return ReadNormalized(configuration, key) ?? defaultValue;
    }

    /// <summary>
    /// Читает обязательное логическое значение.
    /// </summary>
    /// <param name="configuration">Источник ini-значений.</param>
    /// <param name="key">Ключ параметра.</param>
    /// <returns>Разобранное логическое значение.</returns>
    /// <exception cref="FormatException">
    /// Выбрасывается, если параметр содержит строку,
    /// которую нельзя однозначно интерпретировать как boolean.
    /// </exception>
    public static bool ReadRequiredBool(IConfiguration configuration, string key)
    {
        var value = ReadRequired(configuration, key);
        return ParseBool(value, key);
    }

    /// <summary>
    /// Читает целое число с подстановкой значения по умолчанию.
    /// </summary>
    /// <param name="configuration">Источник ini-значений.</param>
    /// <param name="key">Ключ параметра.</param>
    /// <param name="defaultValue">Значение по умолчанию.</param>
    /// <returns>Разобранное целое число либо <paramref name="defaultValue"/>.</returns>
    /// <exception cref="FormatException">
    /// Выбрасывается, если параметр задан, но не является корректным целым числом.
    /// </exception>
    public static int ReadDefaultInt(
        IConfiguration configuration,
        string key,
        int defaultValue)
    {
        var value = ReadNormalized(configuration, key);
        return value is null
            ? defaultValue
            : ParseInt(value, key, "целое число");
    }

    /// <summary>
    /// Читает обязательный список целых чисел.
    /// </summary>
    /// <param name="configuration">Источник ini-значений.</param>
    /// <param name="key">Ключ параметра.</param>
    /// <returns>Отсортированный список без дублей.</returns>
    /// <exception cref="FormatException">
    /// Выбрасывается, если хотя бы одно значение списка не является целым числом
    /// или если список оказался пустым после разбора.
    /// </exception>
    public static IReadOnlyList<int> ReadRequiredIntList(
        IConfiguration configuration,
        string key)
    {
        var value = ReadRequired(configuration, key);

        var items = value
            .Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries)
            .Select(item => ParseInt(item.Trim(), key, "значение версии Revit"))
            .Distinct()
            .OrderBy(item => item)
            .ToArray();

        if (items.Length == 0)
        {
            throw new FormatException(
                $"Параметр '{key}' должен содержать хотя бы одну версию Revit.");
        }

        return items;
    }

    /// <summary>
    /// Читает строковое значение и убирает внешние пробелы.
    /// </summary>
    /// <param name="configuration">Источник ini-значений.</param>
    /// <param name="key">Ключ параметра.</param>
    /// <returns>
    /// Нормализованное строковое значение либо <see langword="null"/>,
    /// если параметр отсутствует или после trim оказался пустым.
    /// </returns>
    private static string? ReadNormalized(IConfiguration configuration, string key)
    {
        var value = configuration[key]?.Trim();
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value;
    }

    /// <summary>
    /// Преобразует строку ini в целое число.
    /// </summary>
    /// <param name="value">Исходная строка параметра.</param>
    /// <param name="key">Ключ параметра для текста ошибки.</param>
    /// <param name="valueDescription">Короткое описание ожидаемого значения.</param>
    /// <returns>Разобранное целое значение.</returns>
    /// <exception cref="FormatException">
    /// Выбрасывается, если строка не является корректным целым числом.
    /// </exception>
    private static int ParseInt(string value, string key, string valueDescription)
    {
        if (int.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var result))
        {
            return result;
        }

        throw new FormatException(
            $"Параметр '{key}' содержит некорректное {valueDescription}: '{value}'.");
    }

    /// <summary>
    /// Преобразует строку ini в логическое значение.
    /// </summary>
    /// <param name="value">Исходная строка параметра.</param>
    /// <param name="key">Ключ параметра для текста ошибки.</param>
    /// <returns>Разобранное логическое значение.</returns>
    /// <exception cref="FormatException">
    /// Выбрасывается, если строка не входит в поддерживаемый набор true/false-значений.
    /// </exception>
    private static bool ParseBool(string value, string key)
    {
        var normalized = value.Trim().ToLowerInvariant();

        if (normalized is "1" or "true" or "yes" or "да")
            return true;

        if (normalized is "0" or "false" or "no" or "нет")
            return false;

        throw new FormatException(
            $"Параметр '{key}' содержит некорректное логическое значение: '{value}'.");
    }
}
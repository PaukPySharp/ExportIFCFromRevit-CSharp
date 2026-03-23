using ExportIfc.Config;
using ExportIfc.Logging;
using ExportIfc.Composition;
using ExportIfc.Settings.Loading;

namespace ExportIfc;

/// <summary>
/// Точка входа внешнего оркестратора.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Разрешает путь к ini-файлу стартовой конфигурации.
/// 2. Загружает итоговые настройки приложения.
/// 3. Создаёт и запускает оркестратор экспорта IFC.
/// 4. Завершает процесс единообразным кодом и итоговым сообщением.
///
/// Контракты:
/// 1. Точка входа не содержит бизнес-логики экспорта.
/// 2. Ошибки конфигурации запуска обрабатываются как фатальные ошибки старта.
/// 3. Ошибки выполнения основного процесса возвращаются через код завершения оркестратора.
/// </remarks>
internal static class Program
{
    private static readonly ConsoleLogger _mainLog = Log.For(LogComponents.Main);

    /// <summary>
    /// Запускает подготовку и выполнение экспорта IFC.
    /// </summary>
    /// <param name="args">Аргументы командной строки.</param>
    /// <returns>Код завершения процесса.</returns>
    public static int Main(string[] args)
    {
        // UTF-8 нужен для корректного вывода русскоязычных сообщений
        // и unicode-символов Spectre.Console в консоли оркестратора.
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        Log.Rule(RevitConstants.IfcExportTransactionName);
        _mainLog.Info("Запуск выгрузки IFC.");

        try
        {
            var iniPath = SettingsIniLocator.ResolveStartupPath(args);
            Environment.SetEnvironmentVariable(EnvironmentVariableNames.SettingsIni, iniPath);

            var settings = AppSettingsLoader.Load(iniPath);
            var orchestrator = ExportCompositionRoot.CreateOrchestrator(settings);
            var exitCode = orchestrator.Run();

            if (exitCode == 0)
            {
                if (settings.RunRevit)
                {
                    _mainLog.Info("Выгрузка IFC завершена.");
                    Log.Result(true, "Готово", "IFC выгружены. Критических ошибок не обнаружено.");
                    return 0;
                }

                _mainLog.Info("Dry-run завершён.");
                Log.Result(
                    true,
                    "Dry-run завершён",
                    "Подготовлены Task-файлы, debug JSON-файлы пакетов и история. Revit не запускался.");
                return 0;
            }

            _mainLog.Error("Выгрузка IFC завершена с ошибками.");
            Log.Result(false, "Есть ошибки", $"Проверьте консоль и txt-логи в папке {ProjectRelativePaths.LogsRelativeDisplayPath}.");
            return exitCode;
        }
        catch (Exception ex) when (
            ex is IOException ||
            ex is ArgumentException ||
            ex is FormatException ||
            ex is KeyNotFoundException)
        {
            return Fail($"Фатальная ошибка конфигурации запуска: {ex.Message}");
        }
        catch (Exception ex)
        {
            return Fail($"Непредвиденная ошибка запуска: {ex.Message}");
        }
    }

    /// <summary>
    /// Завершает процесс с единообразным ошибочным результатом запуска.
    /// </summary>
    /// <param name="message">Итоговое сообщение о причине срыва запуска.</param>
    /// <returns>Код завершения с ошибкой.</returns>
    private static int Fail(string message)
    {
        _mainLog.Error(message);
        Log.Result(false, "Запуск сорвался", message);
        return 1;
    }
}

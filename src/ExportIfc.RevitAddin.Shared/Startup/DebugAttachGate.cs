using ExportIfc.Config;

using System.Diagnostics;

namespace ExportIfc.RevitAddin.Startup;

/// <summary>
/// DEBUG-инструмент раннего ожидания подключения debugger к процессу Revit.
/// </summary>
/// <remarks>
/// Назначение:
/// 1. Позволяет остановить batch-autorun до начала основной логики add-in.
/// 2. Даёт время вручную подключиться к процессу Revit из Visual Studio.
/// 3. Не вмешивается в Release-сценарий и не меняет рабочий контракт запуска.
///
/// Контракты:
/// 1. В Release-сборке метод не выполняет никаких действий.
/// 2. В Debug-сборке ожидание включается только по флагу
///    <see cref="EnvironmentVariableNames.DebugWaitAttach"/>.
/// 3. После подключения debugger возможно принудительное прерывание через <see cref="Debugger.Break"/>.
/// </remarks>
internal static class DebugAttachGate
{
    /// <summary>
    /// При необходимости ждёт подключения debugger к текущему процессу Revit.
    /// </summary>
    /// <remarks>
    /// Используется в ранней точке входа add-in,
    /// чтобы успеть подключить debugger до запуска batch-логики.
    /// </remarks>
    public static void WaitIfRequested()
    {
#if DEBUG
        var flag = Environment.GetEnvironmentVariable(EnvironmentVariableNames.DebugWaitAttach);

        // Ожидание включается только специальным флагом окружения.
        // Это позволяет запускать обычные DEBUG-сборки без паузы и включать
        // раннее ожидание только для нужного локального прогона.
        if (!string.Equals(
                flag,
                EnvironmentVariableValues.DebugWaitAttachEnabled,
                StringComparison.Ordinal))
        {
            return;
        }

        // Ожидание выполняется циклом с паузой 250 мс.
        // Пауза удерживает CPU-нагрузку ниже, чем активное ожидание.
        while (!Debugger.IsAttached)
            Thread.Sleep(250);

        // При локальной отладке здесь можно включить принудительную остановку,
        // чтобы начать разбор из предсказуемой ранней точки входа add-in.
        //Debugger.Break();
#endif
    }
}

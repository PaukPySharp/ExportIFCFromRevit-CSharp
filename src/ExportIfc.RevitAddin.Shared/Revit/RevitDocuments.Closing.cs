using Autodesk.Revit.DB;

namespace ExportIfc.RevitAddin.Revit;

internal static partial class RevitDocuments
{
    /// <summary>
    /// Пытается штатно закрыть документ без сохранения.
    /// </summary>
    /// <param name="document">Документ Revit, подлежащий закрытию.</param>
    /// <returns>
    /// <see langword="null"/>, если документ закрыт без ошибки
    /// или документ не был передан;
    /// иначе текст ошибки закрытия.
    /// </returns>
    /// <remarks>
    /// После попытки закрытия метод дополнительно инициирует очистку CLR
    /// и освобождение Revit API-объектов, не прерывая вызывающий код,
    /// если эти операции завершились ошибкой.
    /// </remarks>
    public static string? CloseSafely(Document? document)
    {
        if (document is null)
            return null;

        string? error = null;

        try
        {
            document.Close(false);
        }
        catch (Exception ex)
        {
            error = ex.ToString();
        }
        finally
        {
            try
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
            catch
            {
                // Ошибки форсированной очистки CLR не влияют на результат batch-обработки.
            }

            try
            {
                document.Application?.PurgeReleasedAPIObjects();
            }
            catch
            {
                // Ошибки очистки Revit API-объектов не считаются фатальными для вызывающего кода.
            }
        }

        return error;
    }
}

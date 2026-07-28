namespace ShieldReport.Application.Common.Interfaces.Services;

// Implemented in Infrastructure by shelling out to LibreOffice (`soffice --headless`) — there is
// no docx->PDF conversion library in .NET without a commercial SDK (Aspose/Syncfusion), and
// QuestPDF only renders new PDFs from scratch, it can't convert an existing docx.
public interface IReportPdfConverter
{
    Task<byte[]> ConvertAsync(byte[] docxBytes, CancellationToken cancellationToken = default);
}

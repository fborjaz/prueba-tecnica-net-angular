using System.Globalization;
using System.Net;
using System.Net.Mail;
using System.Text;
using CashManagement.Application.Common;
using CashManagement.Application.Interfaces;
using CashManagement.Domain.Exceptions;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CashManagement.Infrastructure.Services;

public class SmtpOptions
{
    public const string Section = "Smtp";

    public bool Enabled { get; set; }
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1025;
    public bool EnableSsl { get; set; }
    public string? User { get; set; }
    public string? Password { get; set; }
    public string From { get; set; } = "no-reply@cashmanagement.local";
}

/// <summary>
/// Envío de correos por SMTP. En Docker se usa Mailpit (http://localhost:8025) para ver los correos.
/// Si SMTP está deshabilitado, el contenido se registra en el log para poder probar el flujo.
/// </summary>
public class SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        var smtp = options.Value;

        if (!smtp.Enabled)
        {
            logger.LogWarning("SMTP deshabilitado. Correo para {To} - {Subject}: {Body}", to, subject, htmlBody);
            return;
        }

        try
        {
            using var client = new SmtpClient(smtp.Host, smtp.Port) { EnableSsl = smtp.EnableSsl };
            if (!string.IsNullOrWhiteSpace(smtp.User))
            {
                client.Credentials = new NetworkCredential(smtp.User, smtp.Password);
            }

            using var message = new MailMessage(smtp.From, to, subject, htmlBody) { IsBodyHtml = true };
            await client.SendMailAsync(message, ct);
            logger.LogInformation("Correo enviado a {To}: {Subject}", to, subject);
        }
        catch (Exception ex) when (ex is SmtpException or InvalidOperationException)
        {
            logger.LogError(ex, "No se pudo enviar el correo a {To}", to);
            throw new BusinessException("No se pudo enviar el correo. Intente nuevamente más tarde.");
        }
    }
}

/// <summary>Lee archivos .xlsx (ClosedXML) y .csv (CsvHelper, separador coma o punto y coma).</summary>
public class SpreadsheetReader : ISpreadsheetReader
{
    private const int MaxRows = 1000;

    public IReadOnlyList<IReadOnlyDictionary<string, string>> Read(Stream stream, string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var rows = extension switch
        {
            ".xlsx" => ReadExcel(stream),
            ".csv" => ReadCsv(stream),
            _ => throw new BusinessException("Formato no soportado. Use un archivo .xlsx o .csv.")
        };

        if (rows.Count == 0) throw new BusinessException("El archivo no contiene registros.");
        if (rows.Count > MaxRows) throw new BusinessException($"El archivo supera el máximo de {MaxRows} registros.");
        return rows;
    }

    private static List<IReadOnlyDictionary<string, string>> ReadExcel(Stream stream)
    {
        try
        {
            using var workbook = new XLWorkbook(stream);
            var sheet = workbook.Worksheets.First();
            var used = sheet.RangeUsed();
            if (used is null) return [];

            var headerRow = used.FirstRow();
            var headers = headerRow.Cells().Select(c => SpreadsheetRowExtensions.NormalizeHeader(c.GetString())).ToList();

            var result = new List<IReadOnlyDictionary<string, string>>();
            foreach (var row in used.RowsUsed().Skip(1))
            {
                var dict = new Dictionary<string, string>();
                for (var i = 0; i < headers.Count; i++)
                {
                    dict[headers[i]] = row.Cell(i + 1).GetFormattedString().Trim();
                }

                if (dict.Values.Any(v => !string.IsNullOrWhiteSpace(v))) result.Add(dict);
            }

            return result;
        }
        catch (Exception ex) when (ex is not BusinessException)
        {
            throw new BusinessException("No se pudo leer el archivo Excel. Verifique que sea un .xlsx válido.");
        }
    }

    private static List<IReadOnlyDictionary<string, string>> ReadCsv(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var content = reader.ReadToEnd();
        var firstLine = content.Split('\n').FirstOrDefault() ?? string.Empty;
        var delimiter = firstLine.Count(c => c == ';') > firstLine.Count(c => c == ',') ? ";" : ",";

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = delimiter,
            PrepareHeaderForMatch = args => SpreadsheetRowExtensions.NormalizeHeader(args.Header),
            MissingFieldFound = null,
            BadDataFound = null,
            TrimOptions = TrimOptions.Trim
        };

        using var csv = new CsvReader(new StringReader(content), config);
        csv.Read();
        csv.ReadHeader();
        var headers = csv.HeaderRecord?.Select(SpreadsheetRowExtensions.NormalizeHeader).ToArray() ?? [];

        var result = new List<IReadOnlyDictionary<string, string>>();
        while (csv.Read())
        {
            var dict = new Dictionary<string, string>();
            for (var i = 0; i < headers.Length; i++)
            {
                dict[headers[i]] = csv.GetField(i)?.Trim() ?? string.Empty;
            }

            if (dict.Values.Any(v => !string.IsNullOrWhiteSpace(v))) result.Add(dict);
        }

        return result;
    }
}

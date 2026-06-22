using System.Text;
using System.Text.Json;
using FacturacionArca.Application.Abstractions;
using FacturacionArca.Domain.Comprobantes;
using QRCoder;

namespace FacturacionArca.Infrastructure.Pdf;

public sealed class QrAfipBuilder : IQrAfipBuilder
{
    private const string BaseUrl = "https://www.afip.gob.ar/fe/qr/?p=";

    public string ConstruirUrl(Comprobante c, string cuitEmisor)
    {
        var datos = new Dictionary<string, object?>
        {
            ["ver"] = 1,
            ["fecha"] = c.FechaEmision.ToString("yyyy-MM-dd"),
            ["cuit"] = long.TryParse(cuitEmisor.Replace("-", ""), out var cuitN) ? cuitN : 0,
            ["ptoVta"] = c.PuntoVenta,
            ["tipoCmp"] = (int)c.Tipo,
            ["nroCmp"] = c.Numero ?? 0,
            // Si la factura original no es en PES, fue "pesificada en vuelo" para AFIP.
            // El QR fiscal DEBE reflejar los valores exactos enviados a ARCA (en PESOS).
            ["importe"] = c.CodigoMoneda == "PES" 
                ? decimal.Round(c.ImporteTotal, 2) 
                : decimal.Round(c.ImporteTotal * c.CotizacionMoneda, 2),
            ["moneda"] = c.CodigoMoneda == "PES" ? c.CodigoMoneda : "PES",
            ["ctz"] = c.CodigoMoneda == "PES" ? decimal.Round(c.CotizacionMoneda, 6) : 1m,
            ["tipoDocRec"] = c.Receptor.CodigoTipoDocumento,
            ["nroDocRec"] = long.TryParse(c.Receptor.NumeroDocumento, out var docN) ? docN : 0,
            ["tipoCodAut"] = "E",
            ["codAut"] = long.TryParse(c.Cae?.Numero, out var caeN) ? caeN : 0,
        };

        var json = JsonSerializer.Serialize(datos, new JsonSerializerOptions { WriteIndented = false });
        var b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        return BaseUrl + b64;
    }

    public byte[] GenerarPng(string url)
    {
        using var qrGenerator = new QRCodeGenerator();
        using var qrData = qrGenerator.CreateQrCode(url, QRCodeGenerator.ECCLevel.M);
        var png = new PngByteQRCode(qrData);
        return png.GetGraphic(8);
    }
}

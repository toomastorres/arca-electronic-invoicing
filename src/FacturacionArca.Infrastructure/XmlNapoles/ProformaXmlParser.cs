using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using FacturacionArca.Application.Abstractions;
using FacturacionArca.Domain.Proformas;

namespace FacturacionArca.Infrastructure.XmlNapoles;

public sealed class ProformaXmlParser : IProformaXmlParser
{
    private static readonly CultureInfo InvariantArs = CultureInfo.InvariantCulture;

    public ProformaNapoles Parse(string xmlContent, string archivoOrigen)
    {
        if (string.IsNullOrWhiteSpace(xmlContent))
            throw new InvalidOperationException("El XML está vacío.");

        var normalizado = NormalizarXml(xmlContent);
        XDocument doc;
        try
        {
            doc = XDocument.Parse(normalizado, LoadOptions.PreserveWhitespace);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"No se pudo parsear el XML de la proforma '{archivoOrigen}': {ex.Message}", ex);
        }

        var root = doc.Root ?? throw new InvalidOperationException("El XML no tiene elemento raíz.");

        var p = new ProformaNapoles
        {
            ArchivoOrigen = archivoOrigen,
            NumeroProforma = ReadString(root, "Proforma"),
            CodigoTipoComprobanteOrigen = ReadInt(root, "CodigoTipoComprobante") ?? 0,
            FechaEmision = ReadDate(root, "FechaEmision") ?? default,
            CodigoTipoDocumento = ReadInt(root, "CodigoTipoDocumento") ?? 80,
            NumeroDocumento = ReadString(root, "NumeroDocumento"),
            IvaCondicionTexto = ReadString(root, "IVACondicion"),
            Cliente = ReadString(root, "Cliente"),
            Domicilio = ReadString(root, "Domicilio"),
            CondicionVentaTexto = string.IsNullOrWhiteSpace(ReadString(root, "CondicionVenta")) ? "Contado" : ReadString(root, "CondicionVenta"),
            BuqueViaje = ReadString(root, "BuqueViaje"),
            PorCuenta = ReadString(root, "PorCuenta"),
            Conocimiento = ReadString(root, "Conocimiento"),
            ImporteGravado = ReadDecimal(root, "ImporteGravado") ?? 0m,
            ImporteNoGravado = ReadDecimal(root, "ImporteNoGravado") ?? 0m,
            ImporteSubtotal = ReadDecimal(root, "ImporteSubtotal") ?? 0m,
            ImporteIvaRi = ReadDecimal(root, "ImporteIvaRi") ?? 0m,
            ImporteIvaRni = ReadDecimal(root, "ImporteIvaRni") ?? 0m,
            ImporteTotal = ReadDecimal(root, "ImporteTotal") ?? 0m,
            CodigoMoneda = string.IsNullOrWhiteSpace(ReadString(root, "CodigoMoneda")) ? "PES" : ReadString(root, "CodigoMoneda"),
            CotizacionMoneda = ReadDecimal(root, "CotizacionMoneda") ?? 1m,
        };

        var asociadoEl = FindElement(root, "ComprobanteAsociado");
        if (asociadoEl is not null && TieneContenido(asociadoEl))
        {
            p.ComprobanteAsociado = new ComprobanteAsociadoProforma
            {
                CodigoTipoComprobante = ReadInt(asociadoEl, "CodigoTipoComprobante"),
                NumeroPuntoVenta = ReadInt(asociadoEl, "NumeroPuntoVenta"),
                NumeroComprobante = ReadLong(asociadoEl, "NumeroComprobante"),
                FechaEmision = ReadDate(asociadoEl, "FechaEmision"),
                CuitEmisor = ReadString(asociadoEl, "CuitEmisor"),
            };
        }

        foreach (var st in FindAllElements(root, "SubtotalIva"))
        {
            p.SubtotalesIva.Add(new SubtotalIvaProforma
            {
                Codigo = ReadInt(st, "Codigo") ?? 0,
                Importe = ReadDecimal(st, "Importe") ?? 0m,
            });
        }

        var cargos = FindElement(root, "Cargos");
        if (cargos is not null)
        {
            int currentNumero = 0;
            string currentRefs = "";
            string currentDesc = "";

            foreach (var child in cargos.Elements())
            {
                var name = child.Name.LocalName;
                if (string.Equals(name, "NumeroItem", StringComparison.OrdinalIgnoreCase))
                    int.TryParse(child.Value.Trim(), NumberStyles.Integer, InvariantArs, out currentNumero);
                else if (string.Equals(name, "Referencias", StringComparison.OrdinalIgnoreCase))
                    currentRefs = child.Value.Trim();
                else if (string.Equals(name, "Descripcion", StringComparison.OrdinalIgnoreCase))
                    currentDesc = child.Value.Trim();
                else if (string.Equals(name, "Item", StringComparison.OrdinalIgnoreCase))
                {
                    var unitarios = FindAllElements(child, "PrecioUnitario").Select(x => ReadDecimalValue(x.Value)).ToList();
                    var precioUnitario = unitarios.Count > 0 ? unitarios[^1] : 0m;

                    p.Items.Add(new ItemProforma
                    {
                        NumeroItem = currentNumero,
                        Referencias = currentRefs,
                        DescripcionGrupo = currentDesc,
                        UnidadesMtx = ReadInt(child, "UnidadesMtx") ?? 0,
                        CodigoMtx = ReadString(child, "CodigoMtx"),
                        CodigoItem = ReadString(child, "CodigoItem"),
                        ItemDescripcion = ReadString(child, "ItemDescripcion"),
                        ImpoExpo = ReadString(child, "ImpoExpo"),
                        TarifaDescripcion = ReadString(child, "TarifaDescripcion"),
                        PrecioUnitario = precioUnitario,
                        Cantidad = ReadDecimal(child, "Cantidad") ?? 0m,
                        CodigoUnidadMedida = ReadInt(child, "CodigoUnidadMedida") ?? 7,
                        CodigoCondicionIva = ReadInt(child, "CodigoCondicionIva") ?? 0,
                        ImporteIva = ReadDecimal(child, "ImporteIva") ?? 0m,
                        ImporteItem = ReadDecimal(child, "ImporteItem") ?? 0m,
                    });
                }
            }
        }

        return p;
    }

    private static string NormalizarXml(string raw)
    {
        var sb = new StringBuilder(raw.Length + 64);
        if (!raw.TrimStart().StartsWith("<?xml", StringComparison.OrdinalIgnoreCase))
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.Append(raw);
        var texto = sb.ToString();

        texto = Regex.Replace(texto, @"<NumeroPuntoventa\b", "<NumeroPuntoVenta",
            RegexOptions.IgnoreCase);
        texto = Regex.Replace(texto, @"</NumeroPuntoventa\s*>", "</NumeroPuntoVenta>",
            RegexOptions.IgnoreCase);

        return texto;
    }

    private static XElement? FindElement(XElement parent, string name) =>
        parent.Elements().FirstOrDefault(e =>
            string.Equals(e.Name.LocalName, name, StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<XElement> FindAllElements(XElement parent, string name) =>
        parent.Elements().Where(e =>
            string.Equals(e.Name.LocalName, name, StringComparison.OrdinalIgnoreCase));

    private static bool TieneContenido(XElement el) =>
        el.Elements().Any(e => !string.IsNullOrWhiteSpace(e.Value)) ||
        !string.IsNullOrWhiteSpace(el.Value);

    private static string ReadString(XElement parent, string name) =>
        FindElement(parent, name)?.Value.Trim() ?? "";

    private static int? ReadInt(XElement parent, string name)
    {
        var v = FindElement(parent, name)?.Value.Trim();
        return int.TryParse(v, NumberStyles.Integer, InvariantArs, out var r) ? r : null;
    }

    private static long? ReadLong(XElement parent, string name)
    {
        var v = FindElement(parent, name)?.Value.Trim();
        return long.TryParse(v, NumberStyles.Integer, InvariantArs, out var r) ? r : null;
    }

    private static decimal? ReadDecimal(XElement parent, string name) =>
        FindElement(parent, name) is { } el ? ReadDecimalNullable(el.Value) : null;

    private static decimal? ReadDecimalNullable(string v)
    {
        if (string.IsNullOrWhiteSpace(v)) return null;
        return decimal.TryParse(v.Trim(), NumberStyles.Number, InvariantArs, out var r) ? r : null;
    }

    private static decimal ReadDecimalValue(string v) => ReadDecimalNullable(v) ?? 0m;

    private static DateOnly? ReadDate(XElement parent, string name)
    {
        var v = FindElement(parent, name)?.Value.Trim();
        if (string.IsNullOrWhiteSpace(v)) return null;
        var formatos = new[] { "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "yyyyMMdd" };
        return DateTime.TryParseExact(v, formatos, InvariantArs, DateTimeStyles.None, out var dt)
            ? DateOnly.FromDateTime(dt)
            : null;
    }
}

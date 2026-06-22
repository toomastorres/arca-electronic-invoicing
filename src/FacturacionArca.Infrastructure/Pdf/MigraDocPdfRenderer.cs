using System.Globalization;
using FacturacionArca.Application.Abstractions;
using FacturacionArca.Domain.Comprobantes;
using FacturacionArca.Domain.Configuracion;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using Image = MigraDoc.DocumentObjectModel.Shapes.Image;
using Font = MigraDoc.DocumentObjectModel.Font;
using Colors = MigraDoc.DocumentObjectModel.Colors;

namespace FacturacionArca.Infrastructure.Pdf;

/// <summary>
/// Genera el PDF de factura utilizando MigraDoc (PdfSharp).
/// 100% Gratuito y de código abierto (Licencia MIT), sin límites comerciales ni marcas de agua.
/// </summary>
public sealed class MigraDocPdfRenderer : IPdfRenderer
{
    private static readonly CultureInfo Cult = new("es-AR");
    private static readonly Color ColorTablaHeader = Colors.LightGray;

    private readonly IQrAfipBuilder _qr;
    private readonly IConfiguracionRepository _configRepo;

    public MigraDocPdfRenderer(IQrAfipBuilder qr, IConfiguracionRepository configRepo)
    {
        _qr = qr;
        _configRepo = configRepo;
    }

    public async Task<byte[]> RenderizarAsync(Comprobante c, CancellationToken ct = default)
    {
        var cfg = await _configRepo.GetAsync(ct);
        var qrUrl = _qr.ConstruirUrl(c, cfg.Cuit);
        var qrPng = _qr.GenerarPng(qrUrl);

        var doc = new Document();
        doc.Info.Title = $"Comprobante {c.Tipo.LetraVisible()} {c.PuntoVenta:D4}-{c.Numero:D8}";

        var sec = doc.AddSection();
        sec.PageSetup.PageFormat = PageFormat.A4;
        sec.PageSetup.TopMargin = Unit.FromCentimeter(1);
        sec.PageSetup.BottomMargin = Unit.FromCentimeter(1);
        sec.PageSetup.LeftMargin = Unit.FromCentimeter(1);
        sec.PageSetup.RightMargin = Unit.FromCentimeter(1);

        RenderHeader(sec, c, cfg, qrPng);
        RenderReceptor(sec, c, cfg);
        RenderTablaItems(sec, c);
        RenderTotales(sec, c);
        RenderCae(sec, c);
        RenderFooter(sec, c, cfg);

        // Generar PDF en memoria
        var pdfRenderer = new PdfDocumentRenderer() { Document = doc };
        pdfRenderer.RenderDocument();

        using var ms = new MemoryStream();
        pdfRenderer.PdfDocument.Save(ms);
        return ms.ToArray();
    }

    public async Task<string> RenderizarYGuardarAsync(Comprobante c, string carpetaSalida, CancellationToken ct = default)
    {
        var bytes = await RenderizarAsync(c, ct);
        Directory.CreateDirectory(carpetaSalida);
        var letra = c.Tipo.LetraVisible();
        var nombre = $"FF{letra}{c.PuntoVenta:D4}{c.Numero:D8}.pdf";
        var path = Path.Combine(carpetaSalida, nombre);
        await File.WriteAllBytesAsync(path, bytes, ct);
        return path;
    }

    private static void RenderHeader(Section sec, Comprobante c, ConfiguracionEmpresa cfg, byte[] qrPng)
    {
        var table = sec.AddTable();
        table.AddColumn(Unit.FromCentimeter(8));
        table.AddColumn(Unit.FromCentimeter(3));
        table.AddColumn(Unit.FromCentimeter(8));

        var row = table.AddRow();
        
        // Col 1: Datos empresa
        var col1 = row.Cells[0];
        col1.AddParagraph(cfg.RazonSocial).Format.Font.Bold = true;
        col1.AddParagraph(cfg.RazonSocial).Format.Font.Size = 12; // Title
        if (!string.IsNullOrWhiteSpace(cfg.DomicilioComercial))
            col1.AddParagraph(cfg.DomicilioComercial).Format.Font.Size = 8;
        col1.AddParagraph(cfg.CondicionFrenteIva.ToUpperInvariant()).Format.Font.Size = 8;

        // Col 2: Letra (A/B/C/E)
        var col2 = row.Cells[1];
        col2.Borders.Width = 1;
        col2.Format.Alignment = ParagraphAlignment.Center;
        col2.VerticalAlignment = VerticalAlignment.Center;
        var pLetra = col2.AddParagraph(c.Tipo.LetraVisible());
        pLetra.Format.Font.Size = 36;
        pLetra.Format.Font.Bold = true;
        col2.AddParagraph($"código {(int)c.Tipo:D2}").Format.Font.Size = 8;

        // Col 3: Datos fiscales y QR
        var col3 = row.Cells[2];
        var innerTable = col3.Elements.AddTable();
        innerTable.AddColumn(Unit.FromCentimeter(5.5));
        innerTable.AddColumn(Unit.FromCentimeter(2.5)); // Para el QR
        var innerRow = innerTable.AddRow();
        
        var datosFisc = innerRow.Cells[0];
        datosFisc.AddParagraph(NombreDocumento(c.Tipo).ToUpperInvariant()).Format.Font.Bold = true;
        datosFisc.AddParagraph($"N° {c.PuntoVenta:D4}-{c.Numero:D8}").Format.Font.Bold = true;
        datosFisc.AddParagraph($"Fecha: {c.FechaEmision:dd/MM/yyyy}").Format.Font.Size = 8;
        datosFisc.AddParagraph($"C.U.I.T.: {FormatearCuit(cfg.Cuit)}").Format.Font.Size = 8;
        datosFisc.AddParagraph($"Ing.Brutos: {cfg.IngresosBrutos}").Format.Font.Size = 8;
        if (cfg.InicioActividades != default)
            datosFisc.AddParagraph($"Inicio de Actividades: {cfg.InicioActividades:dd-MM-yy}").Format.Font.Size = 8;
        if (!string.IsNullOrWhiteSpace(c.NumeroProforma))
            datosFisc.AddParagraph($"Pf.: {c.NumeroProforma}").Format.Font.Size = 8;

        // QR
        var base64Qr = Convert.ToBase64String(qrPng);
        var qrCell = innerRow.Cells[1];
        var image = qrCell.AddImage("base64:" + base64Qr);
        image.Width = Unit.FromCentimeter(2.2);

        // ORIGINAL
        var pOriginal = sec.AddParagraph("ORIGINAL");
        pOriginal.Format.Alignment = ParagraphAlignment.Center;
        pOriginal.Format.Font.Bold = true;
        pOriginal.Format.Font.Size = 9;
        pOriginal.Format.SpaceAfter = Unit.FromCentimeter(0.5);
    }

    private static void RenderReceptor(Section sec, Comprobante c, ConfiguracionEmpresa cfg)
    {
        var table = sec.AddTable();
        table.Borders.Width = 1;
        table.AddColumn(Unit.FromCentimeter(3.5));
        table.AddColumn(Unit.FromCentimeter(15.5));

        FilaReceptor(table, "Consignatario:", c.Receptor.RazonSocial);
        FilaReceptor(table, "Domicilio:", c.Receptor.Domicilio);
        FilaReceptor(table, "I.V.A.:", c.Receptor.CondicionIvaTextoOriginal ?? c.Receptor.CondicionIva.ToString());
        FilaReceptor(table, "C.U.I.T.:", c.Receptor.NumeroDocumento);
        
        var condVtaText = c.CondicionVenta.ToString();
        if (!string.IsNullOrWhiteSpace(cfg.CbuPesos))
            condVtaText += $"                 CBU Pesos: {cfg.CbuPesos}";
        FilaReceptor(table, "Condicion de Venta:", condVtaText);
        
        sec.AddParagraph().Format.SpaceAfter = Unit.FromCentimeter(0.5);
    }

    private static void FilaReceptor(Table t, string label, string valor)
    {
        var r = t.AddRow();
        r.Cells[0].AddParagraph(label).Format.Font.Bold = true;
        r.Cells[0].Format.Font.Size = 8;
        r.Cells[1].AddParagraph(valor).Format.Font.Size = 8;
    }

    private static void RenderTablaItems(Section sec, Comprobante c)
    {
        var esPesos = c.CodigoMoneda.Equals("PES", StringComparison.OrdinalIgnoreCase);
        var monedaSimbolo = esPesos ? "$" : c.CodigoMoneda;

        var t = sec.AddTable();
        t.Borders.Width = 1;
        t.AddColumn(Unit.FromCentimeter(3.5)); // References
        t.AddColumn(Unit.FromCentimeter(2)); // Charge Type
        t.AddColumn(Unit.FromCentimeter(2)); // Rate Basis
        t.AddColumn(Unit.FromCentimeter(1.5)); // Rate Base
        t.AddColumn(Unit.FromCentimeter(1.5)); // Factor
        t.AddColumn(Unit.FromCentimeter(3)); // Original Amount
        t.AddColumn(Unit.FromCentimeter(3)); // Local Amount
        t.AddColumn(Unit.FromCentimeter(2.5)); // VAT Rate

        var hRow = t.AddRow();
        hRow.Shading.Color = ColorTablaHeader;
        hRow.Format.Font.Bold = true;
        hRow.Format.Font.Size = 7;
        hRow.Format.Alignment = ParagraphAlignment.Center;
        
        hRow.Cells[0].AddParagraph("References");
        hRow.Cells[1].AddParagraph("Charge\nType");
        hRow.Cells[2].AddParagraph("Rate\nBasis");
        hRow.Cells[3].AddParagraph("Rate\nBase");
        hRow.Cells[4].AddParagraph("Factor");
        hRow.Cells[5].AddParagraph($"Original Amount\n{monedaSimbolo}");
        hRow.Cells[6].AddParagraph("Local Amount\n$");
        hRow.Cells[7].AddParagraph("VAT\nRate");

        if (!string.IsNullOrWhiteSpace(c.BuqueViaje))
            FilaSpan(t, $"M/V: {c.BuqueViaje}");
        if (!string.IsNullOrWhiteSpace(c.PorCuenta))
            FilaSpan(t, $"Por Cuenta/Orden: {c.PorCuenta}");
        if (!string.IsNullOrWhiteSpace(c.Conocimiento))
            FilaSpan(t, $"B/L: {c.Conocimiento}");

        foreach (var item in c.Items)
        {
            var pct = AlicuotaPorcentaje(item.CodigoAlicuotaAfip);
            var localAmount = item.ImporteItem;
            var origAmount = esPesos ? localAmount : localAmount / c.CotizacionMoneda;

            var r = t.AddRow();
            r.Format.Font.Size = 8;
            r.Cells[0].AddParagraph(item.Descripcion);
            r.Cells[1].AddParagraph(item.TipoCargoDescripcion);
            r.Cells[2].AddParagraph(item.TarifaDescripcion);
            r.Cells[3].AddParagraph(item.PrecioUnitario.ToString("N2", Cult)).Format.Alignment = ParagraphAlignment.Right;
            r.Cells[4].AddParagraph(item.Cantidad.ToString("N3", Cult)).Format.Alignment = ParagraphAlignment.Right;
            r.Cells[5].AddParagraph(origAmount.ToString("N2", Cult)).Format.Alignment = ParagraphAlignment.Right;
            r.Cells[6].AddParagraph(localAmount.ToString("N2", Cult)).Format.Alignment = ParagraphAlignment.Right;
            r.Cells[7].AddParagraph(pct > 0 ? pct.ToString("N2", Cult) : "").Format.Alignment = ParagraphAlignment.Right;
        }
        
        sec.AddParagraph().Format.SpaceAfter = Unit.FromCentimeter(0.5);
    }

    private static void FilaSpan(Table t, string texto)
    {
        var r = t.AddRow();
        r.Cells[0].MergeRight = 7;
        r.Cells[0].AddParagraph(texto).Format.Font.Size = 8;
    }

    private static void RenderTotales(Section sec, Comprobante c)
    {
        var esPesos = c.CodigoMoneda.Equals("PES", StringComparison.OrdinalIgnoreCase);
        var totalArs = esPesos ? c.ImporteTotal : c.ImporteTotal * c.CotizacionMoneda;
        var subTotal = c.ImporteNeto + c.ImporteNoGravado;
        var ibbTotal = c.Tributos.Where(t => t.IdAfip == Tributo.IdIngresosBrutos).Sum(t => t.Importe);

        var pSub = sec.AddParagraph();
        pSub.Format.Alignment = ParagraphAlignment.Right;
        pSub.AddFormattedText("Pesos", TextFormat.Bold);
        pSub.Format.Font.Size = 8;

        var tSub = sec.AddTable();
        tSub.Format.Alignment = ParagraphAlignment.Right;
        tSub.AddColumn(Unit.FromCentimeter(15));
        tSub.AddColumn(Unit.FromCentimeter(4));
        var rSub = tSub.AddRow();
        rSub.Cells[0].AddParagraph("Sub-Total").Format.Font.Bold = true;
        rSub.Cells[0].Format.Alignment = ParagraphAlignment.Right;
        rSub.Cells[1].Borders.Width = 1;
        rSub.Cells[1].AddParagraph(subTotal.ToString("N2", Cult)).Format.Alignment = ParagraphAlignment.Right;
        rSub.Format.Font.Size = 8;

        sec.AddParagraph().Format.SpaceAfter = Unit.FromCentimeter(0.2);

        var tTot = sec.AddTable();
        tTot.Borders.Width = 1;
        for (int i = 0; i < 6; i++) tTot.AddColumn(Unit.FromCentimeter(19.0 / 6));

        var rHead = tTot.AddRow();
        rHead.Format.Font.Bold = true;
        rHead.Format.Font.Size = 8;
        rHead.Format.Alignment = ParagraphAlignment.Center;
        string[] hdrs = { "Gravado", "No Gravado", "Percep.I.B.", "I.V.A. RI", "Redondeo", "Total" };
        for (int i = 0; i < 6; i++) rHead.Cells[i].AddParagraph(hdrs[i]);

        var rVal = tTot.AddRow();
        rVal.Format.Font.Size = 8;
        rVal.Format.Alignment = ParagraphAlignment.Right;
        rVal.Cells[0].AddParagraph(c.ImporteNeto.ToString("N2", Cult));
        rVal.Cells[1].AddParagraph(c.ImporteNoGravado.ToString("N2", Cult));
        rVal.Cells[2].AddParagraph(ibbTotal.ToString("N2", Cult));
        rVal.Cells[3].AddParagraph(c.ImporteIva.ToString("N2", Cult));
        rVal.Cells[4].AddParagraph("0.00");
        rVal.Cells[5].AddParagraph(c.ImporteTotal.ToString("N2", Cult)).Format.Font.Bold = true;

        sec.AddParagraph().Format.SpaceAfter = Unit.FromCentimeter(0.5);

        // Son Pesos y textos obligatorios
        var monedaNombre = NombreMoneda(c.CodigoMoneda);
        var pLetras = sec.AddParagraph();
        pLetras.Format.Font.Size = 8;
        pLetras.Format.Font.Bold = true;
        
        // Legal texts exactly as requested
        pLetras.AddText("Los pesos informados en la presente factura, son el contravalor de los montos en moneda extranjera totalizados en la columna 1 a la cotización informada al pie de la misma. Esta factura deberá ser cancelada al tipo de cambio vigente a la fecha de pago de la misma.");
        pLetras.AddLineBreak();
        pLetras.AddLineBreak();
        
        pLetras.AddText($"Son {monedaNombre}: {NumeroEnLetras.Convertir(c.ImporteTotal, monedaNombre)}");
        pLetras.AddLineBreak();
        
        if (!esPesos)
        {
            pLetras.AddText($"Son Pesos: {NumeroEnLetras.Convertir(totalArs, "Pesos")}");
            pLetras.AddLineBreak();
            pLetras.AddText($"Cotización USD: {c.CotizacionMoneda.ToString("N4", Cult)}");
            pLetras.AddLineBreak();
        }

        pLetras.AddText("FACTURA PAGADERA EN PESOS ARGENTINOS");
    }

    private static void RenderCae(Section sec, Comprobante c)
    {
        sec.AddParagraph().Format.SpaceAfter = Unit.FromCentimeter(0.5);
        var p = sec.AddParagraph();
        p.Format.Alignment = ParagraphAlignment.Right;
        
        if (c.Cae is null)
        {
            p.AddText("Comprobante en borrador — sin CAE");
            p.Format.Font.Italic = true;
            return;
        }

        p.Format.Font.Bold = true;
        p.Format.Font.Size = 11;
        p.AddText($"C.A.E. N°: {c.Cae.Numero}");
        p.AddLineBreak();
        p.AddText($"Fecha de Vto.: {c.Cae.FechaVencimiento:dd/MM/yyyy}");
    }

    private static void RenderFooter(Section sec, Comprobante c, ConfiguracionEmpresa cfg)
    {
        var fecha = c.FechaEmisionEfectiva.HasValue
            ? c.FechaEmisionEfectiva.Value.ToString("dd/MM/yyyy")
            : DateTime.Today.ToString("dd/MM/yyyy");

        var p = sec.AddParagraph();
        p.Format.Borders.Top.Width = 1;
        p.Format.SpaceBefore = Unit.FromCentimeter(1);
        p.Format.Font.Size = 7;
        p.AddText($"Impreso por {cfg.RazonSocial} - CUIT. {FormatearCuit(cfg.Cuit)} Fecha: {fecha}");
    }

    private static string NombreDocumento(TipoComprobante tipo) =>
        tipo.EsFce() && tipo.EsNotaCredito() ? "Nota de Crédito Electrónica MiPyME"
        : tipo.EsFce() && tipo.EsNotaDebito() ? "Nota de Débito Electrónica MiPyME"
        : tipo.EsFce() ? "Factura de Crédito Electrónica MiPyME"
        : tipo.EsNotaCredito() ? "Nota de Crédito"
        : tipo.EsNotaDebito() ? "Nota de Débito"
        : "Factura";

    private static decimal AlicuotaPorcentaje(int codigoAfip) => codigoAfip switch
    {
        3 => 0m,
        4 => 10.5m,
        5 => 21m,
        6 => 27m,
        8 => 5m,
        9 => 2.5m,
        _ => 0m
    };

    private static string NombreMoneda(string codigo) => codigo.ToUpperInvariant() switch
    {
        "PES" => "Pesos",
        "DOL" => "Dólares",
        "060" => "Euros",
        "012" => "Reales",
        _ => "Pesos"
    };

    private static string FormatearCuit(string cuit)
    {
        if (cuit.Length == 11)
            return $"{cuit[..2]}-{cuit[2..10]}-{cuit[10]}";
        return cuit;
    }
}

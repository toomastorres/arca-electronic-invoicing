using System.Globalization;
using System.Text;
using FacturacionArca.Domain.Comprobantes;
using FacturacionArca.Domain.Wsaa;

namespace FacturacionArca.Infrastructure.Arca.Wsfev1;

public static class Wsfev1RequestBuilder
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static string FECAESolicitar(TicketAcceso ta, string cuit, Comprobante c)
    {
        var concepto = (int)c.Concepto;
        var cbteFch = c.FechaEmision.ToString("yyyyMMdd", Inv);
        var fchServDesde = c.FechaServicioDesde?.ToString("yyyyMMdd", Inv) ?? "";
        var fchServHasta = c.FechaServicioHasta?.ToString("yyyyMMdd", Inv) ?? "";
        var fchVtoPago = c.FechaVencimientoPago?.ToString("yyyyMMdd", Inv) ?? "";

        var ivasXml = ConstruirIvas(c);
        var tributosXml = ConstruirTributos(c);
        var cbtesAsocXml = ConstruirCbtesAsoc(c);
        var opcionalesXml = ConstruirOpcionales(c);

        var camposServicio = c.Concepto != Concepto.Productos
            ? $"""
                  <ar:FchServDesde>{fchServDesde}</ar:FchServDesde>
                  <ar:FchServHasta>{fchServHasta}</ar:FchServHasta>
                  <ar:FchVtoPago>{fchVtoPago}</ar:FchVtoPago>
              """
            : "";

        var monCotiz = c.CotizacionMoneda.ToString("F6", Inv);

        return $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" xmlns:ar="http://ar.gov.afip.dif.FEV1/">
          <soapenv:Header/>
          <soapenv:Body>
            <ar:FECAESolicitar>
              <ar:Auth>
                <ar:Token>{System.Security.SecurityElement.Escape(ta.Token)}</ar:Token>
                <ar:Sign>{System.Security.SecurityElement.Escape(ta.Sign)}</ar:Sign>
                <ar:Cuit>{cuit}</ar:Cuit>
              </ar:Auth>
              <ar:FeCAEReq>
                <ar:FeCabReq>
                  <ar:CantReg>1</ar:CantReg>
                  <ar:PtoVta>{c.PuntoVenta}</ar:PtoVta>
                  <ar:CbteTipo>{(int)c.Tipo}</ar:CbteTipo>
                </ar:FeCabReq>
                <ar:FeDetReq>
                  <ar:FECAEDetRequest>
                    <ar:Concepto>{concepto}</ar:Concepto>
                    <ar:DocTipo>{c.Receptor.CodigoTipoDocumento}</ar:DocTipo>
                    <ar:DocNro>{c.Receptor.NumeroDocumento}</ar:DocNro>
                    <ar:CbteDesde>{c.Numero}</ar:CbteDesde>
                    <ar:CbteHasta>{c.Numero}</ar:CbteHasta>
                    <ar:CbteFch>{cbteFch}</ar:CbteFch>
                    <ar:ImpTotal>{c.ImporteTotal.ToString("F2", Inv)}</ar:ImpTotal>
                    <ar:ImpTotConc>{c.ImporteNoGravado.ToString("F2", Inv)}</ar:ImpTotConc>
                    <ar:ImpNeto>{c.ImporteNeto.ToString("F2", Inv)}</ar:ImpNeto>
                    <ar:ImpOpEx>{c.ImporteExento.ToString("F2", Inv)}</ar:ImpOpEx>
                    <ar:ImpTrib>{c.ImporteTributos.ToString("F2", Inv)}</ar:ImpTrib>
                    <ar:ImpIVA>{c.ImporteIva.ToString("F2", Inv)}</ar:ImpIVA>
        {camposServicio}
                    <ar:CondicionIVAReceptorId>{(int)c.Receptor.CondicionIva}</ar:CondicionIVAReceptorId>
                    <ar:MonId>{c.CodigoMoneda}</ar:MonId>
                    <ar:MonCotiz>{monCotiz}</ar:MonCotiz>
        {cbtesAsocXml}
        {tributosXml}
        {ivasXml}
        {opcionalesXml}
                  </ar:FECAEDetRequest>
                </ar:FeDetReq>
              </ar:FeCAEReq>
            </ar:FECAESolicitar>
          </soapenv:Body>
        </soapenv:Envelope>
        """;
    }

    private static string ConstruirIvas(Comprobante c)
    {
        if (c.SubtotalesIva.Count == 0) return "";
        var sb = new StringBuilder();
        sb.AppendLine("            <ar:Iva>");
        foreach (var iva in c.SubtotalesIva)
        {
            sb.AppendLine("              <ar:AlicIva>");
            sb.AppendLine($"                <ar:Id>{iva.CodigoAlicuotaAfip}</ar:Id>");
            sb.AppendLine($"                <ar:BaseImp>{iva.BaseImponible.ToString("F2", Inv)}</ar:BaseImp>");
            sb.AppendLine($"                <ar:Importe>{iva.Importe.ToString("F2", Inv)}</ar:Importe>");
            sb.AppendLine("              </ar:AlicIva>");
        }
        sb.Append("            </ar:Iva>");
        return sb.ToString();
    }

    /// <summary>
    /// Bloque &lt;Opcionales&gt; — específico de FCE MiPyMEs:
    ///   - Id 27 = Tipo de transmisión: "S" (SCA, Sist. Circulación Abierta) o "N" (ADC, Agente Depósito Colectivo).
    ///   - Id 2101 = CBU del beneficiario (cuando SCA).
    ///   - Id 2102 = Alias bancario del beneficiario (opcional).
    /// </summary>
    private static string ConstruirOpcionales(Comprobante c)
    {
        if (!c.Tipo.EsFce()) return "";
        if (c.EsSCA is null && string.IsNullOrWhiteSpace(c.CbuFce) && string.IsNullOrWhiteSpace(c.AliasFce))
            return "";

        var sb = new StringBuilder();
        sb.AppendLine("            <ar:Opcionales>");
        if (c.EsSCA is not null)
        {
            var valor = c.EsSCA.Value ? "S" : "N";
            sb.AppendLine("              <ar:Opcional>");
            sb.AppendLine("                <ar:Id>27</ar:Id>");
            sb.AppendLine($"                <ar:Valor>{valor}</ar:Valor>");
            sb.AppendLine("              </ar:Opcional>");
        }
        if (!string.IsNullOrWhiteSpace(c.CbuFce))
        {
            sb.AppendLine("              <ar:Opcional>");
            sb.AppendLine("                <ar:Id>2101</ar:Id>");
            sb.AppendLine($"                <ar:Valor>{System.Security.SecurityElement.Escape(c.CbuFce)}</ar:Valor>");
            sb.AppendLine("              </ar:Opcional>");
        }
        if (!string.IsNullOrWhiteSpace(c.AliasFce))
        {
            sb.AppendLine("              <ar:Opcional>");
            sb.AppendLine("                <ar:Id>2102</ar:Id>");
            sb.AppendLine($"                <ar:Valor>{System.Security.SecurityElement.Escape(c.AliasFce)}</ar:Valor>");
            sb.AppendLine("              </ar:Opcional>");
        }
        sb.Append("            </ar:Opcionales>");
        return sb.ToString();
    }

    private static string ConstruirCbtesAsoc(Comprobante c)
    {
        if (c.ComprobantesAsociados.Count == 0) return "";
        var sb = new StringBuilder();
        sb.AppendLine("            <ar:CbtesAsoc>");
        foreach (var a in c.ComprobantesAsociados)
        {
            sb.AppendLine("              <ar:CbteAsoc>");
            sb.AppendLine($"                <ar:Tipo>{a.CodigoTipoComprobante}</ar:Tipo>");
            sb.AppendLine($"                <ar:PtoVta>{a.PuntoVenta}</ar:PtoVta>");
            sb.AppendLine($"                <ar:Nro>{a.Numero}</ar:Nro>");
            if (!string.IsNullOrWhiteSpace(a.CuitEmisor))
                sb.AppendLine($"                <ar:Cuit>{a.CuitEmisor}</ar:Cuit>");
            if (a.FechaEmision is not null)
                sb.AppendLine($"                <ar:CbteFch>{a.FechaEmision:yyyyMMdd}</ar:CbteFch>");
            sb.AppendLine("              </ar:CbteAsoc>");
        }
        sb.Append("            </ar:CbtesAsoc>");
        return sb.ToString();
    }

    private static string ConstruirTributos(Comprobante c)
    {
        if (c.Tributos.Count == 0) return "";
        var sb = new StringBuilder();
        sb.AppendLine("            <ar:Tributos>");
        foreach (var t in c.Tributos)
        {
            sb.AppendLine("              <ar:Tributo>");
            sb.AppendLine($"                <ar:Id>{t.IdAfip}</ar:Id>");
            sb.AppendLine($"                <ar:Desc>{System.Security.SecurityElement.Escape(t.Descripcion)}</ar:Desc>");
            sb.AppendLine($"                <ar:BaseImp>{t.BaseImponible.ToString("F2", Inv)}</ar:BaseImp>");
            sb.AppendLine($"                <ar:Alic>{t.Alicuota.ToString("F2", Inv)}</ar:Alic>");
            sb.AppendLine($"                <ar:Importe>{t.Importe.ToString("F2", Inv)}</ar:Importe>");
            sb.AppendLine("              </ar:Tributo>");
        }
        sb.Append("            </ar:Tributos>");
        return sb.ToString();
    }

    public static string FECompUltimoAutorizado(TicketAcceso ta, string cuit, int ptoVta, int cbteTipo) =>
        $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" xmlns:ar="http://ar.gov.afip.dif.FEV1/">
          <soapenv:Body>
            <ar:FECompUltimoAutorizado>
              <ar:Auth><ar:Token>{System.Security.SecurityElement.Escape(ta.Token)}</ar:Token><ar:Sign>{System.Security.SecurityElement.Escape(ta.Sign)}</ar:Sign><ar:Cuit>{cuit}</ar:Cuit></ar:Auth>
              <ar:PtoVta>{ptoVta}</ar:PtoVta>
              <ar:CbteTipo>{cbteTipo}</ar:CbteTipo>
            </ar:FECompUltimoAutorizado>
          </soapenv:Body>
        </soapenv:Envelope>
        """;

    public static string FECompConsultar(TicketAcceso ta, string cuit, int ptoVta, int cbteTipo, long nro) =>
        $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" xmlns:ar="http://ar.gov.afip.dif.FEV1/">
          <soapenv:Body>
            <ar:FECompConsultar>
              <ar:Auth><ar:Token>{System.Security.SecurityElement.Escape(ta.Token)}</ar:Token><ar:Sign>{System.Security.SecurityElement.Escape(ta.Sign)}</ar:Sign><ar:Cuit>{cuit}</ar:Cuit></ar:Auth>
              <ar:FeCompConsReq>
                <ar:CbteTipo>{cbteTipo}</ar:CbteTipo>
                <ar:CbteNro>{nro}</ar:CbteNro>
                <ar:PtoVta>{ptoVta}</ar:PtoVta>
              </ar:FeCompConsReq>
            </ar:FECompConsultar>
          </soapenv:Body>
        </soapenv:Envelope>
        """;

    public static string FEParamGet(TicketAcceso ta, string cuit, string metodo) =>
        $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" xmlns:ar="http://ar.gov.afip.dif.FEV1/">
          <soapenv:Body>
            <ar:{metodo}>
              <ar:Auth><ar:Token>{System.Security.SecurityElement.Escape(ta.Token)}</ar:Token><ar:Sign>{System.Security.SecurityElement.Escape(ta.Sign)}</ar:Sign><ar:Cuit>{cuit}</ar:Cuit></ar:Auth>
            </ar:{metodo}>
          </soapenv:Body>
        </soapenv:Envelope>
        """;

    public static string FEParamGetCotizacion(TicketAcceso ta, string cuit, string monedaId) =>
        $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" xmlns:ar="http://ar.gov.afip.dif.FEV1/">
          <soapenv:Body>
            <ar:FEParamGetCotizacion>
              <ar:Auth><ar:Token>{System.Security.SecurityElement.Escape(ta.Token)}</ar:Token><ar:Sign>{System.Security.SecurityElement.Escape(ta.Sign)}</ar:Sign><ar:Cuit>{cuit}</ar:Cuit></ar:Auth>
              <ar:MonId>{monedaId}</ar:MonId>
            </ar:FEParamGetCotizacion>
          </soapenv:Body>
        </soapenv:Envelope>
        """;
}

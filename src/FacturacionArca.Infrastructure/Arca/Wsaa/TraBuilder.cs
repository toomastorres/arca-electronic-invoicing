using System.Globalization;
using System.Text;
using System.Xml.Linq;

namespace FacturacionArca.Infrastructure.Arca.Wsaa;

public static class TraBuilder {
    public static string Construir(string servicio, TimeSpan? vigencia = null, DateTime? ahoraUtc = null)
    {
        var ahora = (ahoraUtc ?? DateTime.UtcNow);
        var generationTime = ahora.AddMinutes(-5);
        var expirationTime = ahora.Add(vigencia ?? TimeSpan.FromMinutes(30));
        var uniqueId = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var doc = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement("loginTicketRequest",
                new XAttribute("version", "1.0"),
                new XElement("header",
                    new XElement("uniqueId", uniqueId.ToString(CultureInfo.InvariantCulture)),
                    new XElement("generationTime", generationTime.ToString("yyyy-MM-ddTHH:mm:ss")),
                    new XElement("expirationTime", expirationTime.ToString("yyyy-MM-ddTHH:mm:ss"))),
                new XElement("service", servicio)));

        var sb = new StringBuilder();
        using var writer = new System.IO.StringWriter(sb);
        doc.Save(writer);
        return sb.ToString();
    }
}

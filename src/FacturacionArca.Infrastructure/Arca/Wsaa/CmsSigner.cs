using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace FacturacionArca.Infrastructure.Arca.Wsaa;

public static class ArcaCmsSigner
{
    public static string FirmarBase64(string traXml, X509Certificate2 certificado)
    {
        if (!certificado.HasPrivateKey)
            throw new InvalidOperationException("El certificado no contiene clave privada. Verifique el .pfx importado.");

        var contenido = new ContentInfo(Encoding.UTF8.GetBytes(traXml));
        var signedCms = new SignedCms(contenido);
        var signer = new CmsSigner(SubjectIdentifierType.IssuerAndSerialNumber, certificado);
        signedCms.ComputeSignature(signer, silent: true);
        var firmado = signedCms.Encode();
        return Convert.ToBase64String(firmado);
    }

    public static bool VerificarFirma(string base64Cms, X509Certificate2 certificadoEsperado)
    {
        try
        {
            var bytes = Convert.FromBase64String(base64Cms);
            var cms = new SignedCms();
            cms.Decode(bytes);
            cms.CheckSignature(new X509Certificate2Collection(certificadoEsperado), verifySignatureOnly: true);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

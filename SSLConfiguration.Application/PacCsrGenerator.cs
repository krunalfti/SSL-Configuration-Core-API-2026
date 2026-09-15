using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace SSLConfiguration.Application
{
    /// <summary>
    /// PAC CSR generation — same intent as old ComodoController.CSRInfo_PAC (IsNewCSR=true)
    /// using Name/Surname/Email subject attributes and 4096-bit RSA.
    /// </summary>
    public static class PacCsrGenerator
    {
        public static (string csrPem, string privateKeyPem) Generate(string? firstName, string? lastName, string? email)
        {
            string fn = firstName ?? string.Empty;
            string ln = lastName ?? string.Empty;
            string em = email ?? string.Empty;

            // OID.2.5.4.41 = Name (same as BouncyCastle X509Name.Name), SN = Surname, E = EmailAddress
            string dn = $"OID.2.5.4.41={EscapeDn(fn)}, SN={EscapeDn(ln)}, E={EscapeDn(em)}";

            using RSA rsa = RSA.Create(4096);
            var request = new CertificateRequest(
                new X500DistinguishedName(dn),
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            byte[] csrDer = request.CreateSigningRequest();
            string csrPem = ToPem("CERTIFICATE REQUEST", csrDer);
            string privateKeyPem = rsa.ExportRSAPrivateKeyPem();
            return (csrPem, privateKeyPem);
        }

        public static string ExportPrivateKeyFile(string? orgName, string key)
        {
            string safeName = (orgName ?? "key").Replace(" ", "_");
            string fileName = "Privatekey_" + safeName + "_" + DateTime.Now.ToString("yyyyMMddHHmm") + ".txt";
            string folderPath = Path.Combine(LogWriter.ContentRoot, "temp");
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            string fullPath = Path.Combine(folderPath, fileName);
            File.WriteAllText(fullPath, key ?? string.Empty);
            return fileName;
        }

        public static string GetTempFilePath(string fileName) =>
            Path.Combine(LogWriter.ContentRoot, "temp", fileName);

        private static string EscapeDn(string value) =>
            (value ?? string.Empty).Replace("\\", "\\\\").Replace(",", "\\,");

        private static string ToPem(string label, byte[] data)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"-----BEGIN {label}-----");
            sb.AppendLine(Convert.ToBase64String(data, Base64FormattingOptions.InsertLineBreaks));
            sb.AppendLine($"-----END {label}-----");
            return sb.ToString();
        }
    }
}

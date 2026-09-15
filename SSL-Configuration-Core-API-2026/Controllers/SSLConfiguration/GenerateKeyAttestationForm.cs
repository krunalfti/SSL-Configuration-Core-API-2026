using Microsoft.AspNetCore.Http;

namespace SSL_Configuration_Core_API_2026.Controllers.SSLConfiguration
{
    /// <summary>
    /// Form model for GenerateKeyAttestation (Swagger-friendly [FromForm] + IFormFile).
    /// Same fields as old ComodoController.ModelGenerateKeyAttestation.
    /// </summary>
    public class GenerateKeyAttestationForm
    {
        public string? HSMType { get; set; }
        public IFormFile? AttestationP7B { get; set; }
        public IFormFile? AttestationCrt { get; set; }
        public IFormFile? IntermediateCrt { get; set; }
        public IFormFile? AttestationBin { get; set; }
    }
}

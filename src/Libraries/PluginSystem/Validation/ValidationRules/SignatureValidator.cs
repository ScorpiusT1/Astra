using Addins.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Addins.Validation.ValidationRules
{
    /// <summary>
    /// 数字签名验证器
    /// </summary>
    public class SignatureValidator : IValidationRule
    {
        private readonly bool _requireSignature;

        public SignatureValidator(bool requireSignature = false)
        {
            _requireSignature = requireSignature;
        }

        public async Task<ValidationResult> ValidateAsync(PluginDescriptor descriptor)
        {
            var result = new ValidationResult { IsValid = true };

            if (!_requireSignature)
                return result;

            var signatureFile = descriptor.AssemblyPath + ".sig";

            if (!File.Exists(signatureFile))
            {
                result.IsValid = false;
                result.Errors.Add($"Signature file not found: {signatureFile}");
                return result;
            }

            try
            {
                var isValid = await VerifySignatureAsync(descriptor.AssemblyPath, signatureFile);
                if (!isValid)
                {
                    result.IsValid = false;
                    result.Errors.Add("Invalid digital signature");
                }
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.Errors.Add($"Signature verification failed: {ex.Message}");
            }

            return result;
        }

        private async Task<bool> VerifySignatureAsync(string assemblyPath, string signatureFile)
        {
            // 简化的签名验证示例
            // 实际应用中应该使用 X.509 证书和 RSA 验证

            var assemblyHash = await ComputeFileHashAsync(assemblyPath);
            var storedHash = await File.ReadAllBytesAsync(signatureFile);

            return assemblyHash.Length == storedHash.Length &&
                   CompareBytes(assemblyHash, storedHash);
        }

        private async Task<byte[]> ComputeFileHashAsync(string filePath)
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            return await Task.Run(() => sha256.ComputeHash(stream));
        }

        private bool CompareBytes(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i]) return false;
            }
            return true;
        }
    }
}

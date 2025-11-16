namespace Astra.Core.Nodes.Models
{
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new List<string>();

        public static ValidationResult Success() => new ValidationResult { IsValid = true };
        public static ValidationResult Failure(params string[] errors) => new ValidationResult { IsValid = false, Errors = new List<string>(errors) };
    }
}

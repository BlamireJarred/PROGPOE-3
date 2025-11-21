using PROG6212_POE.Models;

namespace PROG6212_POE.Services
{
    public interface IClaimValidationService
    {
        ValidationResult ValidateClaim(Claim claim);
        bool IsEligibleForAutoApproval(Claim claim);
    }

    public class ClaimValidationService : IClaimValidationService
    {
        // Predefined validation criteria
        private const decimal MAX_HOURLY_RATE = 500.00m;
        private const int MAX_HOURS_PER_WEEK = 50;
        private const int MAX_HOURS_PER_CLAIM = 200;
        private const decimal MAX_TOTAL_AMOUNT = 16000.00m;

        public ValidationResult ValidateClaim(Claim claim)
        {
            var result = new ValidationResult { IsValid = true };

            // Check hourly rate against policy
            if (claim.HourlyRate > MAX_HOURLY_RATE)
            {
                result.IsValid = false;
                result.Errors.Add($"Hourly rate (R{claim.HourlyRate}) exceeds maximum allowed rate (R{MAX_HOURLY_RATE}).");
            }

            // Check hours worked per claim
            if (claim.HoursWorked > MAX_HOURS_PER_CLAIM)
            {
                result.IsValid = false;
                result.Errors.Add($"Hours worked ({claim.HoursWorked}) exceeds maximum allowed per claim ({MAX_HOURS_PER_CLAIM}).");
            }

            // Check if hours exceed reasonable weekly limit (assuming 5 weeks)
            if (claim.HoursWorked > (MAX_HOURS_PER_WEEK * 5))
            {
                result.Warnings.Add($"Hours worked ({claim.HoursWorked}) exceeds typical 5-week maximum ({MAX_HOURS_PER_WEEK * 5} hours).");
            }

            // Check total amount
            if (claim.TotalAmount > MAX_TOTAL_AMOUNT)
            {
                result.IsValid = false;
                result.Errors.Add($"Total amount (R{claim.TotalAmount}) exceeds maximum allowed (R{MAX_TOTAL_AMOUNT}).");
            }

            // Check for missing supporting document on high-value claims
            if (claim.TotalAmount > 5000 && string.IsNullOrEmpty(claim.SupportingDocumentPath))
            {
                result.Warnings.Add("High-value claim (over R5000) submitted without supporting document.");
            }

            return result;
        }

        public bool IsEligibleForAutoApproval(Claim claim)
        {
            // Auto-approve small claims that meet all criteria
            return claim.TotalAmount <= 3000.00m &&
                   claim.HourlyRate <= 150.00m &&
                   claim.HoursWorked <= 20 &&
                   !string.IsNullOrEmpty(claim.SupportingDocumentPath);
        }
    }

    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
        public List<string> Recommendations { get; set; } = new List<string>();
    }
}
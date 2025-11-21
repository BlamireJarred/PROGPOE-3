// Services/ApprovalWorkflowService.cs
using PROG6212_POE.Models;

namespace PROG6212_POE.Services
{
    public interface IApprovalWorkflowService
    {
        ApprovalWorkflowResult ProcessClaim(Claim claim, ValidationResult validation);
        string GetNextApprovalStep(Claim claim);
        bool RequiresManagerAttention(Claim claim);
        bool RequiresCoordinatorAttention(Claim claim);
    }

    public class ApprovalWorkflowService : IApprovalWorkflowService
    {
        private readonly IClaimValidationService _validationService;

        public ApprovalWorkflowService(IClaimValidationService validationService)
        {
            _validationService = validationService;
        }

        public ApprovalWorkflowResult ProcessClaim(Claim claim, ValidationResult validation)
        {
            var result = new ApprovalWorkflowResult();

            // Auto-reject claims that fail validation
            if (!validation.IsValid)
            {
                result.Action = WorkflowAction.Reject;
                result.Message = "Claim failed automated validation checks.";
                result.Reasons.AddRange(validation.Errors);
                return result;
            }

            // Auto-approve small, compliant claims
            if (_validationService.IsEligibleForAutoApproval(claim))
            {
                result.Action = WorkflowAction.AutoApprove;
                result.Message = "Claim eligible for auto-approval based on predefined criteria.";
                return result;
            }

            // Route to appropriate approver based on amount
            if (claim.TotalAmount <= 5000.00m)
            {
                result.Action = WorkflowAction.RouteToManager;
                result.Message = "Claim requires manager approval.";
            }
            else
            {
                result.Action = WorkflowAction.RouteToCoordinator;
                result.Message = "High-value claim requires coordinator approval.";
            }

            // Add warnings and recommendations
            result.Warnings.AddRange(validation.Warnings);
            result.Recommendations.AddRange(validation.Recommendations);

            return result;
        }

        public string GetNextApprovalStep(Claim claim)
        {
            if (claim.Status == "Rejected") return "Rejected";
            if (claim.ManagerApproved && claim.CoordinatorApproved) return "Approved";
            if (claim.ManagerApproved) return "Awaiting Coordinator Approval";
            if (claim.CoordinatorApproved) return "Awaiting Manager Approval";

            // Determine initial routing
            return claim.TotalAmount <= 5000.00m ? "Awaiting Manager Approval" : "Awaiting Coordinator Approval";
        }

        public bool RequiresManagerAttention(Claim claim)
        {
            return !claim.ManagerApproved &&
                   !claim.CoordinatorApproved &&
                   claim.Status != "Rejected" &&
                   claim.TotalAmount <= 5000.00m;
        }

        public bool RequiresCoordinatorAttention(Claim claim)
        {
            return (!claim.CoordinatorApproved && claim.ManagerApproved) ||
                   (claim.TotalAmount > 5000.00m && !claim.ManagerApproved && !claim.CoordinatorApproved);
        }
    }

    public class ApprovalWorkflowResult
    {
        public WorkflowAction Action { get; set; }
        public string Message { get; set; }
        public List<string> Reasons { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
        public List<string> Recommendations { get; set; } = new List<string>();
    }

    public enum WorkflowAction
    {
        AutoApprove,
        RouteToManager,
        RouteToCoordinator,
        Reject,
        RequireManualReview
    }
}
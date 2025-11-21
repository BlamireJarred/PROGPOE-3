namespace PROG6212_POE.Models
{
    public class Claim
    {
        public int Id { get; set; }
        public string LecturerName { get; set; }
        public int? LecturerId { get; set; }
        public string ContractName { get; set; }
        public int HoursWorked { get; set; }
        public decimal HourlyRate { get; set; }
        public string SupportingDocumentName { get; set; }
        public string SupportingDocumentPath { get; set; }
        public string Status { get; set; } = "Pending";
        public bool ManagerApproved { get; set; } = false;
        public bool CoordinatorApproved { get; set; } = false;
        public DateTime SubmittedDate { get; set; } = DateTime.Now;
        public DateTime? LastUpdatedDate { get; set; }

        // Automated validation properties
        public bool AutoValidated { get; set; } = false;
        public string ValidationNotes { get; set; }
        public string WorkflowStatus { get; set; } = "New";

        // Calculated properties
        public decimal TotalAmount => HoursWorked * HourlyRate;

        public string ApprovalStatus
        {
            get
            {
                if (Status == "Rejected") return "Rejected";
                if (ManagerApproved && CoordinatorApproved) return "Approved";
                if (ManagerApproved) return "Awaiting Coordinator";
                if (CoordinatorApproved) return "Awaiting Manager";
                return WorkflowStatus ?? "Pending Review";
            }
        }

        public string FormattedTotalAmount => TotalAmount.ToString("C");
        public string FormattedHourlyRate => HourlyRate.ToString("C");
        public int DaysSinceSubmission => (DateTime.Now - SubmittedDate).Days;
    }
}
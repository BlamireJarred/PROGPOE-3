using PROG6212_POE.Models;
using System.Linq;

namespace PROG6212_POE.Services
{
    public interface IHRService
    {
        ReportResult GeneratePaymentReport(DateTime startDate, DateTime endDate, List<Claim> claims);
        ReportResult GenerateClaimsSummaryReport(string status, List<Claim> claims);
        decimal CalculateTotalPaymentsForPeriod(DateTime startDate, DateTime endDate, List<Claim> claims);
        List<Claim> GetApprovedClaimsForPayment(List<Claim> claims);
        List<Lecturer> GetAllLecturers(List<Lecturer> lecturers);
        Lecturer GetLecturerById(int id, List<Lecturer> lecturers);
        void UpdateLecturer(Lecturer lecturer, List<Lecturer> lecturers);
        void AddLecturer(Lecturer lecturer, List<Lecturer> lecturers);
    }

    public class HRService : IHRService
    {
        public HRService()
        {
            // No constructor parameters needed now
        }

        public ReportResult GeneratePaymentReport(DateTime startDate, DateTime endDate, List<Claim> claims)
        {
            var approvedClaims = claims
                .Where(c => c.Status == "Approved" &&
                           c.SubmittedDate >= startDate &&
                           c.SubmittedDate <= endDate)
                .ToList();

            var result = new ReportResult
            {
                Title = $"Payment Report - {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}",
                GeneratedDate = DateTime.Now
            };

            foreach (var claim in approvedClaims)
            {
                result.Data.Add(new ReportData
                {
                    LecturerName = claim.LecturerName,
                    ContractName = claim.ContractName,
                    HoursWorked = claim.HoursWorked,
                    HourlyRate = claim.HourlyRate,
                    TotalAmount = claim.TotalAmount,
                    SubmittedDate = claim.SubmittedDate,
                    ApprovedDate = claim.LastUpdatedDate ?? claim.SubmittedDate
                });
            }

            result.Summary.TotalAmount = approvedClaims.Sum(c => c.TotalAmount);
            result.Summary.TotalClaims = approvedClaims.Count;
            result.Summary.TotalHours = approvedClaims.Sum(c => c.HoursWorked);

            return result;
        }

        public ReportResult GenerateClaimsSummaryReport(string status, List<Claim> claims)
        {
            var filteredClaims = string.IsNullOrEmpty(status)
                ? claims
                : claims.Where(c => c.Status == status).ToList();

            var result = new ReportResult
            {
                Title = $"Claims Summary Report - {status ?? "All Statuses"}",
                GeneratedDate = DateTime.Now
            };

            var statusGroups = filteredClaims.GroupBy(c => c.Status);

            foreach (var group in statusGroups)
            {
                result.SummaryData.Add(new SummaryData
                {
                    Category = group.Key,
                    Count = group.Count(),
                    TotalAmount = group.Sum(c => c.TotalAmount)
                });
            }

            result.Summary.TotalAmount = filteredClaims.Sum(c => c.TotalAmount);
            result.Summary.TotalClaims = filteredClaims.Count;

            return result;
        }

        public decimal CalculateTotalPaymentsForPeriod(DateTime startDate, DateTime endDate, List<Claim> claims)
        {
            return claims
                .Where(c => c.Status == "Approved" &&
                           c.SubmittedDate >= startDate &&
                           c.SubmittedDate <= endDate)
                .Sum(c => c.TotalAmount);
        }

        public List<Claim> GetApprovedClaimsForPayment(List<Claim> claims)
        {
            return claims.Where(c => c.Status == "Approved").ToList();
        }

        public List<Lecturer> GetAllLecturers(List<Lecturer> lecturers)
        {
            return lecturers.Where(l => l.IsActive).ToList();
        }

        public Lecturer GetLecturerById(int id, List<Lecturer> lecturers)
        {
            return lecturers.FirstOrDefault(l => l.Id == id);
        }

        public void UpdateLecturer(Lecturer lecturer, List<Lecturer> lecturers)
        {
            var existingLecturer = lecturers.FirstOrDefault(l => l.Id == lecturer.Id);
            if (existingLecturer != null)
            {
                existingLecturer.FirstName = lecturer.FirstName;
                existingLecturer.LastName = lecturer.LastName;
                existingLecturer.Email = lecturer.Email;
                existingLecturer.LastUpdatedDate = DateTime.Now;
            }
        }

        public void AddLecturer(Lecturer lecturer, List<Lecturer> lecturers)
        {
            lecturer.Id = lecturers.Any() ? lecturers.Max(l => l.Id) + 1 : 1;
            lecturer.CreatedDate = DateTime.Now;
            lecturers.Add(lecturer);
        }
    }

    public class ReportResult
    {
        public string Title { get; set; }
        public DateTime GeneratedDate { get; set; }
        public List<ReportData> Data { get; set; } = new List<ReportData>();
        public List<SummaryData> SummaryData { get; set; } = new List<SummaryData>();
        public ReportSummary Summary { get; set; } = new ReportSummary();
    }

    public class ReportData
    {
        public string LecturerName { get; set; }
        public string ContractName { get; set; }
        public int HoursWorked { get; set; }
        public decimal HourlyRate { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime SubmittedDate { get; set; }
        public DateTime ApprovedDate { get; set; }
    }

    public class SummaryData
    {
        public string Category { get; set; }
        public int Count { get; set; }
        public decimal TotalAmount { get; set; }
    }

    public class ReportSummary
    {
        public int TotalClaims { get; set; }
        public decimal TotalAmount { get; set; }
        public int TotalHours { get; set; }
    }
}
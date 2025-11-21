using Microsoft.AspNetCore.Mvc;
using PROG6212_POE.Models;
using PROG6212_POE.Services;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using System.IO;
using Microsoft.AspNetCore.Mvc.Filters;

namespace PROG6212_POE.Controllers
{
    public class ValidateFileAttribute : ActionFilterAttribute
    {
        private readonly long _maxFileSize;
        private readonly string[] _allowedExtensions;

        public ValidateFileAttribute(long maxFileSize, string allowedExtensions)
        {
            _maxFileSize = maxFileSize;
            _allowedExtensions = allowedExtensions.Split(',').Select(ext => ext.Trim().ToLower()).ToArray();
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (context.ActionArguments.TryGetValue("SupportingDocument", out var fileObj) && fileObj is IFormFile file)
            {
                if (file.Length > _maxFileSize)
                {
                    context.ModelState.AddModelError("SupportingDocument", $"File size exceeds {_maxFileSize / (1024 * 1024)} MB limit.");
                    return;
                }

                var fileExtension = Path.GetExtension(file.FileName).ToLower();
                if (!_allowedExtensions.Contains(fileExtension))
                {
                    context.ModelState.AddModelError("SupportingDocument", $"Invalid file type. Allowed types: {string.Join(", ", _allowedExtensions)}");
                    return;
                }
            }

            base.OnActionExecuting(context);
        }
    }

    public class HomeController : Controller
    {
        private static List<Claim> _claims = new List<Claim>();
        private static List<Lecturer> _lecturers = new List<Lecturer>();
        private static int _nextId = 1;
        private readonly IClaimValidationService _validationService;
        private readonly IApprovalWorkflowService _workflowService;
        private readonly IHRService _hrService;

        private const decimal MIN_HOURLY_RATE = 15.00m;
        private const decimal MAX_HOURLY_RATE = 500.00m;
        private const long MAX_FILE_SIZE = 5 * 1024 * 1024;
        private static readonly string[] ALLOWED_FILE_TYPES = { ".pdf", ".docx", ".xlsx", ".jpg", ".png" };

        public HomeController(IClaimValidationService validationService,
                            IApprovalWorkflowService workflowService,
                            IHRService hrService)
        {
            _validationService = validationService;
            _workflowService = workflowService;
            _hrService = hrService;
            InitializeSampleLecturers();
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult LecturerDashboard()
        {
            return View(_claims);
        }

        public IActionResult ManagerDashboard()
        {
            var claimsNeedingAttention = _claims.Where(_workflowService.RequiresManagerAttention).ToList();
            return View(claimsNeedingAttention);
        }

        public IActionResult CoordinatorDashboard()
        {
            var claimsNeedingAttention = _claims.Where(_workflowService.RequiresCoordinatorAttention).ToList();
            return View(claimsNeedingAttention);
        }

        public IActionResult HRDashboard()
        {
            var dashboardData = new HRDashboardViewModel
            {
                TotalLecturers = _lecturers.Count(l => l.IsActive),
                PendingClaims = _claims.Count(c => c.Status == "Pending"),
                ApprovedClaims = _claims.Count(c => c.Status == "Approved"),
                TotalPaymentsThisMonth = _hrService.CalculateTotalPaymentsForPeriod(
                    new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1),
                    DateTime.Now,
                    _claims
                ),
                RecentClaims = _claims.OrderByDescending(c => c.SubmittedDate).Take(10).ToList()
            };

            return View(dashboardData);
        }

        [HttpPost]
        [ValidateFile(MAX_FILE_SIZE, ".pdf,.docx,.xlsx,.jpg,.png")]
        public IActionResult SubmitClaim(string LecturerName, string ContractName, int HoursWorked, decimal HourlyRate, IFormFile SupportingDocument)
        {
            if (string.IsNullOrEmpty(LecturerName) || string.IsNullOrEmpty(ContractName))
            {
                TempData["ErrorMessage"] = "Lecturer name and contract name are required.";
                return RedirectToAction("LecturerDashboard");
            }

            if (HourlyRate < MIN_HOURLY_RATE || HourlyRate > MAX_HOURLY_RATE)
            {
                TempData["ErrorMessage"] = $"Hourly rate must be between {MIN_HOURLY_RATE:C} and {MAX_HOURLY_RATE:C}.";
                return RedirectToAction("LecturerDashboard");
            }

            if (HoursWorked <= 0 || HoursWorked > 200)
            {
                TempData["ErrorMessage"] = "Hours worked must be between 1 and 200.";
                return RedirectToAction("LecturerDashboard");
            }

            if (!ModelState.IsValid)
            {
                var fileError = ModelState["SupportingDocument"]?.Errors.FirstOrDefault()?.ErrorMessage;
                if (!string.IsNullOrEmpty(fileError))
                {
                    TempData["ErrorMessage"] = fileError;
                }
                return RedirectToAction("LecturerDashboard");
            }

            var claim = new Claim
            {
                Id = _nextId++,
                LecturerName = LecturerName,
                ContractName = ContractName,
                HoursWorked = HoursWorked,
                HourlyRate = HourlyRate,
                Status = "Pending",
                ManagerApproved = false,
                CoordinatorApproved = false,
                SubmittedDate = DateTime.Now
            };

            var lecturer = _lecturers.FirstOrDefault(l =>
                l.FullName.Equals(LecturerName, StringComparison.OrdinalIgnoreCase) &&
                l.IsActive);

            if (lecturer != null)
            {
                claim.LecturerId = lecturer.Id;
            }

            if (SupportingDocument != null && SupportingDocument.Length > 0)
            {
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                var fileName = $"{Path.GetFileNameWithoutExtension(SupportingDocument.FileName)}_{DateTime.Now:yyyyMMddHHmmss}{Path.GetExtension(SupportingDocument.FileName)}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    SupportingDocument.CopyTo(stream);
                }

                claim.SupportingDocumentName = SupportingDocument.FileName;
                claim.SupportingDocumentPath = "/uploads/" + fileName;
            }

            var validationResult = _validationService.ValidateClaim(claim);
            var workflowResult = _workflowService.ProcessClaim(claim, validationResult);

            claim.AutoValidated = true;
            claim.ValidationNotes = string.Join("; ", validationResult.Errors.Concat(validationResult.Warnings));
            claim.WorkflowStatus = workflowResult.Action.ToString();

            switch (workflowResult.Action)
            {
                case WorkflowAction.AutoApprove:
                    claim.ManagerApproved = true;
                    claim.CoordinatorApproved = true;
                    claim.Status = "Approved";
                    TempData["SuccessMessage"] = "Claim automatically approved!";
                    break;

                case WorkflowAction.Reject:
                    claim.Status = "Rejected";
                    TempData["ErrorMessage"] = $"Claim automatically rejected: {workflowResult.Message}";
                    break;

                case WorkflowAction.RouteToManager:
                    claim.WorkflowStatus = "Awaiting Manager Review";
                    TempData["SuccessMessage"] = "Claim submitted and routed to Manager for approval.";
                    break;

                case WorkflowAction.RouteToCoordinator:
                    claim.WorkflowStatus = "Awaiting Coordinator Review";
                    TempData["SuccessMessage"] = "Claim submitted and routed to Coordinator for approval.";
                    break;

                default:
                    TempData["SuccessMessage"] = "Claim submitted successfully!";
                    break;
            }

            _claims.Add(claim);
            return RedirectToAction("LecturerDashboard");
        }

        [HttpPost]
        public IActionResult ManagerApprove(int id)
        {
            var claim = _claims.FirstOrDefault(c => c.Id == id);
            if (claim != null)
            {
                claim.ManagerApproved = true;
                claim.LastUpdatedDate = DateTime.Now;

                if (claim.ManagerApproved && claim.CoordinatorApproved)
                {
                    claim.Status = "Approved";
                    claim.WorkflowStatus = "Fully Approved";
                }
                else
                {
                    claim.WorkflowStatus = _workflowService.GetNextApprovalStep(claim);
                }
            }
            return RedirectToAction("ManagerDashboard");
        }

        [HttpPost]
        public IActionResult CoordinatorApprove(int id)
        {
            var claim = _claims.FirstOrDefault(c => c.Id == id);
            if (claim != null)
            {
                claim.CoordinatorApproved = true;
                claim.LastUpdatedDate = DateTime.Now;

                if (claim.ManagerApproved && claim.CoordinatorApproved)
                {
                    claim.Status = "Approved";
                    claim.WorkflowStatus = "Fully Approved";
                }
                else
                {
                    claim.WorkflowStatus = _workflowService.GetNextApprovalStep(claim);
                }
            }
            return RedirectToAction("CoordinatorDashboard");
        }

        [HttpPost]
        public IActionResult RejectClaim(int id)
        {
            var claim = _claims.FirstOrDefault(c => c.Id == id);
            if (claim != null)
            {
                claim.Status = "Rejected";
                claim.ManagerApproved = false;
                claim.CoordinatorApproved = false;
                claim.LastUpdatedDate = DateTime.Now;
                claim.WorkflowStatus = "Rejected";
            }
            return RedirectToAction("ManagerDashboard");
        }

        [HttpPost]
        public IActionResult CoordinatorReject(int id)
        {
            var claim = _claims.FirstOrDefault(c => c.Id == id);
            if (claim != null)
            {
                claim.Status = "Rejected";
                claim.ManagerApproved = false;
                claim.CoordinatorApproved = false;
                claim.LastUpdatedDate = DateTime.Now;
                claim.WorkflowStatus = "Rejected";
            }
            return RedirectToAction("CoordinatorDashboard");
        }

        public IActionResult LecturerManagement()
        {
            return View(_lecturers.Where(l => l.IsActive).ToList());
        }

        public IActionResult AddLecturer()
        {
            return View();
        }

        [HttpPost]
        public IActionResult AddLecturer(Lecturer lecturer)
        {
            if (ModelState.IsValid)
            {
                lecturer.Id = _lecturers.Any() ? _lecturers.Max(l => l.Id) + 1 : 1;
                lecturer.CreatedDate = DateTime.Now;
                _lecturers.Add(lecturer);
                TempData["SuccessMessage"] = "Lecturer added successfully!";
                return RedirectToAction("LecturerManagement");
            }
            return View(lecturer);
        }

        public IActionResult EditLecturer(int id)
        {
            var lecturer = _lecturers.FirstOrDefault(l => l.Id == id);
            if (lecturer == null)
            {
                return NotFound();
            }
            return View(lecturer);
        }

        [HttpPost]
        public IActionResult EditLecturer(Lecturer lecturer)
        {
            if (ModelState.IsValid)
            {
                var existingLecturer = _lecturers.FirstOrDefault(l => l.Id == lecturer.Id);
                if (existingLecturer != null)
                {
                    existingLecturer.FirstName = lecturer.FirstName;
                    existingLecturer.LastName = lecturer.LastName;
                    existingLecturer.Email = lecturer.Email;
                    existingLecturer.LastUpdatedDate = DateTime.Now;
                }
                TempData["SuccessMessage"] = "Lecturer updated successfully!";
                return RedirectToAction("LecturerManagement");
            }
            return View(lecturer);
        }

        public IActionResult LecturerDetails(int id)
        {
            var lecturer = _lecturers.FirstOrDefault(l => l.Id == id);
            if (lecturer == null)
            {
                return NotFound();
            }

            var lecturerClaims = _claims
                .Where(c => c.LecturerName.Equals(lecturer.FullName, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(c => c.SubmittedDate)
                .ToList();

            ViewBag.LecturerClaims = lecturerClaims;
            return View(lecturer);
        }

        [HttpPost]
        public IActionResult DeleteLecturer(int id)
        {
            var lecturer = _lecturers.FirstOrDefault(l => l.Id == id);
            if (lecturer != null)
            {
                lecturer.IsActive = false;
                lecturer.LastUpdatedDate = DateTime.Now;
                TempData["SuccessMessage"] = "Lecturer deactivated successfully!";
            }
            return RedirectToAction("LecturerManagement");
        }

        public IActionResult GenerateReports()
        {
            var model = new ReportViewModel
            {
                StartDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1),
                EndDate = DateTime.Now
            };

            ViewBag.TotalClaims = _claims.Count;
            ViewBag.PendingClaims = _claims.Count(c => c.Status == "Pending");
            ViewBag.ApprovedClaims = _claims.Count(c => c.Status == "Approved");
            ViewBag.RejectedClaims = _claims.Count(c => c.Status == "Rejected");

            return View(model);
        }

        [HttpPost]
        public IActionResult GeneratePaymentReport(DateTime startDate, DateTime endDate)
        {
            var report = _hrService.GeneratePaymentReport(startDate, endDate, _claims);
            return View("PaymentReport", report);
        }

        [HttpPost]
        public IActionResult GenerateClaimsSummaryReport(string status)
        {
            var report = _hrService.GenerateClaimsSummaryReport(status, _claims);
            return View("ClaimsSummaryReport", report);
        }

        private void InitializeSampleLecturers()
        {
            if (!_lecturers.Any())
            {
                _lecturers.Add(new Lecturer
                {
                    Id = 1,
                    FirstName = "Jarred",
                    LastName = "St104",
                    Email = "Jarred@uni.co.za",
                    IsActive = true
                });

                _lecturers.Add(new Lecturer
                {
                    Id = 2,
                    FirstName = "Ethan",
                    LastName = "Surname",
                    Email = "E101010@university.co.za",
                    IsActive = true
                });

                _lecturers.Add(new Lecturer
                {
                    Id = 3,
                    FirstName = "lec",
                    LastName = "Turer",
                    Email = "LEC@university.co.za",
                    IsActive = true
                });
            }
        }

        public class HRDashboardViewModel
        {
            public int TotalLecturers { get; set; }
            public int PendingClaims { get; set; }
            public int ApprovedClaims { get; set; }
            public decimal TotalPaymentsThisMonth { get; set; }
            public List<Claim> RecentClaims { get; set; }
        }

        public class ReportViewModel
        {
            public DateTime StartDate { get; set; }
            public DateTime EndDate { get; set; }
        }
    }
}
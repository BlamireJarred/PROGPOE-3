using Xunit;
using PROG6212_POE.Models;

namespace ClaimTest
{
    public class ClaimTest
    {
        [Fact]
        public void CalculatedTotalAmount()
        {
            var claim = new Claim();

            claim.HoursWorked = 20;
            claim.HourlyRate = 670;

            var getResult = claim.TotalAmount;

            Assert.Equal(13400, getResult);
        }


        [Fact]
        public void Notes_Simulation()
        {
            var claim = new Claim();
            claim.Notes = "This is a test note for the claim.";

            var notes = claim.Notes;

            Assert.Equal("This is a test note for the claim.", notes);
        }

        [Fact]
        public void FileProperties_StoredCorrectly()
        {
            var claim = new Claim();

            claim.SupportingDocumentName = "test.pdf";
            claim.SupportingDocumentPath = "/uploads/test.pdf";

            Assert.Equal("test.pdf", claim.SupportingDocumentName);

        }

        [Fact]
        public void DefaultStatus_ShouldBePending()
        {
            var claim = new Claim();

            Assert.Equal("Pending", claim.Status);
        }

        [Fact]
        public void ClaimProperties_AssignedCorrectly()
        {
            var claim = new Claim
            {
                Id = 1,
                LecturerName = "John Doe",
                HoursWorked = 10,
                HourlyRate = 500,
                Notes = "Lecturer worked extra hours",
                Status = "Pending"
            };

            Assert.Equal(1, claim.Id);
            Assert.Equal("John Doe", claim.LecturerName);
            Assert.Equal(10, claim.HoursWorked);
            Assert.Equal(500, claim.HourlyRate);
            Assert.Equal("Lecturer worked extra hours", claim.Notes);
            Assert.Equal("Pending", claim.Status);
        }

        [Fact]
        public void TotalAmount_Updates_WhenHoursOrRateChange()
        {
            var claim = new Claim
            {
                HoursWorked = 5,
                HourlyRate = 1000
            };

            Assert.Equal(5000, claim.TotalAmount);

            claim.HoursWorked = 10;
            Assert.Equal(10000, claim.TotalAmount);

            claim.HourlyRate = 1200;
            Assert.Equal(12000, claim.TotalAmount);
        }


    }
}
using FluentAssertions;
using GLMS_CORE_APP.Shared.Models;
using GLMS_CORE_APP.Shared.Models.Enums;
using GLMS_CORE_APP.Tests.Helpers;
using GLMS_CORE_APP.ViewModels;
using System.ComponentModel.DataAnnotations;

namespace GLMS_CORE_APP.Tests.SLA;

public class SLAModelTests : IDisposable
{
    private readonly GLMS_CORE_APP.Shared.Data.GlmsDbContext _db;

    public SLAModelTests() => _db = DbContextFactory.Create();
    public void Dispose() => _db.Dispose();

    private static IList<ValidationResult> Validate(object model)
    {
        var ctx     = new ValidationContext(model);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, ctx, results, validateAllProperties: true);
        return results;
    }

    private async Task<Guid> SeedContractAsync()
    {
        var client = new Client
        {
            Name = "Client", ContactEmail = $"{Guid.NewGuid():N}@test.com",
            CountryCode = "ZA", PreferredCurrency = "ZAR"
        };
        _db.Clients.Add(client);
        var contract = new Contract
        {
            ClientId = client.ClientId, Title = "Contract",
            StartDate = DateTime.Today, EndDate = DateTime.Today.AddYears(1),
            Value = 100m, BaseCurrency = "ZAR", Status = ContractStatus.Active
        };
        _db.Contracts.Add(contract);
        await _db.SaveChangesAsync();
        return contract.ContractId;
    }

    [Fact]
    public async Task SLA_CanBeSavedAndRetrieved()
    {
        var cId = await SeedContractAsync();
        var sla = new GLMS_CORE_APP.Shared.Models.SLA
        {
            ContractId = cId, MetricName = "Shipment Accuracy",
            TargetValue = 99.5m, Unit = "%", ReviewDate = DateTime.Today.AddMonths(3)
        };
        _db.SLAs.Add(sla);
        await _db.SaveChangesAsync();
        var retrieved = await _db.SLAs.FindAsync(sla.SLAId);
        retrieved!.MetricName.Should().Be("Shipment Accuracy");
    }

    [Fact]
    public async Task SLA_DefaultsToStatusPending()
    {
        var cId = await SeedContractAsync();
        var sla = new GLMS_CORE_APP.Shared.Models.SLA
        {
            ContractId = cId, MetricName = "Test",
            TargetValue = 1m, Unit = "%", ReviewDate = DateTime.Today.AddMonths(1)
        };
        _db.SLAs.Add(sla);
        await _db.SaveChangesAsync();
        (await _db.SLAs.FindAsync(sla.SLAId))!.Status.Should().Be(SLAStatus.Pending);
    }

    [Fact]
    public async Task SLA_DefaultsToReviewWarningDays_14()
    {
        var cId = await SeedContractAsync();
        var sla = new GLMS_CORE_APP.Shared.Models.SLA
        {
            ContractId = cId, MetricName = "Test",
            TargetValue = 1m, Unit = "%", ReviewDate = DateTime.Today.AddMonths(1)
        };
        _db.SLAs.Add(sla);
        await _db.SaveChangesAsync();
        (await _db.SLAs.FindAsync(sla.SLAId))!.ReviewWarningDays.Should().Be(14);
    }

    [Fact]
    public async Task SLA_CanBeMarkedAsBreached()
    {
        var cId = await SeedContractAsync();
        var sla = new GLMS_CORE_APP.Shared.Models.SLA
        {
            ContractId = cId, MetricName = "Test",
            TargetValue = 99m, Unit = "%",
            ReviewDate = DateTime.Today.AddMonths(1), Status = SLAStatus.Active
        };
        _db.SLAs.Add(sla);
        await _db.SaveChangesAsync();

        sla.Status      = SLAStatus.Breached;
        sla.BreachedAt  = DateTime.UtcNow;
        sla.BreachNotes = "Missed Q3";
        await _db.SaveChangesAsync();

        var retrieved = await _db.SLAs.FindAsync(sla.SLAId);
        retrieved!.Status.Should().Be(SLAStatus.Breached);
        retrieved.BreachedAt.Should().NotBeNull();
    }

    [Fact]
    public void SLAViewModel_IsValid_WithCorrectData()
    {
        var model = new SLAViewModel
        {
            ContractId = Guid.NewGuid(), MetricName = "On-Time Delivery",
            Unit = "%", TargetValue = 99.5m, ReviewDate = DateTime.Today.AddMonths(3)
        };
        Validate(model).Should().BeEmpty();
    }

    [Fact]
    public void SLAViewModel_FailsValidation_WhenMetricNameEmpty()
    {
        var model = new SLAViewModel
        {
            ContractId = Guid.NewGuid(), MetricName = "",
            Unit = "%", TargetValue = 99m, ReviewDate = DateTime.Today.AddMonths(1)
        };
        Validate(model).Should().Contain(e => e.MemberNames.Contains("MetricName"));
    }

    [Fact]
    public void SLAViewModel_FailsValidation_WhenTargetValueZero()
    {
        var model = new SLAViewModel
        {
            ContractId = Guid.NewGuid(), MetricName = "Test",
            Unit = "%", TargetValue = 0m, ReviewDate = DateTime.Today.AddMonths(1)
        };
        Validate(model).Should().Contain(e => e.MemberNames.Contains("TargetValue"));
    }

    [Fact]
    public void SLAViewModel_FailsValidation_WhenUnitEmpty()
    {
        var model = new SLAViewModel
        {
            ContractId = Guid.NewGuid(), MetricName = "Test",
            Unit = "", TargetValue = 99m, ReviewDate = DateTime.Today.AddMonths(1)
        };
        Validate(model).Should().Contain(e => e.MemberNames.Contains("Unit"));
    }

    [Fact]
    public void SLAViewModel_FailsValidation_WhenReviewWarningDaysTooHigh()
    {
        var model = new SLAViewModel
        {
            ContractId = Guid.NewGuid(), MetricName = "Test",
            Unit = "%", TargetValue = 99m,
            ReviewDate = DateTime.Today.AddMonths(1), ReviewWarningDays = 400
        };
        Validate(model).Should().Contain(e => e.MemberNames.Contains("ReviewWarningDays"));
    }
}

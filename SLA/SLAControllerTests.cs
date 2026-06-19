using FluentAssertions;
using GLMS_CORE_APP.API.Controllers;
using GLMS_CORE_APP.API.DTOs;
using GLMS_CORE_APP.Controllers;
using GLMS_CORE_APP.Shared.Data;
using GLMS_CORE_APP.Shared.Models;
using GLMS_CORE_APP.Shared.Models.Enums;
using GLMS_CORE_APP.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace GLMS_CORE_APP.Tests.SLA;

/// <summary>
/// Unit tests for the API SLAsController.
/// Tests CRUD operations and the business rule:
/// SLAs can only be created against Active contracts.
/// </summary>
public class SLAControllerTests : IDisposable
{
    private readonly GlmsDbContext _db;
    private readonly SLAsController _controller;

    public SLAControllerTests()
    {
        _db = DbContextFactory.Create();
        _controller = new SLAsController(_db, NullLogger<SLAsController>.Instance);
    }

    public void Dispose() => _db.Dispose();

    // ── Seed helpers ───────────────────────────────────────────────────────

    private async Task<Client> SeedClientAsync()
    {
        var client = new Client
        {
            Name = "Test Client",
            ContactEmail = $"client{Guid.NewGuid():N}@test.com",
            CountryCode = "ZA",
            PreferredCurrency = "ZAR"
        };
        _db.Clients.Add(client);
        await _db.SaveChangesAsync();
        return client;
    }

    private async Task<Contract> SeedContractAsync(
        ContractStatus status = ContractStatus.Active)
    {
        var client = await SeedClientAsync();
        var contract = new Contract
        {
            ClientId = client.ClientId,
            Title = "Test Contract",
            StartDate = DateTime.Today.AddMonths(-1),
            EndDate = DateTime.Today.AddYears(1),
            Value = 100_000m,
            BaseCurrency = "ZAR",
            Status = status
        };
        _db.Contracts.Add(contract);
        await _db.SaveChangesAsync();
        return contract;
    }

    private async Task<GLMS_CORE_APP.Shared.Models.SLA> SeedSLAAsync(
        Guid? contractId = null)
    {
        var cId = contractId ?? (await SeedContractAsync()).ContractId;
        var sla = new GLMS_CORE_APP.Shared.Models.SLA
        {
            ContractId = cId,
            MetricName = "On-Time Delivery Rate",
            TargetValue = 99.5m,
            Unit = "%",
            ReviewDate = DateTime.Today.AddMonths(3),
            Status = SLAStatus.Active
        };
        _db.SLAs.Add(sla);
        await _db.SaveChangesAsync();
        return sla;
    }

    // ── GetAll ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_ReturnsOk_WithSLAList()
    {
        await SeedSLAAsync();
        await SeedSLAAsync();

        var result = await _controller.GetAll(null, null, null);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var items = ok.Value.Should().BeAssignableTo<IEnumerable<SLADto>>().Subject;
        items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAll_ReturnsEmpty_WhenNoSLAs()
    {
        var result = await _controller.GetAll(null, null, null);
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeAssignableTo<IEnumerable<SLADto>>()
          .Which.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAll_FiltersByContractId()
    {
        var c1 = await SeedContractAsync();
        var c2 = await SeedContractAsync();
        await SeedSLAAsync(c1.ContractId);
        await SeedSLAAsync(c2.ContractId);

        var result = await _controller.GetAll(null, null, c1.ContractId);
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var items = ok.Value.Should().BeAssignableTo<IEnumerable<SLADto>>().Subject;
        items.Should().HaveCount(1);
        items.First().ContractId.Should().Be(c1.ContractId);
    }

    // ── GetById ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetById_ReturnsOk_WhenFound()
    {
        var sla = await SeedSLAAsync();
        var result = await _controller.GetById(sla.SLAId);
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetById_ReturnsNotFound_WhenMissing()
    {
        var result = await _controller.GetById(Guid.NewGuid());
        result.Should().BeOfType<NotFoundResult>();
    }

    // ── Create ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenContractNotActive()
    {
        var contract = await SeedContractAsync(ContractStatus.Expired);

        var result = await _controller.Create(new CreateSLADto(
            contract.ContractId, "Metric", null, 99m, "%",
            DateTime.Today.AddMonths(3)));

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Create_ReturnsCreated_WhenValid()
    {
        var contract = await SeedContractAsync(ContractStatus.Active);

        var result = await _controller.Create(new CreateSLADto(
            contract.ContractId, "Response Time", null, 4m, "hours",
            DateTime.Today.AddMonths(3)));

        result.Should().BeOfType<CreatedAtActionResult>();
        _db.SLAs.FirstOrDefault(s => s.MetricName == "Response Time")
           .Should().NotBeNull();
    }

    // ── Update ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_ReturnsOk_WhenValid()
    {
        var sla = await SeedSLAAsync();

        var result = await _controller.Update(sla.SLAId, new UpdateSLADto(
            "Updated Metric", null, 2m, "days",
            DateTime.Today.AddMonths(6), 14, SLAStatus.Active));

        result.Should().BeOfType<OkObjectResult>();
        (await _db.SLAs.FindAsync(sla.SLAId))!.MetricName.Should().Be("Updated Metric");
    }

    [Fact]
    public async Task Update_ReturnsNotFound_WhenMissing()
    {
        var result = await _controller.Update(Guid.NewGuid(), new UpdateSLADto(
            "X", null, 1m, "%", DateTime.Today, 14, SLAStatus.Active));
        result.Should().BeOfType<NotFoundResult>();
    }

    // ── Delete (soft) ──────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_ReturnsNoContent_AndSetsInactive()
    {
        var sla = await SeedSLAAsync();
        var result = await _controller.Delete(sla.SLAId);

        result.Should().BeOfType<NoContentResult>();
        (await _db.SLAs.FindAsync(sla.SLAId))!.Status.Should().Be(SLAStatus.Inactive);
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenMissing()
    {
        var result = await _controller.Delete(Guid.NewGuid());
        result.Should().BeOfType<NotFoundResult>();
    }
}
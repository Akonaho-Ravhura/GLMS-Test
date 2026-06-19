using FluentAssertions;
using GLMS_CORE_APP.Shared.Data;
using GLMS_CORE_APP.Shared.Models;
using GLMS_CORE_APP.API.Services;
using GLMS_CORE_APP.Tests.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace GLMS_CORE_APP.Tests.Currency;

public class CurrencyConversionServiceTests : IDisposable
{
    private readonly GlmsDbContext _db;
    private readonly CurrencyConversionService _service;

    public CurrencyConversionServiceTests()
    {
        _db     = DbContextFactory.Create();
        _service = new CurrencyConversionService(_db, BuildConfig(),
                       NullLogger<CurrencyConversionService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    private static IConfiguration BuildConfig(double stalenessHours = 2) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CurrencyApi:StalenessHours"] = stalenessHours.ToString()
            })
            .Build();

    private async Task SeedRateAsync(
        string from, string to, decimal rate,
        DateTime? fetchedAt = null, bool isSuccessful = true)
    {
        _db.CurrencyRates.Add(new CurrencyRate
        {
            FromCurrency = from,
            ToCurrency   = to,
            Rate         = rate,
            FetchedAt    = fetchedAt ?? DateTime.UtcNow,
            Source       = "Test",
            IsSuccessful = isSuccessful
        });
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task ConvertAsync_ReturnsSameAmount_WhenSameCurrency()
    {
        (await _service.ConvertAsync(1000m, "USD", "USD")).Should().Be(1000m);
    }

    [Fact]
    public async Task ConvertAsync_IsCaseInsensitive_ForSameCurrency()
    {
        (await _service.ConvertAsync(500m, "usd", "USD")).Should().Be(500m);
    }

    [Fact]
    public async Task ConvertAsync_ReturnsConvertedAmount_WhenDirectRateExists()
    {
        await SeedRateAsync("USD", "ZAR", 18.5m);
        (await _service.ConvertAsync(100m, "USD", "ZAR")).Should().Be(1850m);
    }

    [Fact]
    public async Task ConvertAsync_RoundsTo4DecimalPlaces()
    {
        await SeedRateAsync("USD", "EUR", 0.9213m);
        (await _service.ConvertAsync(100m, "USD", "EUR")).Should().Be(92.13m);
    }

    [Fact]
    public async Task GetRateAsync_ReturnsLatestRate_WhenMultipleExist()
    {
        await SeedRateAsync("USD", "GBP", 0.75m, DateTime.UtcNow.AddMinutes(-90));
        await SeedRateAsync("USD", "GBP", 0.78m, DateTime.UtcNow.AddMinutes(-10));
        (await _service.GetRateAsync("USD", "GBP")).Should().Be(0.78m);
    }

    [Fact]
    public async Task ConvertAsync_ReturnsNull_WhenRateIsStale()
    {
        await SeedRateAsync("USD", "ZAR", 18.5m, fetchedAt: DateTime.UtcNow.AddHours(-3));
        (await _service.ConvertAsync(100m, "USD", "ZAR")).Should().BeNull();
    }

    [Fact]
    public async Task ConvertAsync_UsesRate_WhenJustWithinStalenessThreshold()
    {
        await SeedRateAsync("USD", "ZAR", 18.5m, fetchedAt: DateTime.UtcNow.AddMinutes(-119));
        var result = await _service.ConvertAsync(100m, "USD", "ZAR");
        result.Should().NotBeNull();
        result.Should().Be(1850m);
    }

    [Fact]
    public async Task GetRateAsync_ReturnsNull_WhenNoRatesExist()
    {
        (await _service.GetRateAsync("USD", "ZAR")).Should().BeNull();
    }

    [Fact]
    public async Task ConvertAsync_IgnoresFailedRates()
    {
        await SeedRateAsync("USD", "ZAR", 0m, isSuccessful: false);
        (await _service.ConvertAsync(100m, "USD", "ZAR")).Should().BeNull();
    }

    [Fact]
    public async Task GetRateAsync_UsesCrossRateViaUsd_WhenDirectNotAvailable()
    {
        await SeedRateAsync("ZAR", "USD", 0.054m);
        await SeedRateAsync("USD", "EUR", 0.92m);
        var rate = await _service.GetRateAsync("ZAR", "EUR");
        rate.Should().NotBeNull();
        rate!.Value.Should().BeApproximately(0.04968m, 0.0001m);
    }

    [Fact]
    public async Task GetRateAsync_ReturnsNull_WhenCrossRateLegMissing()
    {
        await SeedRateAsync("ZAR", "USD", 0.054m);
        (await _service.GetRateAsync("ZAR", "EUR")).Should().BeNull();
    }

    [Fact]
    public async Task GetAllRatesAsync_ReturnsAllFreshRatesForBase()
    {
        await SeedRateAsync("USD", "ZAR", 18.5m);
        await SeedRateAsync("USD", "EUR", 0.92m);
        await SeedRateAsync("USD", "GBP", 0.78m);
        var rates = await _service.GetAllRatesAsync("USD");
        rates.Should().ContainKey("ZAR").WhoseValue.Should().Be(18.5m);
        rates.Should().ContainKey("EUR").WhoseValue.Should().Be(0.92m);
        rates.Should().ContainKey("GBP").WhoseValue.Should().Be(0.78m);
    }

    [Fact]
    public async Task GetAllRatesAsync_ExcludesStaleRates()
    {
        await SeedRateAsync("USD", "ZAR", 18.5m, fetchedAt: DateTime.UtcNow.AddHours(-3));
        (await _service.GetAllRatesAsync("USD")).Should().NotContainKey("ZAR");
    }

    [Fact]
    public async Task GetAllRatesAsync_ReturnsEmpty_WhenNoRatesExist()
    {
        (await _service.GetAllRatesAsync("USD")).Should().BeEmpty();
    }
}

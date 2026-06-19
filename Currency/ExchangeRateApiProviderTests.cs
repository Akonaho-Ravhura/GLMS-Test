using FluentAssertions;
using GLMS_CORE_APP.API.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Net;
using System.Text;

namespace GLMS_CORE_APP.Tests.Currency;

public class ExchangeRateApiProviderTests
{
    private static IConfiguration BuildConfig(string? apiKey = "test-key") =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CurrencyApi:ApiKey"] = apiKey
            })
            .Build();

    private static ExchangeRateApiProvider BuildProvider(
        MockHttpMessageHandler handler, string? apiKey = "test-key")
    {
        var client  = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);
        return new ExchangeRateApiProvider(factory.Object, BuildConfig(apiKey),
            NullLogger<ExchangeRateApiProvider>.Instance);
    }

    private static MockHttpMessageHandler SuccessHandler(
        string baseCurrency = "USD", Dictionary<string, decimal>? rates = null)
    {
        rates ??= new() { ["ZAR"] = 18.5m, ["EUR"] = 0.92m, ["GBP"] = 0.78m };
        var ratesJson = string.Join(",", rates.Select(kvp => $"\"{kvp.Key}\":{kvp.Value}"));
        var json = $$"""{"result":"success","base_code":"{{baseCurrency}}","conversion_rates":{{{ratesJson}}}""";
        return new MockHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
    }

    private static MockHttpMessageHandler ErrorHandler(string errorType = "invalid-key") =>
        new(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $$"""{"result":"error","error-type":"{{errorType}}"}""",
                Encoding.UTF8, "application/json")
        });

    [Fact]
    public async Task FetchRatesAsync_ReturnsSuccess_WithParsedRates()
    {
        var result = await BuildProvider(SuccessHandler()).FetchRatesAsync("USD");
        result.IsSuccess.Should().BeTrue();
        result.BaseCurrency.Should().Be("USD");
        result.Rates.Should().ContainKey("ZAR").WhoseValue.Should().Be(18.5m);
    }

    [Fact]
    public async Task FetchRatesAsync_UppercasesBaseCurrency()
    {
        var result = await BuildProvider(SuccessHandler()).FetchRatesAsync("usd");
        result.IsSuccess.Should().BeTrue();
        result.BaseCurrency.Should().Be("USD");
    }

    [Fact]
    public async Task FetchRatesAsync_ParsesMultipleRates()
    {
        var rates = Enumerable.Range(1, 10).ToDictionary(i => $"CUR{i}", i => i * 1.5m);
        var result = await BuildProvider(SuccessHandler(rates: rates)).FetchRatesAsync("USD");
        result.IsSuccess.Should().BeTrue();
        result.Rates.Should().HaveCount(10);
    }

    [Fact]
    public async Task FetchRatesAsync_ReturnsFailure_WhenApiKeyNotConfigured()
    {
        var result = await BuildProvider(SuccessHandler(), apiKey: null).FetchRatesAsync("USD");
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("API key");
    }

    [Fact]
    public async Task FetchRatesAsync_ReturnsFailure_WhenApiReturnsError()
    {
        var result = await BuildProvider(ErrorHandler("invalid-key")).FetchRatesAsync("USD");
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("invalid-key");
    }

    [Fact]
    public async Task FetchRatesAsync_ReturnsFailure_WhenHttpFails()
    {
        var handler = new MockHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("Unauthorized")
        });
        var result = await BuildProvider(handler).FetchRatesAsync("USD");
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("401");
    }

    [Fact]
    public async Task FetchRatesAsync_ReturnsFailure_WhenNetworkThrows()
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>()))
               .Throws(new HttpRequestException("Network error"));
        var provider = new ExchangeRateApiProvider(factory.Object, BuildConfig(),
            NullLogger<ExchangeRateApiProvider>.Instance);
        var result = await provider.FetchRatesAsync("USD");
        result.IsSuccess.Should().BeFalse();
    }
}

internal sealed class MockHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(response);
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NursingCareBackend.Infrastructure.Persistence;
using Xunit;

namespace NursingCareBackend.Api.Tests;

public sealed class VerifyPricingApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public VerifyPricingApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task VerifyPricing_Should_Return_Matches_True_For_Unchanged_Rates()
    {
        // Arrange: create a care request via the API (pricing calculator stores snapshots)
        var (clientToken, _) = await CareRequestApiAuthHelper.CreateClientTokenAsync(_factory, "verify-match");
        var careRequestId = await CreateCareRequestAsClientAsync(clientToken, "verify-match-request");

        var adminClient = _factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization =
          new AuthenticationHeaderValue("Bearer", JwtTestTokens.CreateAdminToken(_factory.Services));

        // Act
        var response = await adminClient.GetAsync($"/api/care-requests/{careRequestId}/verify-pricing");

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<VerifyPricingResult>();

        Assert.NotNull(result);
        Assert.Equal(careRequestId, result!.CareRequestId);
        Assert.True(result.Matches, "Pricing should match when catalog rates have not changed.");
        Assert.Equal(0.01m, result.ToleranceUsed);
        Assert.NotNull(result.LimitationNotes);
        Assert.NotEmpty(result.LimitationNotes);
        Assert.Empty(result.Discrepancies);
    }

    [Fact]
    public async Task VerifyPricing_Should_Return_404_For_NonExistent_CareRequest()
    {
        var adminClient = _factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization =
          new AuthenticationHeaderValue("Bearer", JwtTestTokens.CreateAdminToken(_factory.Services));

        var response = await adminClient.GetAsync($"/api/care-requests/{Guid.NewGuid()}/verify-pricing");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task VerifyPricing_Should_Return_Forbidden_For_NonAdmin_Client()
    {
        var (clientToken, _) = await CareRequestApiAuthHelper.CreateClientTokenAsync(_factory, "verify-forbidden-client");
        var careRequestId = await CreateCareRequestAsClientAsync(clientToken, "verify-forbidden-request");

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
          new AuthenticationHeaderValue("Bearer", clientToken);

        var response = await client.GetAsync($"/api/care-requests/{careRequestId}/verify-pricing");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task VerifyPricing_Should_Return_Forbidden_For_Nurse()
    {
        var (clientToken, _) = await CareRequestApiAuthHelper.CreateClientTokenAsync(_factory, "verify-forbidden-nurse-client");
        var careRequestId = await CreateCareRequestAsClientAsync(clientToken, "verify-forbidden-nurse-request");
        var (nurseToken, _) = await CareRequestApiAuthHelper.CreateCompletedNurseTokenAsync(_factory, "verify-forbidden-nurse");

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
          new AuthenticationHeaderValue("Bearer", nurseToken);

        var response = await client.GetAsync($"/api/care-requests/{careRequestId}/verify-pricing");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task VerifyPricing_Should_Return_Unauthorized_When_No_Token()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/care-requests/{Guid.NewGuid()}/verify-pricing");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GET_CareRequest_Should_Include_Pricing_Snapshot_Fields()
    {
        // Arrange
        var (clientToken, _) = await CareRequestApiAuthHelper.CreateClientTokenAsync(_factory, "snapshot-fields");
        var careRequestId = await CreateCareRequestAsClientAsync(clientToken, "snapshot-fields-request");

        var adminClient = _factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization =
          new AuthenticationHeaderValue("Bearer", JwtTestTokens.CreateAdminToken(_factory.Services));

        // Act
        var response = await adminClient.GetAsync($"/api/care-requests/{careRequestId}");

        response.EnsureSuccessStatusCode();
        var item = await response.Content.ReadFromJsonAsync<CareRequestWithPricing>();

        // Assert: all 8 pricing snapshot fields should be populated
        Assert.NotNull(item);
        Assert.NotNull(item!.PricingCategoryCode);
        Assert.NotNull(item.CategoryFactorSnapshot);
        Assert.NotNull(item.DistanceFactorMultiplierSnapshot);
        Assert.NotNull(item.ComplexityMultiplierSnapshot);
        Assert.NotNull(item.VolumeDiscountPercentSnapshot);
        Assert.NotNull(item.LineBeforeVolumeDiscount);
        Assert.NotNull(item.UnitPriceAfterVolumeDiscount);
        Assert.NotNull(item.SubtotalBeforeSupplies);

        // Verify numeric values are positive
        Assert.True(item.CategoryFactorSnapshot > 0, "CategoryFactorSnapshot should be positive");
        Assert.True(item.LineBeforeVolumeDiscount > 0, "LineBeforeVolumeDiscount should be positive");
        Assert.True(item.UnitPriceAfterVolumeDiscount > 0, "UnitPriceAfterVolumeDiscount should be positive");
        Assert.True(item.SubtotalBeforeSupplies > 0, "SubtotalBeforeSupplies should be positive");
    }

    [Fact]
    public async Task VerifyPricing_LimitationNotes_Should_Mention_Volume_Discount()
    {
        var (clientToken, _) = await CareRequestApiAuthHelper.CreateClientTokenAsync(_factory, "verify-limitation");
        var careRequestId = await CreateCareRequestAsClientAsync(clientToken, "verify-limitation-request");

        var adminClient = _factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization =
          new AuthenticationHeaderValue("Bearer", JwtTestTokens.CreateAdminToken(_factory.Services));

        var response = await adminClient.GetAsync($"/api/care-requests/{careRequestId}/verify-pricing");

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<VerifyPricingResult>();

        Assert.NotNull(result);
        Assert.Contains(result!.LimitationNotes, note => note.Contains("volumen", StringComparison.OrdinalIgnoreCase));
    }

    private async Task<Guid> CreateCareRequestAsClientAsync(string clientToken, string description)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", clientToken);

        var createResponse = await client.PostAsJsonAsync("/api/care-requests", new
        {
            careRequestDescription = description,
            careRequestType = "domicilio_24h",
            unit = 1
        });

        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<CreateResponse>();
        Assert.NotNull(created);
        return created!.Id;
    }

    private sealed class CreateResponse
    {
        public Guid Id { get; set; }
    }

    private sealed class VerifyPricingResult
    {
        public Guid CareRequestId { get; set; }
        public bool Matches { get; set; }
        public decimal ToleranceUsed { get; set; }
        public List<string> LimitationNotes { get; set; } = new();
        public List<PricingDiscrepancyItem> Discrepancies { get; set; } = new();
    }

    private sealed class PricingDiscrepancyItem
    {
        public string FieldName { get; set; } = default!;
        public decimal StoredValue { get; set; }
        public decimal CurrentValue { get; set; }
        public decimal Difference { get; set; }
    }

    private sealed class CareRequestWithPricing
    {
        public Guid Id { get; set; }
        public Guid UserID { get; set; }
        public string? PricingCategoryCode { get; set; }
        public decimal? CategoryFactorSnapshot { get; set; }
        public decimal? DistanceFactorMultiplierSnapshot { get; set; }
        public decimal? ComplexityMultiplierSnapshot { get; set; }
        public int? VolumeDiscountPercentSnapshot { get; set; }
        public decimal? LineBeforeVolumeDiscount { get; set; }
        public decimal? UnitPriceAfterVolumeDiscount { get; set; }
        public decimal? SubtotalBeforeSupplies { get; set; }
        public decimal Price { get; set; }
        public decimal Total { get; set; }
    }
}

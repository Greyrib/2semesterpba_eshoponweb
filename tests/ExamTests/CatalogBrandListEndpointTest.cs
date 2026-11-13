using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using MinimalApi.Endpoint;
using Microsoft.eShopWeb;
using Microsoft.eShopWeb.PublicApi.CatalogBrandEndpoints;
using Xunit;

namespace ExamTests;

public class CatalogBrandListEndpointTest
{
    // TODO : Point at MICROSERVICE, you CLANKER CLOWN >:V

    [Fact]
    public async Task GetCatalogBrands_Returns200_AndSeededBrands()
    {
        // Arrange
        // A) Create an instance of WebApplicationFactory (starts PublicApi in memory)
        //    PublicApi's AddEndpoints() registers IEndpoint classes from EVERY assembly loaded in the process.
        //    If a CatalogMicroservice test ran first, its endpoints get picked up too and the app fails to start.
        //    So: throw away every endpoint that doesn't belong to PublicApi itself.
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                var foreignEndpoints = services
                    .Where(d => d.ServiceType == typeof(IEndpoint) && d.ImplementationType?.Assembly != typeof(Program).Assembly)
                    .ToList();

                foreach (var endpoint in foreignEndpoints)
                    services.Remove(endpoint);
            }));

        // B) Generate an HttpClient from the factory
        using var client = factory.CreateClient();

        // Act
        // C) Do the call and assert status code and payload
        var response = await client.GetAsync("api/catalog-brands");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        var model = json.FromJson<ListCatalogBrandsResponse>();

        Assert.Equal(5, model!.CatalogBrands.Count);
        Assert.Contains(model.CatalogBrands, b => b.Name == "Azure");
    }
}

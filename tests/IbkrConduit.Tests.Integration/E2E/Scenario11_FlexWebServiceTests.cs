using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using IbkrConduit.Auth;
using IbkrConduit.Client;
using IbkrConduit.Flex;
using IbkrConduit.Http;
using IbkrConduit.Session;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace IbkrConduit.Tests.Integration.E2E;

/// <summary>
/// E2E Scenario 11: Flex Web Service.
/// Exercises the two-step Flex query flow (send request + poll for statement)
/// against the real IBKR Flex Web Service using a long-lived Flex token.
/// </summary>
[Collection("IBKR E2E")]
[ExcludeFromCodeCoverage]
public sealed class Scenario11_FlexWebServiceTests : E2eScenarioBase
{
    /// <summary>
    /// Happy path: execute a Flex query and verify the result contains expected elements.
    /// Requires IBKR_FLEX_TOKEN and IBKR_FLEX_QUERY_ID environment variables.
    /// </summary>
    [EnvironmentFact("IBKR_FLEX_TOKEN")]
    public async Task FlexWebService_ExecuteQuery()
    {
        var queryId = Environment.GetEnvironmentVariable("IBKR_FLEX_QUERY_ID");
        if (string.IsNullOrEmpty(queryId))
        {
            return;
        }

        var client = CreateFlexClient();
        await using var provider = client.Provider;

        try
        {
            StartRecording("Scenario11_FlexWebService_ExecuteQuery");

            // Step 1: Execute query — handles send + poll internally
            var result = await client.Client.Flex.ExecuteQueryAsync(queryId, CT);

            // Step 2: Verify the result contains the raw XML and FlexStatements
            result.RawXml.ShouldNotBeNull("Flex query should return an XML document");
            result.RawXml.Descendants("FlexStatements").ShouldNotBeEmpty(
                "Flex query result should contain FlexStatements element");
        }
        finally
        {
            StopRecording();
        }
    }

    /// <summary>
    /// Error case: executing a query with an invalid query ID should throw FlexQueryException.
    /// </summary>
    [EnvironmentFact("IBKR_FLEX_TOKEN")]
    public async Task ExecuteQuery_InvalidQueryId_ThrowsFlexQueryException()
    {
        var client = CreateFlexClient();
        await using var provider = client.Provider;

        try
        {
            StartRecording("Scenario11_FlexWebService_InvalidQueryId");

            var ex = await Should.ThrowAsync<FlexQueryException>(
                () => client.Client.Flex.ExecuteQueryAsync("999999", CT));

            ex.ErrorCode.ShouldBeGreaterThan(0, "Error code should be a positive integer");
            ex.Message.ShouldNotBeNullOrWhiteSpace("Error message should describe the failure");
        }
        finally
        {
            StopRecording();
        }
    }

    private (ServiceProvider Provider, IIbkrClient Client) CreateFlexClient()
    {
        var flexToken = Environment.GetEnvironmentVariable("IBKR_FLEX_TOKEN")!;
        using var creds = OAuthCredentialsFactory.FromEnvironment();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIbkrClient(creds, new IbkrClientOptions
        {
            FlexToken = flexToken,
        });

        var provider = services.BuildServiceProvider();
        var ibkrClient = provider.GetRequiredService<IIbkrClient>();
        return (provider, ibkrClient);
    }
}

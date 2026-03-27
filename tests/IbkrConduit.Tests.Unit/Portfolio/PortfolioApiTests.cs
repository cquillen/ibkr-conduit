using System.Text.Json;
using IbkrConduit.Portfolio;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Portfolio;

public class PortfolioApiTests
{
    [Fact]
    public void Account_DeserializesFromJson()
    {
        var json = """
            {
                "id": "U1234567",
                "accountTitle": "Paper Trading Account",
                "type": "INDIVIDUAL"
            }
            """;

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var account = JsonSerializer.Deserialize<Account>(json, options);

        account.ShouldNotBeNull();
        account.Id.ShouldBe("U1234567");
        account.AccountTitle.ShouldBe("Paper Trading Account");
        account.Type.ShouldBe("INDIVIDUAL");
    }
}

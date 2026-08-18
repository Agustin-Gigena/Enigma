using Enigma.Test.Auth;
using Xunit;

namespace Enigma.Test.Security;

public class HstsTest : IClassFixture<EnigmaWebFactory>
{
  private readonly HttpClient _client;
  public HstsTest(EnigmaWebFactory factory) => _client = factory.CreateClient();

  [Fact]
  public async Task EnDesarrollo_NoSeEnviaHeaderHsts()
  {
    HttpResponseMessage response = await _client.GetAsync("/auth/me");

    Assert.False(response.Headers.Contains("Strict-Transport-Security"),
        "En Development NO debe aplicarse HSTS (UseHsts lanza si se llama en dev).");
  }
}

using Enigma.Test.Auth;
using NUnit.Framework;

namespace Enigma.Test.Security;

[TestFixture]
public class HstsTest
{
    private static EnigmaWebFactory _factory = null!;
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public void Setup()
    {
        _factory = new EnigmaWebFactory();
        _client = _factory.CreateClient();
    }

    [OneTimeTearDown]
    public void TearDown() => _factory?.Dispose();

    [Test]
    public async Task EnDesarrollo_NoSeEnviaHeaderHsts()
    {
        HttpResponseMessage response = await _client.GetAsync("/auth/me");

        Assert.That(response.Headers.Contains("Strict-Transport-Security"), Is.False,
            "En Development NO debe aplicarse HSTS (UseHsts lanza si se llama en dev).");
    }
}

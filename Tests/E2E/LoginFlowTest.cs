using Microsoft.Playwright;
using NUnit.Framework;

namespace Enigma.Test.E2E;

[TestFixture]
public class LoginFlowTest
{
    private IPlaywright _playwright = null!;
    private IBrowser _browser = null!;
    private IPage _page = null!;

    [OneTimeSetUp]
    public async Task BrowserSetup()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new() { Headless = true });
    }

    [SetUp]
    public async Task NewPage()
    {
        _page = await _browser.NewPageAsync();
    }

    [TearDown]
    public async Task ClosePage()
    {
        await _page.CloseAsync();
    }

    [OneTimeTearDown]
    public async Task BrowserTeardown()
    {
        await _browser.CloseAsync();
        _playwright.Dispose();
    }

    [Test]
    public async Task Login_Admin_RetornaTokenYRedirigeHome()
    {
        await _page.GotoAsync($"{E2EWebFixture.ClientUrl}/auth/login");

        await _page.GetByLabel("Usuario").FillAsync("admin");
        await _page.GetByLabel("Contraseña").FillAsync("admin123");
        await _page.GetByRole(AriaRole.Button, new() { Name = "Ingresá" }).ClickAsync();

        await _page.WaitForURLAsync("**/");

        string? textoHome = await _page.GetByText("admin").TextContentAsync();
        Assert.That(textoHome, Does.Contain("admin"));

        string? token = await _page.EvaluateAsync<string>("localStorage.getItem('enigma_token')");
        Assert.That(token, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public async Task Login_CredencialesInvalidas_MuestraError()
    {
        await _page.GotoAsync($"{E2EWebFixture.ClientUrl}/auth/login");

        await _page.GetByLabel("Usuario").FillAsync("admin");
        await _page.GetByLabel("Contraseña").FillAsync("incorrecta");
        await _page.GetByRole(AriaRole.Button, new() { Name = "Ingresá" }).ClickAsync();

        ILocator alerta = _page.GetByRole(AriaRole.Alert);
        await alerta.WaitForAsync(new() { Timeout = 5000 });
        string? texto = await alerta.TextContentAsync();
        Assert.That(texto, Does.Contain("incorrectos"));

        string? token = await _page.EvaluateAsync<string>("localStorage.getItem('enigma_token')");
        Assert.That(token, Is.Null);
    }

    [Test]
    public async Task Logout_LimpiaSesionYRedirigeLogin()
    {
        // Login primero
        await _page.GotoAsync($"{E2EWebFixture.ClientUrl}/auth/login");
        await _page.GetByLabel("Usuario").FillAsync("admin");
        await _page.GetByLabel("Contraseña").FillAsync("admin123");
        await _page.GetByRole(AriaRole.Button, new() { Name = "Ingresá" }).ClickAsync();
        await _page.WaitForURLAsync("**/");

        // Click "Salir"
        await _page.GetByRole(AriaRole.Button, new() { Name = "Salir" }).ClickAsync();
        await _page.WaitForURLAsync("**/auth/login");

        string? token = await _page.EvaluateAsync<string>("localStorage.getItem('enigma_token')");
        Assert.That(token, Is.Null);
    }
}

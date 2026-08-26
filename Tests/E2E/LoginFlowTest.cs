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
    public async Task NewPage() => _page = await _browser.NewPageAsync();

    [TearDown]
    public async Task ClosePage() => await _page.CloseAsync();

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

        await _page.GetByRole(AriaRole.Textbox, new() { Name = "Usuario" }).FillAsync("admin");
        await _page.GetByRole(AriaRole.Textbox, new() { Name = "Contraseña" }).FillAsync("admin123");
        await _page.GetByRole(AriaRole.Button, new() { Name = "Ingresá" }).ClickAsync();

        // Admin tiene 2 instituciones → pasa por la selección antes del Home.
        await _page.WaitForURLAsync("**/auth/seleccion-institucion", new() { Timeout = 10000 });
        await _page.Locator(".seleccion__tarjeta").First.ClickAsync();
        await _page.WaitForURLAsync("**/", new() { Timeout = 10000 });

        ILocator saludo = _page.GetByText("Bienvenido, admin");
        await saludo.WaitForAsync(new() { Timeout = 10000 });

        // El JWT vive en una cookie HttpOnly (ya no se guarda en localStorage).
        IReadOnlyList<BrowserContextCookiesResult> cookies =
            await _page.Context.CookiesAsync($"{E2EWebFixture.ClientUrl}/");
        Assert.That(
            cookies.Any(c => c.Name == "enigma_token" && !string.IsNullOrEmpty(c.Value)),
            Is.True, "La cookie enigma_token debería existir tras el login.");
    }

    [Test]
    public async Task Login_CredencialesInvalidas_MuestraError()
    {
        await _page.GotoAsync($"{E2EWebFixture.ClientUrl}/auth/login");

        await _page.GetByLabel("Usuario").FillAsync("admin");
        await _page.GetByRole(AriaRole.Textbox, new() { Name = "Contraseña" }).FillAsync("incorrecta");
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
        await _page.GetByRole(AriaRole.Textbox, new() { Name = "Contraseña" }).FillAsync("admin123");
        await _page.GetByRole(AriaRole.Button, new() { Name = "Ingresá" }).ClickAsync();
        await _page.WaitForURLAsync("**/");

        // Click "Salir"
        await _page.GetByRole(AriaRole.Button, new() { Name = "Salir" }).ClickAsync();
        await _page.WaitForURLAsync("**/auth/login");

        string? token = await _page.EvaluateAsync<string>("localStorage.getItem('enigma_token')");
        Assert.That(token, Is.Null);
    }

    [Test]
    public async Task SeleccionInstitucion_MultiTenant()
    {
        await _page.GotoAsync($"{E2EWebFixture.ClientUrl}/auth/login");

        await _page.GetByLabel("Usuario").FillAsync("admin");
        await _page.GetByRole(AriaRole.Textbox, new() { Name = "Contraseña" }).FillAsync("admin123");
        await _page.GetByRole(AriaRole.Button, new() { Name = "Ingresá" }).ClickAsync();

        // Admin tiene 2 instituciones → debería llegar a selección
        await _page.WaitForURLAsync("**/auth/seleccion-institucion", new() { Timeout = 10000 });

        // Click en la primera institución
        ILocator tarjetas = _page.GetByRole(AriaRole.Button);
        await tarjetas.First.ClickAsync();

        // Verificar redirect a Home
        await _page.WaitForURLAsync("**/");
    }

    [Test]
    public async Task TokenExpirado_RedirigeLogin()
    {
        // Login primero
        await _page.GotoAsync($"{E2EWebFixture.ClientUrl}/auth/login");
        await _page.GetByLabel("Usuario").FillAsync("admin");
        await _page.GetByRole(AriaRole.Textbox, new() { Name = "Contraseña" }).FillAsync("admin123");
        await _page.GetByRole(AriaRole.Button, new() { Name = "Ingresá" }).ClickAsync();
        await _page.WaitForURLAsync("**/");

        // Inyectar token expirado en localStorage
        string tokenExpirado = TokenExpirado();
        await _page.EvaluateAsync($"localStorage.setItem('enigma_token', '{tokenExpirado}')");

        // Refrescar → auth state lee token expirado → anónimo → redirect a login
        await _page.ReloadAsync();
        await _page.WaitForURLAsync("**/auth/login", new() { Timeout = 10000 });
    }

    private static string TokenExpirado()
    {
        // JWT manual con exp en el pasado (2020-01-01)
        string header = Base64UrlEncode("{\"alg\":\"HS256\",\"typ\":\"JWT\"}");
        string payload = Base64UrlEncode("{\"sub\":\"1\",\"nameid\":\"1\",\"unique_name\":\"admin\",\"exp\":1577836800}");
        string signature = "firma-falsa";
        return $"{header}.{payload}.{signature}";
    }

    private static string Base64UrlEncode(string json)
    {
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    [Test]
    public async Task RutaProtegida_YaAutenticado_RedirigeHome()
    {
        // Login primero
        await _page.GotoAsync($"{E2EWebFixture.ClientUrl}/auth/login");
        await _page.GetByLabel("Usuario").FillAsync("admin");
        await _page.GetByRole(AriaRole.Textbox, new() { Name = "Contraseña" }).FillAsync("admin123");
        await _page.GetByRole(AriaRole.Button, new() { Name = "Ingresá" }).ClickAsync();
        await _page.WaitForURLAsync("**/");

        // Navegar directamente a selección de institución
        await _page.GotoAsync($"{E2EWebFixture.ClientUrl}/auth/seleccion-institucion");

        // Debería redirect a Home (ya está autenticado, route guard redirige)
        await _page.WaitForURLAsync("**/", new() { Timeout = 10000 });
    }
}

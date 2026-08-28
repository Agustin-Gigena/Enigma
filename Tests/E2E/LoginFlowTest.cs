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
        // 60 s: es el primer test del suite en cargar el WASM (cold start del dev server).
        await _page.GetByRole(AriaRole.Textbox, new() { Name = "Usuario" }).FillAsync("admin", new() { Timeout = 60000 });
        await _page.GetByRole(AriaRole.Textbox, new() { Name = "Contraseña" }).FillAsync("admin123", new() { Timeout = 60000 });
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
        // Admin tiene 2 instituciones: hay que elegir una para llegar al Home
        // (sin esto el flujo queda en la selección y el menú de cuenta no existe).
        await _page.WaitForURLAsync("**/auth/seleccion-institucion", new() { Timeout = 10_000 });
        await _page.Locator(".seleccion__tarjeta").First.ClickAsync();

        // Menú de cuenta → "Cerrar sesión" (T10 reemplazó el botón "Salir" de la barra).
        await _page.Locator(".app-cuenta summary").ClickAsync();
        await _page.GetByRole(AriaRole.Button, new() { Name = "Cerrar sesión" }).ClickAsync();
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

        // Click en la primera institución (tarjeta: el primer botón del DOM es "Salir").
        await _page.Locator(".seleccion__tarjeta").First.ClickAsync();

        // Verificar redirect a Home
        await _page.WaitForURLAsync("**/");
    }

    [Test]
    public async Task SesionInvalidada_RedirigeLogin()
    {
        // Login + selección → sesión completa en Home.
        await _page.GotoAsync($"{E2EWebFixture.ClientUrl}/auth/login");
        await _page.GetByLabel("Usuario").FillAsync("admin");
        await _page.GetByRole(AriaRole.Textbox, new() { Name = "Contraseña" }).FillAsync("admin123");
        await _page.GetByRole(AriaRole.Button, new() { Name = "Ingresá" }).ClickAsync();
        await _page.WaitForURLAsync("**/auth/seleccion-institucion", new() { Timeout = 10000 });
        await _page.Locator(".seleccion__tarjeta").First.ClickAsync();
        // Esperar el Home renderizado (no solo la URL): garantiza que el POST de
        // selección terminó antes de borrar la cookie.
        await _page.GetByText("Bienvenido, admin").WaitForAsync(new() { Timeout = 10000 });

        // Sin la cookie no hay sesión: /auth/me da 401 → provider anónimo → redirect a login.
        // 45 s: tras el reload el redirect sale del guard tras un arranque completo
        // del WASM (5-30 s en el devcontainer con build Debug + dotnet run).
        await _page.Context.ClearCookiesAsync();
        await _page.ReloadAsync();
        await _page.WaitForURLAsync("**/auth/login", new() { Timeout = 45_000 });
    }

    [Test]
    public async Task RutaProtegida_YaAutenticado_RedirigeHome()
    {
        // Login + selección → sesión completa (admin tiene 2 instituciones).
        await _page.GotoAsync($"{E2EWebFixture.ClientUrl}/auth/login");
        await _page.GetByLabel("Usuario").FillAsync("admin");
        await _page.GetByRole(AriaRole.Textbox, new() { Name = "Contraseña" }).FillAsync("admin123");
        await _page.GetByRole(AriaRole.Button, new() { Name = "Ingresá" }).ClickAsync();
        await _page.WaitForURLAsync("**/auth/seleccion-institucion", new() { Timeout = 10000 });
        await _page.Locator(".seleccion__tarjeta").First.ClickAsync();
        // URL EXACTA: el glob "**/" matchea cualquier URL y hacía pasar el test vacío.
        string home = $"{E2EWebFixture.ClientUrl}/";
        await _page.WaitForURLAsync(url => url == home, new() { Timeout = 10000 });

        // Ya autenticado con institución: /auth/login debe redirigir al Home.
        // 45 s: el redirect fuerza recarga completa → nuevo arranque del WASM.
        await _page.GotoAsync($"{E2EWebFixture.ClientUrl}/auth/login");
        await _page.WaitForURLAsync(url => url == home, new() { Timeout = 45_000 });
    }
}

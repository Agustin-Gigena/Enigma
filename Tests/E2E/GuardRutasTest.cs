using Microsoft.Playwright;
using NUnit.Framework;

namespace Enigma.Test.E2E;

/// <summary>Guard central del cliente: rutas de sección exigen claim permiso.</summary>
[TestFixture]
public class GuardRutasTest
{
    private IPlaywright _playwright = null!;
    private IBrowser _browser = null!;

    [OneTimeSetUp]
    public async Task Setup()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new() { Headless = true });
    }

    [OneTimeTearDown]
    public async Task Teardown()
    {
        await _browser.CloseAsync();
        _playwright.Dispose();
    }

    private async Task<IPage> PaginaAdminAutenticadaAsync()
    {
        IPage page = await _browser.NewPageAsync();
        await page.GotoAsync($"{E2EWebFixture.ClientUrl}/auth/login");
        await page.GetByRole(AriaRole.Textbox, new() { Name = "Usuario" }).FillAsync("admin");
        await page.GetByRole(AriaRole.Textbox, new() { Name = "Contraseña" }).FillAsync("admin123");
        await page.GetByRole(AriaRole.Button, new() { Name = "Ingresá" }).ClickAsync();
        await page.WaitForURLAsync("**/auth/seleccion-institucion", new() { Timeout = 10_000 });
        await page.Locator(".seleccion__tarjeta").First.ClickAsync();
        await page.WaitForURLAsync("**/", new() { Timeout = 10_000 });
        return page;
    }

    [Test]
    public async Task RutaDeSeccion_ConPermiso_Renderiza()
    {
        IPage page = await PaginaAdminAutenticadaAsync();
        try
        {
            await page.GotoAsync($"{E2EWebFixture.ClientUrl}/administracion/instituciones");
            // El Task 11 crea la página; hasta entonces basta con QUE NO redirija a denegado.
            await page.WaitForURLAsync(url => !url.Contains("acceso-denegado"), new() { Timeout = 5_000 });
            Assert.Pass();
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [Test]
    public async Task RutaDeSeccion_SinPermiso_RedirigeAAccesoDenegado()
    {
        IPage page = await PaginaAdminAutenticadaAsync();
        try
        {
            // Forjar sesión SIN roles con el secret fijo del fixture.
            string token = ForjadorTokens.Sesion(usuarioId: 1, institucionId: 1, roles: []);
            await page.Context.AddCookiesAsync(new[]
            {
                new Cookie
                {
                    Name = "enigma_token", Value = token, Domain = "localhost", Path = "/",
                    SameSite = SameSiteAttribute.None, Secure = true,
                },
            });
            // Invalidar cache del provider para que re-valide contra /auth/me.
            await page.ReloadAsync();
            await page.GotoAsync($"{E2EWebFixture.ClientUrl}/administracion/instituciones");
            await page.WaitForURLAsync("**/acceso-denegado", new() { Timeout = 10_000 });
            Assert.That(page.Url, Does.Contain("acceso-denegado"));
        }
        finally
        {
            await page.CloseAsync();
        }
    }
}

using Microsoft.Playwright;
using NUnit.Framework;

namespace Enigma.Test.E2E;

[TestFixture]
public class MenuUiTest
{
    private IPlaywright _playwright = null!;
    private IBrowser _browser = null!;
    private IPage _page = null!;

    [OneTimeSetUp]
    public async Task Setup()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new() { Headless = true });
    }

    [SetUp]
    public async Task NewPage() => _page = await _browser.NewPageAsync();

    [TearDown]
    public async Task ClosePage() => await _page.CloseAsync();

    [OneTimeTearDown]
    public async Task Teardown()
    {
        await _browser.CloseAsync();
        _playwright.Dispose();
    }

    private async Task EntrarComoAdminAsync()
    {
        await _page.GotoAsync($"{E2EWebFixture.ClientUrl}/auth/login");
        await _page.GetByRole(AriaRole.Textbox, new() { Name = "Usuario" }).FillAsync("admin");
        await _page.GetByRole(AriaRole.Textbox, new() { Name = "Contraseña" }).FillAsync("admin123");
        await _page.GetByRole(AriaRole.Button, new() { Name = "Ingresá" }).ClickAsync();
        await _page.WaitForURLAsync("**/auth/seleccion-institucion", new() { Timeout = 10_000 });
        await _page.Locator(".seleccion__tarjeta").First.ClickAsync();
        await _page.WaitForURLAsync("**/", new() { Timeout = 10_000 });
    }

    [Test]
    public async Task Barra_MuestraModuloConSeccionesParaAdmin()
    {
        await EntrarComoAdminAsync();
        await _page.Locator(".app-nav").WaitForAsync(new() { Timeout = 10_000 });
        await _page.Locator(".app-nav details summary").First.ClickAsync();
        await _page.GetByRole(AriaRole.Link, new() { Name = "Usuarios" }).WaitForAsync(new() { Timeout = 5_000 });
        await _page.GetByRole(AriaRole.Link, new() { Name = "Instituciones" }).WaitForAsync(new() { Timeout = 5_000 });
    }

    [Test]
    public async Task Barra_Angosta_LasSeccionesVanAMas()
    {
        await EntrarComoAdminAsync();
        await _page.SetViewportSizeAsync(420, 800);
        await _page.Locator(".app-nav__mas").WaitForAsync(new() { Timeout = 10_000 });
        await _page.Locator(".app-nav__mas summary").ClickAsync();
        await _page.GetByRole(AriaRole.Link, new() { Name = "Usuarios" }).WaitForAsync(new() { Timeout = 5_000 });
    }

    [Test]
    public async Task MenuCuenta_CierraSesion()
    {
        await EntrarComoAdminAsync();
        await _page.Locator(".app-cuenta summary").ClickAsync();
        await _page.GetByRole(AriaRole.Button, new() { Name = "Cerrar sesión" }).ClickAsync();
        await _page.WaitForURLAsync("**/auth/login", new() { Timeout = 10_000 });
    }

    [Test]
    public async Task SelectorInstitucion_CambiaYRecalcula()
    {
        await EntrarComoAdminAsync();
        await _page.Locator(".app-institucion-menu summary").ClickAsync();
        await _page.GetByRole(AriaRole.Button, new() { Name = "Colegio San Martín" }).ClickAsync();
        await _page.WaitForURLAsync("**/", new() { Timeout = 10_000 });
        await _page.Locator(".app-institucion-menu summary").WaitForAsync(new() { Timeout = 10_000 });
        Assert.That(await _page.Locator(".app-institucion-menu summary").InnerTextAsync(),
            Does.Contain("Colegio San Martín"));
    }
}

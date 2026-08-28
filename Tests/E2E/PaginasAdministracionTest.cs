using Microsoft.Playwright;
using NUnit.Framework;

namespace Enigma.Test.E2E;

[TestFixture]
public class PaginasAdministracionTest
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
        // Con el server frío, POST /auth/institucion tarda varios segundos (JIT + MySQL).
        // El glob "**/" resuelve espuriamente sobre /auth/seleccion-institucion, y un
        // GotoAsync posterior cancelaría el POST en vuelo (sesión perdida → redirect a
        // login). Se espera el aterrizaje REAL en "/" (solo ocurre tras el 200 del POST).
        await _page.WaitForURLAsync(url => new Uri(url).AbsolutePath == "/", new() { Timeout = 45_000 });
    }

    [Test]
    public async Task Instituciones_ListaLasDelSeed()
    {
        await EntrarComoAdminAsync();
        await _page.GotoAsync($"{E2EWebFixture.ClientUrl}/administracion/instituciones");
        // GotoAsync = full reload: el arranque del WASM tarda 5-30 s en el devcontainer.
        // Scope a la tabla: el selector de institución del header también lista
        // los nombres (el admin es miembro de ambas instituciones del seed).
        await _page.Locator(".tabla-admin").GetByText("Universidad Nacional del Plata").WaitForAsync(new() { Timeout = 45_000 });
        await _page.Locator(".tabla-admin").GetByText("Colegio San Martín").WaitForAsync(new() { Timeout = 45_000 });
    }

    [Test]
    public async Task Usuarios_MuestraAdminConRolYGuarda()
    {
        await EntrarComoAdminAsync();
        await _page.GotoAsync($"{E2EWebFixture.ClientUrl}/administracion/usuarios");
        // Presupuesto generoso: full reload + arranque WASM (5-30 s en devcontainer).
        // Celda exacta: GetByText("admin") también matchea los labels de los roles
        // ("Admin", "Administrador") por substring case-insensitive.
        await _page.Locator(".tabla-admin").GetByRole(AriaRole.Cell, new() { Name = "admin", Exact = true })
            .WaitForAsync(new() { Timeout = 45_000 });
        ILocator checkAdmin = _page.Locator("input[type='checkbox'][name='Admin']").First;
        Assert.That(await checkAdmin.IsCheckedAsync(), Is.True, "El admin sembrado tiene rol Admin.");

        await checkAdmin.UncheckAsync();
        await _page.GetByRole(AriaRole.Button, new() { Name = "Guardar" }).First.ClickAsync();
        await _page.GetByText("Roles actualizados.").WaitForAsync(new() { Timeout = 10_000 });

        // Restaurar (deja la BD como estaba) y verificar. Restaurar SUMA permisos al
        // propio usuario → la página re-emite su sesión y se recarga completa
        // (forceLoad): no hay mensaje inline; la tabla vuelve con el estado persistido.
        await checkAdmin.CheckAsync();
        await _page.GetByRole(AriaRole.Button, new() { Name = "Guardar" }).First.ClickAsync();
        await _page.Locator(".tabla-admin").GetByRole(AriaRole.Cell, new() { Name = "admin", Exact = true })
            .WaitForAsync(new() { Timeout = 45_000 });
        await _page.Locator("input[type='checkbox'][name='Admin']").First.WaitForAsync(new() { Timeout = 45_000 });
        Assert.That(await _page.Locator("input[type='checkbox'][name='Admin']").First.IsCheckedAsync(), Is.True);
    }
}

using Microsoft.Playwright;
using NUnit.Framework;

namespace Enigma.Test.E2E;

[TestFixture]
public class PaginasAdministracionTest
{
    private IPlaywright _playwright = null!;
    private IBrowser _browser = null!;
    private IPage _page = null!;
    private IBrowserContext _context = null!;

    [OneTimeSetUp]
    public async Task Setup()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new() { Headless = true });
    }

    [SetUp]
    public async Task NewPage()
    {
        // Contexto aislado: sin cookies/localStorage del test anterior.
        _context = await _browser.NewContextAsync();
        _page = await _context.NewPageAsync();
    }

    [TearDown]
    public async Task ClosePage()
    {
        await _context.CloseAsync();
    }

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

        // Abrir el diálogo de roles (icono lápiz de la fila del admin).
        await _page.GetByRole(AriaRole.Button, new() { Name = "Editar roles de admin" })
            .ClickAsync(new() { Timeout = 10_000 });
        await _page.Locator(".mud-dialog .chip").First.WaitForAsync(new() { Timeout = 10_000 });

        // El admin sembrado tiene el chip Admin activo (aria-pressed=true).
        ILocator chipAdmin = _page.Locator(".mud-dialog .chip").GetByText("Admin", new() { Exact = true });
        Assert.That(await chipAdmin.GetAttributeAsync("aria-pressed"), Is.EqualTo("true"),
            "El admin sembrado tiene rol Admin.");

        // Quitar el rol, guardar (MudDialog) y esperar el snackbar de éxito.
        await chipAdmin.ClickAsync();
        await _page.GetByRole(AriaRole.Button, new() { Name = "Guardar" }).ClickAsync();
        await _page.GetByText("Roles actualizados.").WaitForAsync(new() { Timeout = 10_000 });

        // Restaurar (deja la BD como estaba) y verificar el estado persistido.
        await _page.GetByRole(AriaRole.Button, new() { Name = "Editar roles de admin" })
            .ClickAsync(new() { Timeout = 10_000 });
        ILocator chipAdminReabierto = _page.Locator(".mud-dialog .chip").GetByText("Admin", new() { Exact = true });
        await chipAdminReabierto.WaitForAsync(new() { Timeout = 10_000 });
        Assert.That(await chipAdminReabierto.GetAttributeAsync("aria-pressed"), Is.EqualTo("false"),
            "El rol quedó quitado tras guardar.");

        await chipAdminReabierto.ClickAsync();
        await _page.GetByRole(AriaRole.Button, new() { Name = "Guardar" }).ClickAsync();
        await _page.GetByText("Roles actualizados.").WaitForAsync(new() { Timeout = 10_000 });

        // Reabrir: el rol restaurado persiste.
        await _page.GetByRole(AriaRole.Button, new() { Name = "Editar roles de admin" })
            .ClickAsync(new() { Timeout = 10_000 });
        ILocator chipAdminFinal = _page.Locator(".mud-dialog .chip").GetByText("Admin", new() { Exact = true });
        await chipAdminFinal.WaitForAsync(new() { Timeout = 10_000 });
        Assert.That(await chipAdminFinal.GetAttributeAsync("aria-pressed"), Is.EqualTo("true"));
        // Cerrar el diálogo con Cancelar (regresión del crash de cierre).
        await _page.GetByRole(AriaRole.Button, new() { Name = "Cancelar" }).ClickAsync();
        await _page.Locator(".mud-dialog").WaitForAsync(new() { Timeout = 10_000 });
    }
}

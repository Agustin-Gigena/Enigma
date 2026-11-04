# Monorepo Web + MAUI + API Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Convertir Enigma en un monorepo `src/` + `tests/` con paridad Build-It: `Enigma.{Api,Web,SharedUI,Maui,Shared}` + `Enigma.Tests` único, UI compartida en RCL, host MAUI Blazor Hybrid (Android+Windows), CI/deploy según spec.

**Architecture:** La UI (páginas, layouts, servicios de auth/tema, assets del design system) migra a la RCL `Enigma.SharedUI`; `Enigma.Web` queda como host WASM fino y `Enigma.Maui` como host Blazor Hybrid — ambos montan `SharedUI.App` y registran su propio `HttpClient`. `Enigma.Api` es rename puro de `Server`. Nadie referencia la Api excepto los tests (regla exigida en `ArchitectureTests`). Spec: `docs/superpowers/specs/2026-11-04-monorepo-web-maui-api-design.md`.

**Tech Stack:** .NET 10 (net10.0), Blazor WASM, MAUI Blazor Hybrid, EF Core + MySQL (`Microting.EntityFrameworkCore.MySql`), NUnit 4, Stryker, GitHub Actions.

## Global Constraints

- Todas las rutas nuevas siguen el spec §2: `src/Enigma.{Api,Web,SharedUI,Maui,Shared}`, `tests/Enigma.Tests`, `deploy/`.
- Namespaces: `Enigma.Server.*` → `Enigma.Api.*`; `Enigma.Client.*` → `Enigma.Web.*` (hasta Task 2, que mueve la UI a `Enigma.SharedUI.*`).
- **PROHIBIDO reescribir el literal JWT audience `"Enigma.Client"` ni el issuer `"Enigma"`** (emisión/validación de tokens). El codemod de namespaces usa patrones con contexto (ver Task 1, Step 4) que no tocan strings sueltos.
- CPM: `Directory.Packages.props` con `ManagePackageVersionsCentrally=true` y `CentralPackageTransitivePinningEnabled=true`. Ningún `PackageReference` lleva atributo `Version` en csproj (solo `PrivateAssets`/`IncludeAssets` permitidos).
- Puertos y config existentes NO cambian: Api `0.0.0.0:8081`, Web devserver `0.0.0.0:8080`, JWT issuer `Enigma` / audience `Enigma.Client`, `ENIGMA_JWT_SECRET`, `MYSQL_*`.
- Naming español y voseo en UI: no traducir nada existente.
- Lefthook `validate` (build Release `TreatWarningsAsErrors`) debe quedar verde desde Task 1 — hoy falla por NU1902/NU1903; el pinning CPM lo arregla.
- Cada tarea termina: build verde + `dotnet test` unit verde + commit(s) convencionales.
- Worktree/rama: se trabaja sobre `feat/monorepo-web-maui-api` (ya creada). Sin worktree adicional (consentimiento del usuario: rama dedicada).
- Los cambios SIN COMMITEAR del devcontainer (`.devcontainer/{devcontainer.json,devcontainer-lock.json,setup.sh,start-dev.sh,Dockerfile,compose.json}`) son WIP del usuario que este plan CONSUME en Task 4: al commitear Task 4 se incluyen esos archivos deliberadamente.

---

### Task 1: Renames src/tests + namespaces + CPM

**Files:**
- Move: `Server/` → `src/Enigma.Api/`, `Client/` → `src/Enigma.Web/`, `Shared/` → `src/Enigma.Shared/`, `Tests/` → `tests/Enigma.Tests/` (con `git mv`)
- Rename csproj: `Enigma.Server.csproj`→`Enigma.Api.csproj`, `Enigma.Client.csproj`→`Enigma.Web.csproj`, `Enigma.Test.csproj`→`Enigma.Tests.csproj`
- Modify: `Server/Dockerfile` (paths + dll), ambos `.csproj`, `Enigma.slnx`, todos los `.cs`/`.razor` con namespaces viejos, `Tests/E2E/*`, `Tests/stryker-config.json`, `Tests/*.sh`
- Create: `Directory.Build.props`, `Directory.Packages.props`, `global.json`

**Interfaces:**
- Consumes: nada (primer task).
- Produces: proyectos `Enigma.Api` (ns `Enigma.Api.*`, dll `Enigma.Api.dll`), `Enigma.Web` (ns `Enigma.Web.*`), `Enigma.Shared` (ns intacto), `Enigma.Tests`. CPM activo. El audience JWT sigue siendo literal `"Enigma.Client"`.

- [ ] **Step 1: git mv de carpetas**

```bash
mkdir -p src tests
git mv Server src/Enigma.Api
git mv Client src/Enigma.Web
git mv Shared src/Enigma.Shared
git mv Tests tests/Enigma.Tests
```

- [ ] **Step 2: renombrar csproj**

```bash
git mv src/Enigma.Api/Enigma.Server.csproj src/Enigma.Api/Enigma.Api.csproj
git mv src/Enigma.Web/Enigma.Client.csproj src/Enigma.Web/Enigma.Web.csproj
git mv tests/Enigma.Tests/Enigma.Test.csproj tests/Enigma.Tests/Enigma.Tests.csproj
```

- [ ] **Step 3: codemod namespaces (patrones con contexto — NO tocar literales JWT)**

```bash
# Enigma.Server -> Enigma.Api (qualified refs y declaraciones)
git grep -l 'Enigma\.Server' -- '*.cs' '*.razor' '*.csproj' '*.json' '*.sh' '*.yml' \
  | xargs sed -i -e 's/Enigma\.Server\./Enigma.Api./g' -e 's/\(namespace \|using \)Enigma\.Server/\1Enigma.Api/g' -e 's/<RootNamespace>Enigma\.Server</<RootNamespace>Enigma.Api</'
# Enigma.Client -> Enigma.Web (misma protección; el literal "Enigma.Client" sin punto/uso ns queda intacto)
git grep -l 'Enigma\.Client' -- '*.cs' '*.razor' '*.csproj' '*.json' '*.sh' '*.yml' '*.html' '*.js' \
  | xargs sed -i -e 's/Enigma\.Client\./Enigma.Web./g' -e 's/\(namespace \|using \)Enigma\.Client/\1Enigma.Web/g' -e 's/<RootNamespace>Enigma\.Client</<RootNamespace>Enigma.Web</'
```

Verificar que `TokenService`/validación JWT aún contienen `"Enigma.Client"` (audience) e `"Enigma"` (issuer): `git grep -n '"Enigma.Client"' src/Enigma.Api src/Enigma.Shared` debe seguir encontrándolos.

- [ ] **Step 4: csproj sin Versiones (CPM) + referencias a rutas nuevas**

En cada csproj: quitar atributos `Version="..."` de los `PackageReference` (los `Version` van a Packages.props), mantener `PrivateAssets`/`IncludeAssets`, y actualizar `ProjectReference`:

- `src/Enigma.Api/Enigma.Api.csproj`: `<ProjectReference Include="..\Enigma.Shared\Enigma.Shared.csproj" />`
- `src/Enigma.Web/Enigma.Web.csproj`: `<ProjectReference Include="..\Enigma.Shared\Enigma.Shared.csproj" />`
- `tests/Enigma.Tests/Enigma.Tests.csproj`: refs a `..\..\src\Enigma.Api\Enigma.Api.csproj`, `..\..\src\Enigma.Web\Enigma.Web.csproj`, `..\..\src\Enigma.Shared\Enigma.Shared.csproj`

- [ ] **Step 5: crear `global.json`**

```json
{
  "sdk": {
    "version": "10.0.*",
    "rollForward": "latestFeature"
  }
}
```

- [ ] **Step 6: crear `Directory.Build.props`**

```xml
<Project>
  <PropertyGroup>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
```

(Eliminar `<Nullable>`/`<ImplicitUsings>` duplicados de los 4 csproj existentes.)

- [ ] **Step 7: crear `Directory.Packages.props`**

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>
  </PropertyGroup>
  <ItemGroup>
    <!-- Server -->
    <PackageVersion Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.0.10" />
    <PackageVersion Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" Version="10.0.10" />
    <PackageVersion Include="Microsoft.AspNetCore.OpenApi" Version="10.0.11" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.0" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Tools" Version="10.0.0" />
    <PackageVersion Include="Scalar.AspNetCore" Version="2.16.18" />
    <!-- Pins de transitivos con CVE (MySqlConnector/Pkcs/Xml) -->
    <PackageVersion Include="System.Security.Cryptography.Pkcs" Version="10.0.14" />
    <PackageVersion Include="System.Security.Cryptography.Xml" Version="10.0.14" />
    <!-- Web / SharedUI -->
    <PackageVersion Include="Microsoft.AspNetCore.Components.Authorization" Version="10.0.9" />
    <PackageVersion Include="Microsoft.AspNetCore.Components.WebAssembly" Version="10.0.9" />
    <PackageVersion Include="Microsoft.AspNetCore.Components.WebAssembly.DevServer" Version="10.0.9" />
    <PackageVersion Include="Microsoft.AspNetCore.Components.Web" Version="10.0.9" />
    <!-- MAUI -->
    <PackageVersion Include="Microsoft.AspNetCore.Components.WebView.Maui" Version="10.0.120" />
    <PackageVersion Include="Microsoft.Extensions.Configuration.Json" Version="10.0.14" />
    <PackageVersion Include="Microsoft.Maui.Controls" Version="10.0.120" />
    <!-- Tests -->
    <PackageVersion Include="coverlet.collector" Version="10.0.1" />
    <PackageVersion Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.0" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="18.8.1" />
    <PackageVersion Include="Microsoft.Playwright" Version="1.52.0" />
    <PackageVersion Include="NUnit" Version="4.3.2" />
    <PackageVersion Include="NUnit3TestAdapter" Version="4.6.0" />
  </ItemGroup>
</Project>
```

`Microting.EntityFrameworkCore.MySql`: NO va en el primer intento de este archivo — se agrega vía `dotnet add src/Enigma.Api package Microting.EntityFrameworkCore.MySql` (trae la última 10.x estable y la escribe aquí). Luego `dotnet list src/Enigma.Api package --include-transitive | grep MySqlConnector`: si la versión transitiva sigue siendo 2.6.0, agregar `<PackageVersion Include="MySqlConnector" Version="<fix>" />` al pinning (la versión fix de GHSA-473q-m89c-ghf8, verificar en el advisory) y re-verificar.

- [ ] **Step 8: reescribir `Enigma.slnx`**

```xml
<Solution>
  <Folder Name="/src/Api/">
    <Project Path="src/Enigma.Api/Enigma.Api.csproj" />
  </Folder>
  <Folder Name="/src/Web/">
    <Project Path="src/Enigma.Web/Enigma.Web.csproj" />
  </Folder>
  <Folder Name="/src/SharedUI/">
    <Project Path="src/Enigma.SharedUI/Enigma.SharedUI.csproj" />
  </Folder>
  <Folder Name="/src/Maui/">
    <Project Path="src/Enigma.Maui/Enigma.Maui.csproj" />
  </Folder>
  <Folder Name="/src/Shared/">
    <Project Path="src/Enigma.Shared/Enigma.Shared.csproj" />
  </Folder>
  <Folder Name="/tests/">
    <Project Path="tests/Enigma.Tests/Enigma.Tests.csproj" />
  </Folder>
</Solution>
```

(SharedUI y Maui entran al slnx ya; se CREAN en Tasks 2-3. Si `dotnet build Enigma.slnx` falla por proyectos faltantes, comentar esas dos entradas `<Project/>` en Task 1 y descomentarlas en su task.)

- [ ] **Step 9: actualizar `src/Enigma.Api/Dockerfile`**

```dockerfile
# src/Enigma.Api/Dockerfile
# Build context: repo root (docker build -f src/Enigma.Api/Dockerfile .)

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/Enigma.Shared/Enigma.Shared.csproj src/Enigma.Shared/
COPY src/Enigma.Api/Enigma.Api.csproj src/Enigma.Api/
RUN dotnet restore src/Enigma.Api/Enigma.Api.csproj

COPY Directory.Build.props Directory.Build.props
COPY Directory.Packages.props Directory.Packages.props
COPY src/Enigma.Shared/ src/Enigma.Shared/
COPY src/Enigma.Api/ src/Enigma.Api/
RUN dotnet publish src/Enigma.Api/Enigma.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 8081
ENTRYPOINT ["dotnet", "Enigma.Api.dll"]
```

(NOTA: Kestrel ya escucha 8081 en el contenedor; `EXPOSE 8081` corrige el metadata.)

- [ ] **Step 10: verificar**

```bash
dotnet restore Enigma.slnx && dotnet build Enigma.slnx -c Release /p:TreatWarningsAsErrors=true   # 0 errores, 0 NU19xx
dotnet list Enigma.slnx package --vulnerable                                                      # SIN salida
dotnet test --no-build -c Release --filter "FullyQualifiedName!~E2E"                              # unit verde (excluye E2E que necesita MySQL)
```

- [ ] **Step 11: commit**

```bash
git add -A ':!docs/superpowers'
git commit -m "refactor!: monorepo src/tests con renames Enigma.Api/Enigma.Web + CPM"
```

---

### Task 2: Extracción Enigma.SharedUI (RCL)

**Files:**
- Create: `src/Enigma.SharedUI/Enigma.SharedUI.csproj`, `src/Enigma.SharedUI/EnigmaUIServiceCollectionExtensions.cs`
- Move (git mv desde `src/Enigma.Web/`): `App.razor`, `_Imports.razor`, `Pages/` (Login, SeleccionInstitucion, Home, NotFound, RedirectToLogin + `.razor.css`), `Layout/` (AuthLayout, MainLayout + `.razor.css`), `Shared/PanelVivo.razor` (+css), `Services/` (AuthService, CookieHandler, EnigmaAuthenticationStateProvider, ThemeService), `wwwroot/css/app.css`, `wwwroot/fonts/`, `wwwroot/js/login-motion.js`, `wwwroot/lib/bootstrap/`
- Modify: `src/Enigma.Web/Program.cs`, `src/Enigma.Web/wwwroot/index.html`, `src/Enigma.Web/wwwroot/service-worker.published.js`, `src/Enigma.Web/Enigma.Web.csproj`, `Enigma.slnx`, namespaces `Enigma.Web.*` → `Enigma.SharedUI.*` SOLO en archivos movidos

**Interfaces:**
- Consumes: proyecto `Enigma.Web` del Task 1 (ns `Enigma.Web`), CPM activo.
- Produces:
  - `namespace Enigma.SharedUI` con `public sealed class App : ComponentBase` (Router, `AppAssembly = typeof(App).Assembly`)
  - `namespace Enigma.SharedUI` con `public static class EnigmaUIServiceCollectionExtensions` y `public static IServiceCollection AddEnigmaSharedUI(this IServiceCollection services)`
  - `namespace Enigma.SharedUI.{Pages,Layout,Services,Shared}`
  - Assets en `_content/Enigma.SharedUI/{css/app.css,js/login-motion.js,fonts/,lib/bootstrap/}`
  - Web host: `Enigma.Web.styles.css` (bundle scoped css autogenerado del host WASM, incluye RCLs)

- [ ] **Step 1: crear la RCL**

```xml
<Project Sdk="Microsoft.NET.Sdk.Razor">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <SupportedPlatform Include="browser" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Components.Web" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\Enigma.Shared\Enigma.Shared.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: mover archivos (git mv) y ajustar namespaces**

```bash
cd src/Enigma.Web
mkdir -p ../Enigma.SharedUI
git mv App.razor _Imports.razor ../Enigma.SharedUI/
git mv Pages Layout Shared Services ../Enigma.SharedUI/
mkdir -p ../Enigma.SharedUI/wwwroot
git mv wwwroot/css wwwroot/fonts wwwroot/js ../Enigma.SharedUI/wwwroot/
git mv wwwroot/lib ../Enigma.SharedUI/wwwroot/lib
cd ../Enigma.SharedUI
git grep -l 'Enigma\.Web' -- '*.cs' '*.razor' \
  | xargs sed -i -e 's/Enigma\.Web\./Enigma.SharedUI./g' -e 's/\(namespace \|using \)Enigma\.Web/\1Enigma.SharedUI/g'
```

En `App.razor`: `AppAssembly="typeof(Program).Assembly"` → `AppAssembly="typeof(App).Assembly"`. `_Imports.razor`: los `@using Enigma.Web.*` ya quedaron como `Enigma.SharedUI.*` por el sed; eliminar `@using Enigma.Web` si quedó suelto.

- [ ] **Step 3: `EnigmaUIServiceCollectionExtensions.cs`**

```csharp
using Enigma.SharedUI.Services;
using Microsoft.AspNetCore.Components.Authorization;

namespace Enigma.SharedUI;

/// <summary>
/// Registro único de los servicios de UI compartidos. El host (Web o MAUI)
/// registra SU HttpClient (con CookieHandler y BaseAddress propia) ANTES de
/// llamar esta extensión.
/// </summary>
public static class EnigmaUIServiceCollectionExtensions
{
    public static IServiceCollection AddEnigmaSharedUI(this IServiceCollection services)
    {
        services.AddOptions();
        services.AddAuthorizationCore();
        services.AddScoped<EnigmaAuthenticationStateProvider>();
        services.AddScoped<AuthenticationStateProvider>(
            sp => sp.GetRequiredService<EnigmaAuthenticationStateProvider>());
        services.AddScoped<AuthService>();
        services.AddScoped<ThemeService>();
        services.AddScoped<CookieHandler>();
        return services;
    }
}
```

- [ ] **Step 4: `src/Enigma.Web/Program.cs` fino**

```csharp
using Enigma.SharedUI;
using Enigma.SharedUI.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<Enigma.SharedUI.App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// HttpClient con la cookie de sesión; la BaseAddress viene de wwwroot/appsettings.json
// (ServerUri). Los servicios compartidos de la RCL consumen este HttpClient.
builder.Services.AddScoped<CookieHandler>();
builder.Services.AddScoped(sp => new HttpClient(sp.GetRequiredService<CookieHandler>())
{
    BaseAddress = new Uri(builder.Configuration["ServerUri"] ?? builder.HostEnvironment.BaseAddress)
});

builder.Services.AddEnigmaSharedUI();

await builder.Build().RunAsync();

// Requerido por WebApplicationFactory<T> en los tests.
public partial class Program { }
```

- [ ] **Step 5: `src/Enigma.Web/wwwroot/index.html`**

Reemplazar los links de assets por las rutas `_content/`:

- `href="lib/bootstrap/dist/css/bootstrap.min.css"` → `href="_content/Enigma.SharedUI/lib/bootstrap/dist/css/bootstrap.min.css"`
- `href="css/app.css"` → `href="_content/Enigma.SharedUI/css/app.css"`
- `href="Enigma.Client.styles.css"` → `href="Enigma.Web.styles.css"` (bundle autogenerado del host; el sed del codemod ya lo habrá dejado en `Enigma.Web.styles.css` si contenía el punto — verificarlo)
- `src="js/login-motion.js"` (si index.html lo referencia) → `src="_content/Enigma.SharedUI/js/login-motion.js"`
- Fuentes `fonts/manrope-*.woff2` referenciadas desde `css/app.css`: verificar que `app.css` usa rutas relativas (`../fonts/`) — si usa absolutas `/fonts/`, cambiar a relativas para que `_content/` funcione.

- [ ] **Step 6: `service-worker.published.js` (precache)**

Actualizar el array `cache.addAll([...])`:

```js
await cache.addAll([
    './',
    'index.html',
    '_content/Enigma.SharedUI/css/app.css',
    '_content/Enigma.SharedUI/lib/bootstrap/dist/css/bootstrap.min.css',
    'Enigma.Web.styles.css',
    'images/logo_size.jpg',
    'images/logo_size_invert.png',
    'images/logo_size.svg',
    'images/logo_size_invert.svg',
    'images/icon.svg',
    'images/icon-invert.svg',
    'manifest.webmanifest',
    '_content/Enigma.SharedUI/js/login-motion.js',
    '_content/Enigma.SharedUI/fonts/manrope-latin.woff2'
]);
```

- [ ] **Step 7: slnx ya referencia SharedUI (descomentar si Task 1 lo comentó). Verificar**

```bash
dotnet build Enigma.slnx -c Release /p:TreatWarningsAsErrors=true   # verde
dotnet test --no-build -c Release --filter "FullyQualifiedName!~E2E"   # unit verde
git grep -n 'Enigma\.Client' -- 'src' ':!*/wwwroot/*'               # sin residuos de ns (excepto literales JWT)
```

- [ ] **Step 8: commit**

```bash
git add -A ':!docs/superpowers'
git commit -m "refactor(ui): extraer Enigma.SharedUI RCL con servicios y assets compartidos"
```

---

### Task 3: Host Enigma.Maui (Blazor Hybrid)

**Files:**
- Create: `src/Enigma.Maui/Enigma.Maui.csproj`, `MauiProgram.cs`, `App.xaml`, `App.xaml.cs`, `MainPage.xaml`, `MainPage.xaml.cs`, `wwwroot/index.html`, `appsettings.json`, `Platforms/Android/AndroidManifest.xml`, `Platforms/Android/MainActivity.cs`, `Platforms/Android/MainApplication.cs`, `Platforms/Windows/app.manifest`
- Modify: `Enigma.slnx` (entrada Maui ya presente)

**Interfaces:**
- Consumes: `Enigma.SharedUI` (Task 2): `App`, `AddEnigmaSharedUI()`, `CookieHandler`.
- Produces: app MAUI Android (TFM `net10.0-android`, buildable en Linux con workload) y Windows (`net10.0-windows10.0.19041.0` solo en hosts Windows; `-p:AndroidOnly=true` recorta a Android). `MauiProgram.CreateMauiApp()`.

- [ ] **Step 1: `Enigma.Maui.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk.Razor">
  <!-- MAUI Blazor Hybrid: aloja los mismos componentes de la RCL en WebView nativa. -->
  <PropertyGroup>
    <TargetFrameworks>net10.0-android</TargetFrameworks>
    <TargetFrameworks Condition="$([MSBuild]::IsOSPlatform('windows'))">$(TargetFrameworks);net10.0-windows10.0.19041.0</TargetFrameworks>
    <OutputType>Exe</OutputType>
    <UseMaui>true</UseMaui>
    <SingleProject>true</SingleProject>
    <ApplicationId>com.enigma.app</ApplicationId>
    <ApplicationTitle>Enigma</ApplicationTitle>
    <ApplicationDisplayVersion>1.0</ApplicationDisplayVersion>
    <ApplicationVersion>1</ApplicationVersion>
    <SupportedOSPlatformVersion Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'android'">24</SupportedOSPlatformVersion>
    <SupportedOSPlatformVersion Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'windows'">10.0.17763.0</SupportedOSPlatformVersion>
    <TargetPlatformMinVersion Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'windows'">10.0.17763.0</TargetPlatformMinVersion>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Components.WebView.Maui" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Json" />
    <PackageReference Include="Microsoft.Maui.Controls" />
  </ItemGroup>
  <ItemGroup>
    <EmbeddedResource Include="appsettings.json" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\Enigma.Shared\Enigma.Shared.csproj" />
    <ProjectReference Include="..\Enigma.SharedUI\Enigma.SharedUI.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: `MauiProgram.cs`**

```csharp
using Enigma.SharedUI;
using Enigma.SharedUI.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Maui.Storage;

namespace Enigma.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        // appsettings.json embebido: Api:BaseUrl = http://10.0.2.2:8081 (loopback del
        // host visto desde el emulador Android). Dispositivo físico: IP LAN.
        ConfigurationManager config = new();
        using var stream = FileSystem.OpenAppPackageFileAsync("appsettings.json").GetAwaiter().GetResult();
        config.AddJsonStream(stream);

        MauiAppBuilder builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();
        builder.Services.AddMauiBlazorWebView();
#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
#endif
        builder.Services.AddSingleton<IConfiguration>(config);

        // HttpClient con cookie de sesión hacia la Api; luego los servicios de la RCL.
        builder.Services.AddScoped<CookieHandler>();
        builder.Services.AddScoped(sp => new HttpClient(sp.GetRequiredService<CookieHandler>())
        {
            BaseAddress = new Uri(config["Api:BaseUrl"] ?? "http://10.0.2.2:8081")
        });
        builder.Services.AddEnigmaSharedUI();

        return builder.Build();
    }
}
```

- [ ] **Step 3: `App.xaml` + `App.xaml.cs` + `MainPage.xaml` + `MainPage.xaml.cs`**

`App.xaml`:

```xml
<Application xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             x:Class="Enigma.Maui.App">
    <Application.Resources>
        <ResourceDictionary />
    </Application.Resources>
</Application>
```

`App.xaml.cs`:

```csharp
namespace Enigma.Maui;

public partial class App : Application
{
    public App() => InitializeComponent();

    protected override Window CreateWindow(IActivationState? activationState)
        => new Window(new MainPage());
}
```

`MainPage.xaml`:

```xml
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             x:Class="Enigma.Maui.MainPage"
             xmlns:b="clr-namespace:Microsoft.AspNetCore.Components.WebView.Maui;assembly=Microsoft.AspNetCore.Components.WebView.Maui"
             xmlns:sharedUI="clr-namespace:Enigma.SharedUI;assembly=Enigma.SharedUI"
             NavigationPage.HasNavigationBar="False">
    <b:BlazorWebView x:Name="Blazor" HostPage="wwwroot/index.html">
        <b:BlazorWebView.RootComponents>
            <b:RootComponent Selector="#app" ComponentType="{x:Type sharedUI:App}" />
        </b:BlazorWebView.RootComponents>
    </b:BlazorWebView>
</ContentPage>
```

`MainPage.xaml.cs`:

```csharp
namespace Enigma.Maui;

public partial class MainPage : ContentPage
{
    public MainPage() => InitializeComponent();
}
```

- [ ] **Step 4: `wwwroot/index.html`** (shell mínimo con tema pre-render)

```html
<!DOCTYPE html>
<html lang="es">

<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>Enigma</title>
    <base href="/" />
    <link rel="stylesheet" href="_content/Enigma.SharedUI/lib/bootstrap/dist/css/bootstrap.min.css" />
    <link rel="stylesheet" href="_content/Enigma.SharedUI/css/app.css" />
    <!-- Scoped CSS de la RCL: en BlazorWebView NO se empaqueta en el host (lección Build-It). -->
    <link rel="stylesheet" href="_content/Enigma.SharedUI/Enigma.SharedUI.bundle.scp.css" />
    <script>
        // Tema antes del primer render (idéntico al shell web).
        (function () {
            var t = localStorage.getItem('enigma_theme');
            if (t !== 'light' && t !== 'dark') {
                t = window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
            }
            document.documentElement.setAttribute('data-theme', t);
        })();
    </script>
</head>

<body>
    <div id="app"></div>
    <script src="_framework/blazor.webview.js" autostart="false"></script>
</body>

</html>
```

- [ ] **Step 5: `appsettings.json`**

```json
{
  "Api": {
    "BaseUrl": "http://10.0.2.2:8081"
  }
}
```

- [ ] **Step 6: Platforms**

`Platforms/Android/AndroidManifest.xml`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<manifest xmlns:android="http://schemas.android.com/apk/res/android">
    <application android:allowBackup="true" android:icon="@mipmap/ic_launcher"
                 android:label="Enigma" android:theme="@style/Maui.SplashTheme" />
    <uses-permission android:name="android.permission.INTERNET" />
</manifest>
```

`Platforms/Android/MainApplication.cs`:

```csharp
using Android.App;
using Android.Runtime;

namespace Enigma.Maui.Platforms.Android;

[Application(Label = "Enigma")]
public class MainApplication(IntPtr handle, JniHandleOwnership ownership)
    : MauiApplication(handle, ownership)
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
```

`Platforms/Android/MainActivity.cs`:

```csharp
using Android.App;
using Android.Content.PM;

namespace Enigma.Maui.Platforms.Android;

[Activity(Label = "Enigma", Theme = "@style/Maui.SplashTheme",
          MainLauncher = true,
          ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation |
                                 ConfigChanges.UiMode | ConfigChanges.ScreenLayout |
                                 ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
}
```

`Platforms/Windows/app.manifest`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
  <assemblyIdentity version="1.0.0.0" name="Enigma.Maui.app" />
</assembly>
```

(Nota: `@mipmap/ic_launcher`/`Maui.SplashTheme` los genera el build MAUI con recursos por defecto si el csproj no define `<MauiIcon>`; si el build exige recursos, crear `Resources/AppIcon/appicon.svg` con el monograma E y `<MauiIcon Include="Resources\AppIcon\appicon.svg" />` en el csproj.)

- [ ] **Step 7: instalar workload Android y verificar**

```bash
dotnet workload install maui-android
dotnet build src/Enigma.Maui -p:AndroidOnly=true -c Release /p:TreatWarningsAsErrors=true   # verde
dotnet build Enigma.slnx -c Release /p:TreatWarningsAsErrors=true                            # verde (slnx completo)
dotnet test --no-build -c Release --filter "FullyQualifiedName!~E2E"                          # unit verde
```

- [ ] **Step 8: commit**

```bash
git add -A ':!docs/superpowers'
git commit -m "feat(maui): host Blazor Hybrid Android+Windows sobre SharedUI"
```

---

### Task 4: CI, deploy y devcontainer

**Files:**
- Create: `Enigma.Core.slnf`, `.github/workflows/build-maui.yml`, `.github/workflows/deploy-web.yml`, `deploy/docker-compose.yml`, `deploy/Caddyfile`
- Modify: `.github/workflows/build-server-image.yml` (context/file paths), `.github/workflows/pr-validation.yml` (build slnf), `.devcontainer/start-dev.sh`, `.devcontainer/setup.sh`, `src/Enigma.Api/Program.cs` (ForwardedHeaders + CORS por config), `src/Enigma.Api/Properties/launchSettings.json` (si referencia `ASPNETCORE_URLS` — no cambiar puertos)
- Delete: `.github/workflows/build-client-image.yml`, `src/Enigma.Web/Dockerfile`

**Interfaces:**
- Consumes: `src/Enigma.Api/Dockerfile` (Task 1), slnx completo (Task 3).
- Produces: workflows nuevos; `Enigma.Core.slnf` (Api+Web+SharedUI+Shared+Tests); Api lee `ENIGMA_CORS_ORIGINS` (coma-separada) con default dev `http://localhost:8080,https://localhost:8080`.

- [ ] **Step 1: `Enigma.Core.slnf`**

```json
{
  "solution": {
    "path": "Enigma.slnx",
    "projects": [
      "src/Enigma.Shared/Enigma.Shared.csproj",
      "src/Enigma.SharedUI/Enigma.SharedUI.csproj",
      "src/Enigma.Api/Enigma.Api.csproj",
      "src/Enigma.Web/Enigma.Web.csproj",
      "tests/Enigma.Tests/Enigma.Tests.csproj"
    ]
  }
}
```

Verificar: `dotnet build Enigma.Core.slnf -c Release` verde en Linux SIN workload MAUI (en este entorno el workload ya está instalado del Task 3 — validar igualmente que el filtro NO incluye `Enigma.Maui.csproj`).

- [ ] **Step 2: Api — ForwardedHeaders + CORS por configuración**

En `Program.cs`, ANTES de `app.UseCors()` (y tras `app.Build()`), insertar:

```csharp
// Detrás de Caddy (proxy TLS): honrar X-Forwarded-* para scheme/host correctos.
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});
```

Reemplazar la política CORS existente por orígenes desde config:

```csharp
// Orígenes del frontend: dev por defecto; en prod, ENIGMA_CORS_ORIGINS (coma-separada,
// ej: https://enigma.pages.dev).
string[] corsOrigins = (Environment.GetEnvironmentVariable("ENIGMA_CORS_ORIGINS")
        ?? "http://localhost:8080,https://localhost:8080")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod());
});
```

y `app.UseCors("Frontend")`. Eliminar la política permisiva dev anterior. Ajustar tests que referencien la política vieja si los hay.

- [ ] **Step 3: `deploy/docker-compose.yml`**

```yaml
# Stack de producción en la VPS: Api (imagen GHCR) + MySQL + Caddy (TLS).
# Deploy manual por ahora: docker compose pull && docker compose up -d
services:
  api:
    image: ghcr.io/agustin-gigena/enigma-server:latest
    restart: unless-stopped
    environment:
      MYSQL_HOST: db
      MYSQL_PORT: "3306"
      MYSQL_DATABASE: ${MYSQL_DATABASE}
      MYSQL_USER: ${MYSQL_USER}
      MYSQL_PASSWORD: ${MYSQL_PASSWORD}
      ENIGMA_JWT_SECRET: ${ENIGMA_JWT_SECRET}
      ENIGMA_AUTH_REQUIRED: "true"
      ENIGMA_CORS_ORIGINS: ${ENIGMA_CORS_ORIGINS}
    depends_on:
      - db
    expose:
      - "8081"

  db:
    image: mysql:8.0
    restart: unless-stopped
    environment:
      MYSQL_DATABASE: ${MYSQL_DATABASE}
      MYSQL_USER: ${MYSQL_USER}
      MYSQL_PASSWORD: ${MYSQL_PASSWORD}
      MYSQL_ROOT_PASSWORD: ${MYSQL_ROOT_PASSWORD}
    volumes:
      - db-data:/var/lib/mysql

  caddy:
    image: caddy:2-alpine
    restart: unless-stopped
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - ./Caddyfile:/etc/caddy/Caddyfile:ro
      - caddy-data:/data
      - caddy-config:/config
    depends_on:
      - api

volumes:
  db-data:
  caddy-data:
  caddy-config:
```

- [ ] **Step 4: `deploy/Caddyfile`**

```
# Reverse proxy TLS para la Api (Kestrel ya escucha :8081 en la red interna).
# Caddy emite y renueva el certificado automáticamente.
{$ENIGMA_API_DOMAIN} {
    reverse_proxy api:8081
}
```

- [ ] **Step 5: `build-maui.yml`**

```yaml
name: Build MAUI

on:
  workflow_dispatch:

permissions:
  contents: read

jobs:
  android:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: ./.github/actions/setup-dotnet
      - name: Install MAUI Android workload
        run: dotnet workload install maui-android
      - name: Build APK
        run: >
          dotnet build src/Enigma.Maui -p:AndroidOnly=true -c Release
          -f net10.0-android
      - name: Copy APK artifact
        run: |
          mkdir -p dist
          find src/Enigma.Maui/bin -name '*-Signed.apk' -exec cp {} dist/ \;
      - uses: actions/upload-artifact@v4
        with:
          name: enigma-android-apk
          path: dist/*.apk

  windows:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - uses: ./.github/actions/setup-dotnet
      - name: Install MAUI Windows workload
        run: dotnet workload install maui-windows
      - name: Publish self-contained (Windows)
        run: >
          dotnet publish src/Enigma.Maui -c Release
          -f net10.0-windows10.0.19041.0
          -p:RuntimeIdentifierOverride=win10-x64 -p:WindowsAppSDKSelfContained=true
          -o dist/win
      - uses: actions/upload-artifact@v4
        with:
          name: enigma-windows
          path: dist/win/
```

(Si `./.github/actions/setup-dotnet` no existe como composite action, verificar el nombre real en `.github/actions/` y ajustar; pr-validation ya lo usa.)

- [ ] **Step 6: `deploy-web.yml`**

```yaml
name: Deploy Web (Cloudflare Pages)

on:
  workflow_dispatch:

permissions:
  contents: read
  deployments: write

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: ./.github/actions/setup-dotnet
      - name: Publish WASM
        run: dotnet publish src/Enigma.Web -c Release -o dist
      # Lección Build-It #2: el bundle scoped CSS de la RCL sale con hash y el SDK
      # no reescribe la referencia en el host. Verificar y reescribir si hace falta.
      - name: Fix hashed bundle css reference (if needed)
        run: |
          cd dist/wwwroot
          hashed=$(ls _content/Enigma.SharedUI/*.bundle.scp.css 2>/dev/null | head -1 || true)
          if [ -n "$hashed" ]; then
            sed -i "s|_content/Enigma.SharedUI/Enigma.SharedUI.bundle.scp.css|$(basename "$hashed")|g" index.html
          fi
      - name: Deploy to Cloudflare Pages
        uses: cloudflare/wrangler-action@v3
        with:
          wranglerVersion: "4"
          apiToken: ${{ secrets.CLOUDFLARE_API_TOKEN }}
          accountId: ${{ secrets.CLOUDFLARE_ACCOUNT_ID }}
          command: pages deploy dist/wwwroot --project-name enigma
```

- [ ] **Step 7: workflows existentes**

- `build-server-image.yml`: `context: .`, `file: src/Enigma.Api/Dockerfile`.
- `pr-validation.yml`: el job `validate` buildea `Enigma.Core.slnf` en lugar de `Enigma.slnx`.
- **Eliminar** `build-client-image.yml` y `src/Enigma.Web/Dockerfile` (la Web es estática en Pages).

- [ ] **Step 8: devcontainer/scripts (incluye WIP sin commitear del usuario, ver Global Constraints)**

- `.devcontainer/setup.sh`: agregar instalación del workload Android si falta (`dotnet workload list | grep maui-android || dotnet workload install maui-android`) en la sección de tools.
- `.devcontainer/start-dev.sh`: reemplazar `--project Server` → `--project src/Enigma.Api` y `--project Client` → `--project src/Enigma.Web` en los `dotnet watch` y los `pkill -f` correspondientes.
- `scripts/validate.sh` y `scripts/free-ports.sh`: verificar/actualizar referencias a `Server/`/`Client/` si existen.
- `lefthook.yml`: sin cambios de contenido (buildea `Enigma.slnx`, ahora verde con CPM).

- [ ] **Step 9: verificar**

```bash
dotnet build Enigma.Core.slnf -c Release      # verde
dotnet build Enigma.slnx -c Release /p:TreatWarningsAsErrors=true   # verde (lefthook OK de acá en más)
dotnet test --no-build -c Release --filter "FullyQualifiedName!~E2E"
```

- [ ] **Step 10: commit (incluye WIP devcontainer del usuario)**

```bash
git add -A ':!docs/superpowers'
git commit -m "feat(ci): deploy api-vps docker+caddy, web-pages estático, artifacts maui; slnf core"
```

---

### Task 5: Cutover — E2E, vulnerables, residuos, docs

**Files:**
- Modify: `AGENTS.md`, `README.md`, `PRODUCT.md` (Operating Context), `DESIGN.md` (solo si menciona rutas de assets del Client)
- Verify: E2E completo, `--vulnerable` vacío, grep de residuos

**Interfaces:**
- Consumes: todo lo anterior.
- Produces: rama lista para PR; docs consistentes con la estructura nueva.

- [ ] **Step 1: levantar MySQL y correr TODA la suite (unit + E2E)**

```bash
bash .devcontainer/start-dev.sh   # en detached trae MySQL + watchers; o el stack dev que el usuario ya usa
dotnet test -c Release            # 59/59 esperados (unit + 6 E2E Playwright)
```

Si Playwright browsers faltan: `agent-browser install --with-deps` (o `pwsh bin/Debug/net10.0/playwright.ps1 install`).

- [ ] **Step 2: gates de seguridad y residuos**

```bash
dotnet list Enigma.slnx package --vulnerable        # SIN salida
git grep -n 'Enigma\.Server' -- '*.cs' '*.razor'    # vacío
git grep -n 'Enigma\.Client' -- 'src' 'tests'       # solo el literal audience JWT "Enigma.Client"
git grep -rn 'Client/' 'Server/' -- '*.yml' '*.sh' 'Dockerfile' 'Dockerfile*'   # sin referencias viejas en CI/scripts
```

- [ ] **Step 3: docs**

- `AGENTS.md`: actualizar Key Directories, Commands (`dotnet watch --project src/Enigma.Api` / `src/Enigma.Web`), Architecture (agregar hosts Web/Maui + SharedUI y la regla de dependencias), Tests (`tests/Enigma.Tests`), CI (workflows nuevos, slnf, eliminación de client image), Notes (regla "nadie referencia Api", deploy targets).
- `README.md`: rutas de proyectos y comandos con `src/`/`tests/`.
- `PRODUCT.md` (Operating Context): "Cliente Blazor WASM" → "Web (Blazor WASM, Cloudflare Pages) + MAUI (Android/Windows)"; deploy VPS Docker+Caddy.
- `DESIGN.md`: solo referencias a rutas si las hay (`Client/wwwroot/...` → `src/Enigma.SharedUI/wwwroot/...`).

- [ ] **Step 4: commit final**

```bash
git add AGENTS.md README.md PRODUCT.md DESIGN.md
git commit -m "docs: actualizar docs al monorepo web+maui+api"
```

---

## Self-Review (hecho al escribir)

- **Cobertura del spec:** §2 estructura → T1; §3 SharedUI → T2; §4 hosts → T2/T3; §4 Api ForwardedHeaders/CORS → T4; §5 tests → T1/T2/T5; §6 deploy → T4; §7 tooling → T4; §8 riesgos → verificación T5; §9 orden = T1..T5. ✔
- **Placeholders:** ninguno; versiones exactas y código completo. Únicas decisiones runtime con árbol de decisión explícito: versión latest de Microting (T1 S7), recursos MauiIcon (T3 S6), nombre del composite setup-dotnet (T4 S5).
- **Consistencia de tipos:** `AddEnigmaSharedUI` (T2) consumida igual en T3; `Enigma.Web.styles.css` consistente entre T2 S5/S6; audience `"Enigma.Client"` protegida por patrones de codemod con contexto.

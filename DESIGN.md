---
name: Enigma
description: Plataforma educativa sobre MudBlazor 9 (Material Design) — jade institucional como seed de la paleta, componentes MudBlazor como única superficie de UI.
colors:
  # Los valores canónicos viven en Client/Themes/TemaEnigma.cs (MudTheme con
  # PaletteLight + PaletteDark). Este frontmatter documenta el seed; el CSS
  # propio restante se alimenta de las variables --mud-palette-*.
  primary: "#14665C"
  primary-dark-theme: "#2BB09E"
  on-primary-dark-theme: "#06201E"
  secondary: "#0E3736"
  tertiary: "#259585"
  background-light: "#F3F8F7"
  background-dark: "#081D1C"
  surface-light: "#FFFFFF"
  surface-dark: "#102E2C"
  appbar-dark: "#0C2524"
  text-primary-dark: "#E4F1EF"
  text-secondary-dark: "#A9C3BF"
  error: "#B3261E"
typography:
  display:
    fontFamily: "'Manrope', 'Roboto', system-ui, sans-serif"
    fontWeight: 800
  body:
    fontFamily: "'Manrope', 'Roboto', system-ui, sans-serif"
    fontWeight: 400
rounded:
  base: "10px"
components:
  # Todos los componentes son MudBlazor; la configuración vive en TemaEnigma.cs.
  source: "MudBlazor 9.9 + CodeBeam.MudBlazor.Extensions 9.1"
  buttons: "MudButton / MudLoadingButton (Variant.Filled | Text, Color.Primary)"
  tables: "MudTable + MudSkeleton en LoadingContent"
  dialogs: "MudDialog vía IDialogService (DialogoInstitucion, DialogoAltaUsuario, DialogoRolesUsuario, DialogoConfirmacion)"
  feedback: "ServicioNotificaciones sobre ISnackbar (única vía de feedback)"
  menus: "MudMenu (módulos, institución activa, cuenta)"
  fields: "MudTextField / MudSelect / MudPasswordField (MudExtensions)"
  loading: "MudLoading (MudExtensions) + MudLoadingButton para submits"
---

# Design System: Enigma (MudBlazor-centric)

## Overview

**Creative North Star: "Material Jade"**

Enigma es una app **MudBlazor**. El sistema de diseño ES MudBlazor 9 (Material Design): sus componentes, su paleta, su tipografía, su elevación y sus estados son la única superficie de UI. La identidad de la marca (jade institucional) entra como **seed de la paleta** en `Client/Themes/TemaEnigma.cs` — un solo archivo que configura el `MudTheme` para ambos modos — y en la tipografía display (Manrope) configurada vía `Typography` del mismo MudTheme. Nada de la UI se estiliza por fuera: si MudBlazor tiene un componente, se usa el componente; si falta un color, se agrega a la paleta; si falta un comportamiento, se configura el tema.

El CSS propio (`app.css` + sidecars) queda reducido a tres dominios legítimos: **layout de página** (anchos, grillas, split del login), **momentos de marca** (PanelVivo, panel de identidad, splash de carga) y **armonización menor** (densidad de tablas). Cualquier estilo que imite o duplique un componente MudBlazor es una violación del sistema.

**Key Characteristics:**
- MudBlazor 9 es el sistema: componentes, tokens (`--mud-palette-*`), elevación, estados, motion.
- La paleta se define UNA vez en `TemaEnigma.cs` (claro + oscuro); el modo oscuro del tema usa elevación real (background < drawer/appbar < surface) y primary #2BB09E con texto tinta #06201E encima (AA ≈ 5:1).
- Iconografía: `Icons.Material.*` (SVG inline de MudBlazor) — sin fuentes de iconos.
- Feedback SIEMPRE por `ServicioNotificaciones` (snackbars); confirmaciones/destrucciones por `DialogoConfirmacion` vía `DialogService`.
- Ambos modos siempre: `ThemeService` puentea `MudThemeProvider` ↔ `data-theme` (para el CSS propio restante) con persistencia en localStorage y script pre-render sin flash.
- Voseo rioplatense imperativo en toda la UI ("Ingresá", "Elegí", "Contactá").

## Colors

Los roles de color son los de la paleta MudBlazor (`Primary`, `Secondary`, `Tertiary`, `Background`, `Surface`, `TextPrimary`/`TextSecondary`, `ActionDefault`, `Divider`, `Success`, `Error`…). El seed jade:

- **Claro:** Primary `#14665C` (texto blanco encima ≈ 6.6:1 AA), Background `#F3F8F7`, Surface blanco.
- **Oscuro:** Primary `#2BB09E` (el contraste del texto encima lo computa MudBlazor; el OnPrimary efectivo es tinta oscura ≈ 5:1), Background `#081D1C`, Surface `#102E2C`, Appbar/Drawer `#0C2524` — elevación real entre capas.

**La Regla del Seed Único.** Un color nuevo NO se escribe en CSS: se agrega/ajusta en `TemaEnigma.cs` y se consume como componente o `var(--mud-palette-*)`. Los tokens `--enigma-*` sobreviven solo para el layout y la marca.

## Typography

Configurada en `TemaEnigma.cs` → `Typography`: **Manrope** (autohosteada) en `Default` y todos los roles display; el resto de la jerarquía (h1–h6, button, body) la maneja MudBlazor. La tipografía NO se declara en CSS de página.

## Components

**Todo componente visual es un componente MudBlazor** (o MudExtensions):

| Dominio | Componente |
|---|---|
| Shell | `MudLayout`, `MudAppBar`, `MudSpacer`, `MudMainContent` |
| Navegación/menús | `MudMenu` + `MudMenuItem` (módulos, institución activa, cuenta) |
| Tablas | `MudTable` (+ `LoadingContent` con `MudSkeleton`) |
| Formularios | `MudForm`, `MudTextField`, `MudSelect`, `MudPasswordField` (CodeBeam), `MudLoadingButton` (CodeBeam) para submits |
| Diálogos | `MudDialog` vía `IDialogService` (`DialogoInstitucion`, `DialogoAltaUsuario`, `DialogoRolesUsuario`, `DialogoConfirmacion`) |
| Feedback | `MudSnackbarProvider` vía `ServicioNotificaciones`; errores inline `MudAlert` |
| Chips/roles | `MudChip`; selector de roles: `ChipRoles` con `aria-pressed` |
| Estados | `MudLoading` (CodeBeam, `LoaderType`), `MudSkeleton`, `MudProgressLinear` |
| Iconos | `Icons.Material.*` vía `MudIcon`/`MudIconButton`, con `MudTooltip` |

**La Regla del Componente Primero.** Antes de escribir CSS: ¿MudBlazor tiene esto? Úsalo con sus parámetros (`Variant`, `Color`, `Elevation`, `Dense`). CSS propio solo para layout de página y marca.

**La Regla del Feedback Centralizado.** Todo resultado de operación se notifica por `ServicioNotificaciones` (`Exito`/`Error`/`ErrorAsync` leyendo el cuerpo de error de la API). Ninguna página arma carteles propios.

## Motion

- MudBlazor aporta las transiciones de componentes (popover, dialog, snackbar).
- Momentos autoriales de marca: entrada `rise` escalonada en auth (CSS, curva `var(--enigma-ease)`) y vida ambiental de `PanelVivo` — bajo `prefers-reduced-motion: no-preference`, siempre.
- GSAP permanece para la transición de vista (`VistaEntradaAsync` en `MainLayout`) y la entrada escalonada de filas.

## Do's and Don'ts

### Do:
- **Do** consumir colores por componente o `var(--mud-palette-*)`; los nuevos colores entran por `TemaEnigma.cs`.
- **Do** usar `Icons.Material.*` para toda iconografía.
- **Do** pasar ganchos de test como `Class="..."` sin estilos propios (los estilos los pone MudBlazor).
- **Do** escribir la UI en voseo rioplatense imperativo.
- **Do** mantener AA en ambos modos: dark usa primary claro + tinta oscura en fills.

### Don't:
- **Don't** duplicar en CSS un componente que MudBlazor ya provee (botones, chips, campos, tablas, menús, diálogos).
- **Don't** hardcodear colores en scoped CSS — ni siquiera "provisoriamente".
- **Don't** notificar fuera de `ServicioNotificaciones`.
- **Don't** romper el puente de tema (`ThemeService` ↔ `MudThemeProvider` ↔ `data-theme`): un solo modo sin el otro es una regresión crítica.
- **Don't** introducir dependencias de UI que no sean MudBlazor/MudExtensions.

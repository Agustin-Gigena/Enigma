using MudBlazor;

namespace Enigma.Client.Themes;

/// <summary>
/// Tema Enigma sobre el sistema Material de MudBlazor.
/// - Claro: jade profundo #14665C como primary (texto blanco encima ≈ 6.6:1, AA).
/// - Oscuro: jade claro #2BB09E como primary con tinta oscura encima (≈ 5:1, AA);
///   escala de superficies con elevación real (background &lt; drawer/appbar &lt; surface)
///   para que la barra y las tarjetas se distingan del fondo (corrección del
///   dark plano del sistema anterior).
/// Tipografía de marca Manrope en todos los roles.
/// </summary>
public static class TemaEnigma
{
    public static MudTheme Instancia { get; } = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#14665C",
            PrimaryContrastText = "#FFFFFF",
            Secondary = "#0E3736",
            SecondaryContrastText = "#FFFFFF",
            Tertiary = "#259585",
            TertiaryContrastText = "#FFFFFF",
            Background = "#F3F8F7",
            Surface = "#FFFFFF",
            DrawerBackground = "#FFFFFF",
            AppbarBackground = "#FFFFFF",
            AppbarText = "#10201F",
            ActionDefault = "#37504C",
            ActionDisabled = "#9DB4B0",
            Divider = "#C7D8D5",
            DividerLight = "#E1EBE9",
            Success = "#1E7F4C",
            Info = "#0E5A8A",
            Warning = "#8F6400",
            Error = "#B3261E"
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#2BB09E",
            PrimaryContrastText = "#06201E",
            TextPrimary = "#E4F1EF",
            TextSecondary = "#A9C3BF",
            TextDisabled = "#6F8B87",
            Background = "#081D1C",
            Surface = "#102E2C",
            DrawerBackground = "#0C2524",
            AppbarBackground = "#0C2524",
            AppbarText = "#E4F1EF",
            ActionDefault = "#A9C3BF",
            ActionDisabled = "#54706C",
            Divider = "#2E605B",
            DividerLight = "#1E4441",
            Success = "#54C08A",
            Info = "#5BA8D4",
            Warning = "#D9A93F",
            Error = "#E5726B"
        },
        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = ["Manrope", "Roboto", "system-ui", "sans-serif"]
            },
            H1 = new H1Typography
            {
                FontFamily = ["Manrope", "Roboto", "system-ui", "sans-serif"]
            },
            H2 = new H2Typography
            {
                FontFamily = ["Manrope", "Roboto", "system-ui", "sans-serif"]
            },
            H3 = new H3Typography
            {
                FontFamily = ["Manrope", "Roboto", "system-ui", "sans-serif"]
            },
            H4 = new H4Typography
            {
                FontFamily = ["Manrope", "Roboto", "system-ui", "sans-serif"]
            },
            H5 = new H5Typography
            {
                FontFamily = ["Manrope", "Roboto", "system-ui", "sans-serif"]
            },
            H6 = new H6Typography
            {
                FontFamily = ["Manrope", "Roboto", "system-ui", "sans-serif"]
            },
            Button = new ButtonTypography
            {
                FontFamily = ["Manrope", "Roboto", "system-ui", "sans-serif"]
            }
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "10px"
        },
        Shadows = new Shadow(),
        ZIndex = new ZIndex()
    };
}

---
name: Enigma
description: Sistema de identidad institucional para plataformas educativas — petrol-jade, ambos modos siempre.
colors:
  # Valores canónicos = modo claro (bloque :root de app.css); cada rol tiene su par oscuro en el sidecar (.impeccable/design.json).
  accent: "#14665C"
  accent-hover: "#0F524A"
  accent-soft: "rgba(20, 102, 92, 0.10)"
  brand-accent: "#4FD1BE"
  bg: "#F3F8F7"
  surface: "#FFFFFF"
  ink: "#123E3C"
  ink-2: "#43605D"
  ink-3: "#64807D"
  border: "#D5E2E0"
  error: "#B42318"
  error-soft: "rgba(180, 35, 24, 0.08)"
  brand-panel: "#0E3736"
  brand-panel-ink: "#EAF3F1"
  brand-panel-ink-2: "#A9BEBB"
typography:
  display:
    fontFamily: "'Manrope', system-ui, -apple-system, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif"
    fontSize: "clamp(1.9rem, 4vw, 2.5rem)"
    fontWeight: 750
    lineHeight: 1.12
    letterSpacing: "-0.03em"
  headline:
    fontFamily: "'Manrope', system-ui, -apple-system, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif"
    fontSize: "clamp(1.75rem, 3vw, 2.5rem)"
    fontWeight: 750
    lineHeight: 1.12
    letterSpacing: "-0.02em"
  title:
    fontFamily: "'Manrope', system-ui, -apple-system, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif"
    fontSize: "1.05rem"
    fontWeight: 650
    letterSpacing: "-0.01em"
  body:
    fontFamily: "system-ui, -apple-system, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif"
    fontSize: "1rem"
    fontWeight: 400
    lineHeight: 1.55
    letterSpacing: "normal"
  label:
    fontFamily: "system-ui, -apple-system, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif"
    fontSize: "0.875rem"
    fontWeight: 600
    letterSpacing: "normal"
rounded:
  base: "10px"
spacing:
  xs: "4px"
  sm: "8px"
  md: "16px"
  lg: "24px"
  xl: "32px"
  xxl: "40px"
  xxxl: "48px"
  4xl: "56px"
components:
  button-primary:
    backgroundColor: "{colors.accent}"
    textColor: "#FFFFFF"
    rounded: "{rounded.base}"
    height: "48px"
  button-primary-hover:
    backgroundColor: "{colors.accent-hover}"
  button-outline:
    backgroundColor: "transparent"
    textColor: "{colors.ink-2}"
    rounded: "{rounded.base}"
    padding: "0.5rem 1rem"
  input-text:
    backgroundColor: "{colors.surface}"
    textColor: "{colors.ink}"
    rounded: "{rounded.base}"
    height: "48px"
    padding: "0 0.9rem"
  card-institution:
    backgroundColor: "{colors.surface}"
    textColor: "{colors.ink}"
    rounded: "{rounded.base}"
    padding: "1.35rem 1.4rem"
  theme-toggle:
    backgroundColor: "{colors.surface}"
    textColor: "{colors.ink}"
    rounded: "50%"
    size: "40px"
---

# Design System: Enigma

## Overview

**Creative North Star: "Jade Institucional"**

El sistema es una identidad institucional para plataformas educativas hecha de verde petrol-jade: calma, credibilidad y una sola voz. La paleta vive en pares — cada rol de color tiene un valor claro y uno oscuro, porque la escena (personal y estudiantes, oficinas y aulas) exige ambos modos siempre. El verde profundo del panel de identidad, la superficie clara u oscura donde se trabaja y un único acento jade reservado para la interactividad: esa es toda la arquitectura cromática.

La tipografía es la de una institución seria y moderna: Manrope autohosteado (variable 500–800) para los roles display, en pesos 650–750 con tracking apretado (−0.01 a −0.03em), sobre una pila de sistema neutral para el cuerpo. Las formas son uniformes — un solo radio de 10px — y la profundidad es ambiental: superficies planas, tarjetas que se elevan 2px con sombras difusas, sin neobrutalismo. La voz de la UI es voseo rioplatense imperativo ("Ingresá", "Elegí", "Contactá").

Cada superficie de auth autoriza una sola entrada animada de 400ms; todo lo demás transiciona en 180ms con la misma curva. El login vive: coreografía de entrada única (≈600ms, escalonada) y vida ambiental lenta en el panel de marca — aurora, textura y resplandor del emblema — pausada con la pestaña oculta; `prefers-reduced-motion` elimina entrada y vida ambiental. El tema es ciudadano de primera clase: toggle persistido en `localStorage` (`enigma_theme`), script pre-render en `index.html` y fallback a `prefers-color-scheme`, sin flash.

**Key Characteristics:**
- Paleta petrol-jade en pares claro/oscuro; cada rol tiene ambas variantes.
- Un solo acento (jade) para interactividad; el error vive aparte en su propio rojo.
- Manrope (variable 500–800, autohosteado) solo en roles display, 650–750, tracking −0.01 a −0.03em; el cuerpo usa pila de sistema.
- Radio único de 10px; sombras ambientales de dos capas; elevación por hover de 2px.
- Ambos modos siempre: toggle persistido, script pre-render, fallback `prefers-color-scheme`.
- Voseo rioplatense imperativo en toda la UI.

## Colors

Una sola familia de matices — petrol-jade — expresada como pares claro/oscuro por rol: el oscuro es la paleta del usuario (acento #259585, profundidades #123E3C / #0E3736) y el claro deriva de la misma familia de tonos. El frontmatter registra el valor claro (bloque `:root`); el sidecar lleva el par oscuro de cada rol.

### Primary
- **Jade Acción** (#14665C / oscuro #259585): el único acento, exclusivamente para interactividad — botón primario, enlaces, anillos de focus, estados hover, borde de tarjeta en hover.
- **Jade Hover** (#0F524A / oscuro #2BB09E): profundización del acento en hover/focus de la acción primaria.
- **Jade de Marca** (#4FD1BE / oscuro #259585): trazo del monograma sobre los paneles de identidad.
- **Tinte de Acento** (rgba 20, 102, 92, 0.10 / oscuro rgba 37, 149, 133, 0.16): fondos de hover suaves, anillos de focus, relleno del anillo de inputs.

### Neutral
- **Tinta** (#123E3C / oscuro #E9F2F0): texto primario y títulos; también el glifo de marca fuera del panel.
- **Tinta Secundaria** (#43605D / oscuro #A9BEBB): leads, texto secundario, estados de carga.
- **Tinta Terciaria** (#64807D / oscuro #86A09D): placeholders, iconos en reposo, flecha de tarjeta.
- **Fondo** (#F3F8F7 / oscuro #0E3736): fondo de página.
- **Superficie** (#FFFFFF / oscuro #123E3C): tarjetas, inputs, botones de icono.
- **Borde** (#D5E2E0 / oscuro #2A615F): trazos de 1px en inputs, tarjetas y botones outline.
- **Verde Institucional Profundo** (#0E3736 / oscuro #0B2B2A): panel de identidad de la marca (login).
- **Ink de Panel** (#EAF3F1 en ambos modos): texto y wordmark sobre el panel profundo.
- **Ink de Panel 2** (#A9BEBB en ambos modos): copy secundario sobre el panel.

### Error
- **Error** (#B42318 / oscuro #F97066) + **Tinte de Error** (rgba 180, 35, 24, 0.08 / oscuro rgba 249, 112, 102, 0.10): solo estados de error y validación — alertas con borde al 35% del error.

### Named Rules
**La Regla del Par.** Cada rol de color existe como par claro/oscuro; nada se define en un solo modo. Ambos modos renderizan siempre, con toggle persistido y sin flash.

**La Regla del Jade de Interacción.** El acento jade se usa solo para interactividad: enlaces, focus, hover, acción primaria, selección. El texto estático y los bordes usan las tintas y el borde neutral; decorar con acento está prohibido.

**La Regla del Panel de Marca.** La identidad (monograma + wordmark) vive siempre sobre el Verde Institucional Profundo con ink pálido; el trazo del glifo cambia a Jade de Marca sobre el panel. Una sola identidad para todas las instituciones — sin variantes por institución.

## Typography

**Display Font:** Manrope (autohosteado, variable 500–800, woff2 en `Client/wwwroot/fonts/`, fallback pila de sistema)
**Body Font:** pila de sistema (`system-ui, -apple-system, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif`)

**Character:** Manrope geométrico-humanista en display sobre un cuerpo de sistema silencioso: institucional, confiable, sin ruido decorativo.

### Hierarchy
- **Display** (750, `clamp(1.9rem, 4vw, 2.5rem)`, lh 1.12, ls −0.03em): h1 de página (selección de institución, inicio; en login el h1 es 2.25rem fijo).
- **Headline** (750, `clamp(1.75rem, 3vw, 2.5rem)`, lh 1.12, ls −0.02em): copy de manifiesto del panel de identidad, máx. 16ch.
- **Title** (650, 1.05rem, ls −0.01em): nombres de entidad en tarjetas (institución).
- **Body** (400, 1rem–1.05rem, lh 1.55): formularios, leads, copy del panel; secundario en Tinta Secundaria; máx. 38ch en el panel.
- **Label** (600, 0.875rem): etiquetas de campos; los botones usan peso 600.
- **Wordmark** (750, 1.375rem, ls −0.02em): nombre de marca, solo junto al monograma.

### Named Rules
**La Regla de Manrope Solo Display.** Manrope es solo para roles display (h1, headline de panel, wordmark, nombres de entidad) en pesos 650–750 con tracking −0.01 a −0.03em. El cuerpo, los inputs y los botones usan la pila de sistema; Manrope nunca compone texto corrido.

## Layout

El login es un split asimétrico: `grid-template-columns: minmax(320px, 42%) 1fr` — panel de identidad a la izquierda, columna de formulario centrada con ancho máx. de 400px. Por debajo de 860px colapsa a una sola columna con el panel como franja superior. La selección de institución es una columna única centrada (máx. 880px) con grilla de tarjetas `repeat(auto-fill, minmax(260px, 1fr))`, gap 1rem; por debajo de 480px el padding se compacta (1.25rem). El layout de auth no tiene navegación: `AuthLayout` renderiza el cuerpo desnudo.

La app autenticada es una mesa de trabajo: `MainLayout` es un shell con navbar superior sticky (lockup de marca 28px a la izquierda; chip de institución, usuario y botón Salir a la derecha — el chip se oculta por debajo de 640px) y sin sidebar. El home arranca con una franja de identidad viva (PanelVivo sobre el Verde Institucional Profundo + saludo, nombre y tipo de institución) y continúa con el escritorio: `grid-template-columns: minmax(240px, 300px) 1fr` — rail de contexto a la izquierda (fichas de Institución activa y Usuario) y zona de módulos a la derecha con empty state honesto; por debajo de 860px todo apila en una columna.

### Named Rules
**La Regla del Movimiento en Tres Capas.** (1) **Entrada autoral única:** coreografía escalonada de ≈600ms (marca → emblema → manifiesto → formulario) con la curva del sistema; un solo momento por superficie (el home tiene su propia entrada suave de 560ms). (2) **Vida ambiental:** el componente compartido `PanelVivo` — aurora de tres manchas (13–21s), barrido de luz (9s), motas ascendentes (15–26s, `100cqh` de recorrido) y textura de puntos — siempre sobre el panel de marca, con transform/opacity y box-shadow; se pausa con la pestaña oculta (`EnigmaMotion` → `.panel-vivo--hidden`, `animation-play-state: paused`). (3) **Feedback:** 150–300ms — spinner del botón 700ms, alerta de error 260ms. Toda entrada y vida ambiental vive bajo `@media (prefers-reduced-motion: no-preference)`; sin ella el contenido queda visible.

**La Regla de Ambos Modos Siempre.** Toda superficie renderiza en claro y oscuro. La preferencia persiste en `localStorage` (`enigma_theme`), se aplica con un script inline pre-render en `index.html` antes del primer render y cae a `prefers-color-scheme`.

## Elevation & Depth

El sistema es plano por defecto con elevación ambiental: panel, formulario e inputs no tienen sombra; solo las tarjetas interactivas la llevan. La sombra en reposo es suave y de dos capas — un hairline de 1px más un ambiente de 24px — y el hover levanta la tarjeta 2px y profundiza el ambiente. No hay sombras offset duras: el neobrutalismo está fuera del mundo.

### Shadow Vocabulary
- **Tarjeta en reposo** (`0 1px 2px rgba(14, 55, 54, 0.06), 0 8px 24px rgba(14, 55, 54, 0.08)`; oscuro `0 1px 2px rgba(0, 0, 0, 0.25), 0 8px 24px rgba(0, 0, 0, 0.35)`): tarjetas de institución en reposo.
- **Tarjeta en hover** (`0 1px 2px rgba(14, 55, 54, 0.06), 0 14px 32px rgba(14, 55, 54, 0.14)`): profundización ambiental del hover, junto al levante de 2px.

### Named Rules
**La Regla del Levante Suave.** Las tarjetas interactivas se elevan 2px en hover con sombra ambiental profundizada; toda sombra es difusa (blur ≥ 24px), nunca offset dura.

## Shapes

Lenguaje de formas uniforme: un solo radio de 10px (`--enigma-radius`) para inputs, botones, tarjetas y alertas. Dos excepciones deliberadas: el toggle de tema es un botón-icono circular de 40px, y el glifo de marca es un cuadrado redondeado de 32px con rx 7 (activo de marca, no superficie). El focus usa outline de 2px en acento con offset de 2px; los controles Bootstrap reciben un anillo doble (0.1rem blanco + 0.25rem de tinte de acento); los inputs en focus suman borde acento más anillo de 3px de tinte.

### Named Rules
**La Regla de los Diez Píxeles.** Toda superficie comparte el mismo radio (10px): inputs, botones, tarjetas, alertas. Sin píldoras ni esquinas rectas de excepción.

## Components

### Marca
Monograma "E" de 32×32 (rect rx 7, `fill: currentColor`) con la barra del medio corrida a la izquierda (paths `M11 11.5h10` / `M11 16h6.5` / `M11 20.5h9`), trazo de 2.4 con puntas redondas en `var(--enigma-brand-glyph-stroke, #FFFFFF)` — Jade de Marca sobre paneles, blanco por defecto. Wordmark Manrope 1.375rem, peso 750, ls −0.02em. La marca vive siempre sobre el Verde Institucional Profundo. **Logo real:** `images/logo_size.jpg` (color) e `images/logo_size_invert.png` (invertido). El invertido vive sobre el panel profundo — lockup de 32px (rx 7, junto al wordmark) y emblema hero de `clamp(72px, 9vw, 116px)` centrado, con resplandor jade respirante; el emblema se oculta por debajo de 860px. El color es el favicon (`<link rel="icon" type="image/jpeg"`), el apple-touch-icon y el lockup de la navbar autenticada (28px). **PWA:** `images/logo-512.svg` envuelve el logo color en un lienzo 512×512 (icono any + maskable del manifest); el manifest (`manifest.webmanifest`, theme `#0E3736`, fondo `#F3F8F7`, display standalone) y los service workers viven en `wwwroot` (`service-worker.js` no-op en dev; `service-worker.published.js` — red primero con caché de respaldo — lo reemplaza al publicar).

### Botones
- **Primario (Ingresá):** 48px de alto, fondo Jade Acción, texto blanco, peso 600, radio 10px, ancho completo en el formulario. Hover: Jade Hover; active: `translateY(1px)`; disabled: opacidad 0.6. Focus-visible: outline 2px acento, offset 2px. Transición de fondo 180ms.
- **Outline (Salir):** 1px borde neutral, texto Tinta Secundaria, radio 10px, padding 0.5rem 1rem. Hover: texto y borde en acento sobre Tinte de Acento.

### Inputs / Campos
48px de alto, fondo Superficie, borde 1px neutral, radio 10px, label arriba (0.875rem, peso 600). Placeholder en Tinta Terciaria; focus: borde acento + anillo de 3px de Tinte de Acento. El campo de contraseña lleva un botón de visibilidad (38px) anidado a la derecha.

### Tarjetas (institución)
Fondo Superficie, borde 1px neutral, radio 10px, sombra de reposo, padding 1.35rem 1.4rem. Contenido: nombre (Manrope 1.05rem, peso 650, ls −0.01em) + flecha a la derecha en Tinta Terciaria. Hover: levante 2px, borde acento, flecha en acento deslizada 3px; active: sin levante.

### Alerta de error
Texto Error sobre Tinte de Error, borde 1px al 35% del Error (`color-mix`), radio 10px, padding ~0.7–1rem, `role="alert"`. Compartida entre login y selección.

### Toggle de tema
Botón circular de 40px, fondo Superficie, borde 1px neutral, icono de 20px de trazo. Hover: Tinte de Acento + borde acento. Alterna `data-theme` en `<html>` y persiste en `localStorage`.

### PanelVivo (vida ambiental compartida)
`EnigmaMotion.init()` (idempotente, una vez por sesión desde `App.razor` — cubre login, selección y home) toggla `.panel-vivo--hidden` con `document.hidden` y pausa las animaciones. Sin blur en runtime — solo transform/opacity.

### Shell autenticado (navbar)
Barra sticky de 1px de borde sobre Superficie: lockup de marca a la izquierda (logo color 28px + wordmark), a la derecha chip de institución (icono de edificio 16px + nombre, Tinta Secundaria), usuario (peso 600, separado por hairline) y botón Salir (outline compacto). Por debajo de 640px el chip desaparece y el padding se compacta.

### Home: fichas de contexto y empty state de módulos
Ficha de contexto: Superficie + borde 1px + sombra de reposo, radio 10px, padding 1.15rem 1.25rem; dt como label (0.8rem, Tinta Terciaria), dd peso 650 y sub en Tinta Secundaria. Empty state de módulos: borde 1.5px dashed, radio 10px, centrado, con el monograma de marca en marca de agua (40px, opacidad 0.45), título peso 650 y copy en Tinta Secundaria (máx. 34ch). Sin métricas ni módulos inventados.

### PWA
La app es instalable: `manifest.webmanifest` (name Enigma, `display: standalone`, `theme_color` `#0E3736` — el Verde Institucional Profundo — y `background_color` `#F3F8F7`), iconos `images/logo_size.jpg` (192, any), `images/logo_size.svg` (logo color, any), `images/logo_size_invert.svg` (invertido, maskable) e `images/icon.svg` (favicon SVG), `meta theme-color`, apple touch icon y `mobile-web-app-capable`. Service worker doble: `service-worker.js` (no-op en desarrollo, se reemplaza manualmente por `service-worker.published.js` antes de deploy) y `service-worker.published.js` (red primero con fallback de caché, cache `enigma-v1`, precache del shell; offline sirve lo último visitado, navegaciones caen a `index.html`).

## Do's and Don'ts

### Do:
- **Do** emparejar cada rol de color en claro y oscuro — ambos modos renderizan siempre, sin flash.
- **Do** reservar el acento jade para estados interactivos; el texto estático y los bordes usan las tintas y el borde neutral.
- **Do** usar Manrope solo en roles display, pesos 650–750, tracking −0.01 a −0.03em.
- **Do** mantener el radio de 10px y el vocabulario de sombras ambientales de dos capas en toda superficie nueva.
- **Do** escribir la UI en voseo rioplatense imperativo ("Ingresá", "Elegí", "Contactá").
- **Do** mantener la marca sobre el Verde Institucional Profundo con ink pálido y trazo del glifo en Jade de Marca.

### Don't:
- **Don't** introducir sombras offset duras ni profundidad neobrutalista — la elevación es ambiental y suave.
- **Don't** usar micro-etiquetas en mayúsculas con tracking (kickers/eyebrows) — no forman parte del sistema.
- **Don't** dejar que el display caiga en una fuente de sistema — Manrope se autohostea; hay que enviarlo.
- **Don't** crear un segundo acento ni branding por institución — una sola identidad para todas.
- **Don't** pintar validaciones con `red`/verde crudos — usar los tokens de Error (el build aún carga restos de plantilla en `red` y #26b050; defecto, no regla).

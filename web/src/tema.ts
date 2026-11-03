import { createTheme, type MantineColorsTuple, virtualColor } from '@mantine/core';

// Estilo "vía" (pedido del usuario, 2026-11-01, reemplaza al blanco y negro del 2026-10-21): la interfaz toma los colores
// de la calle. Asfalto para las superficies de mando (cabecera, portada), azul vía para todo lo que se pulsa, amarillo
// señal para la ruta y los acentos (nunca como texto sobre blanco), y el semáforo solo para estados. Tarjetas blancas
// sobre fondo niebla, con borde fino y sombra suave en capas. Inter para leer y Overpass (derivada de la letra de las
// señales viales, Highway Gothic) para títulos y cifras.

/** Azul vía: botones, enlaces y selección. El tono 6 (#2453ff) da 5,9:1 con texto blanco. */
const via: MantineColorsTuple = ['#eef2ff', '#dce4ff', '#b6c6ff', '#8ca5ff', '#6888ff', '#4c72ff', '#2453ff', '#1a44e0', '#1236bd', '#0b2a99'];
/** Amarillo señal: la ruta, la línea de carril y los acentos decorativos. */
const senal: MantineColorsTuple = ['#fff8e1', '#ffefb8', '#ffe38a', '#ffd75c', '#ffcd38', '#ffc61f', '#ffc21a', '#e6a800', '#c48f00', '#9e7300'];
/** Asfalto: cabecera, portada y el tema oscuro completo (azul muy profundo, no gris). */
const asfalto: MantineColorsTuple = ['#c9d1e0', '#a7b2c8', '#7e8ba6', '#58657f', '#3b4660', '#2a3449', '#1d2638', '#162033', '#0f1b2d', '#0a1322'];
// Se conservan para los componentes que aún los nombran.
const negro: MantineColorsTuple = ['#f6f6f6', '#e8e8e8', '#d1d1d1', '#afafaf', '#8a8a8a', '#5e5e5e', '#000000', '#222222', '#000000', '#222222'];
const blanco: MantineColorsTuple = ['#ffffff', '#ffffff', '#f6f6f6', '#e8e8e8', '#d1d1d1', '#afafaf', '#ffffff', '#e8e8e8', '#ffffff', '#e8e8e8'];

const fuente = '"Inter Variable", Inter, system-ui, -apple-system, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif';
export const fuenteSenal = '"Overpass Variable", Overpass, "Inter Variable", system-ui, sans-serif';

export const tema = createTheme({
  primaryColor: 'primario',
  primaryShade: { light: 6, dark: 5 },
  colors: {
    via,
    senal,
    asfalto,
    negro,
    blanco,
    // El tema oscuro de Mantine se arma sobre el asfalto: fondo #162033, superficies #1d2638.
    dark: ['#d5dbe7', '#a7b2c8', '#7e8ba6', '#58657f', '#3b4660', '#2a3449', '#1d2638', '#162033', '#0f1b2d', '#0a1322'],
    primario: virtualColor({ name: 'primario', light: 'via', dark: 'via' }),
  },
  white: '#ffffff',
  black: '#0f1b2d',
  autoContrast: true,
  defaultRadius: 'md',
  radius: { xs: '4px', sm: '6px', md: '10px', lg: '14px', xl: '20px' },
  fontFamily: fuente,
  fontFamilyMonospace: 'ui-monospace, "Cascadia Mono", Consolas, monospace',
  // Una escala más grande que la de Mantine (14/16 px): el panel se lee de pie, a distancia del monitor, en turnos largos.
  fontSizes: { xs: '0.8125rem', sm: '0.9375rem', md: '1.0625rem', lg: '1.1875rem', xl: '1.375rem' },
  lineHeights: { xs: '1.45', sm: '1.5', md: '1.55', lg: '1.55', xl: '1.5' },
  headings: {
    fontFamily: fuenteSenal,
    fontWeight: '700',
    sizes: {
      h1: { fontSize: '2.5rem', lineHeight: '1.15', fontWeight: '700' },
      h2: { fontSize: '1.875rem', lineHeight: '1.2' },
      h5: { fontSize: '1.125rem', lineHeight: '1.3' },
    },
  },
  // Sombras en capas, teñidas de asfalto: dan profundidad sin el gris plano de las sombras por defecto.
  shadows: {
    xs: '0 1px 2px rgba(15, 27, 45, 0.06)',
    sm: '0 1px 2px rgba(15, 27, 45, 0.05), 0 2px 8px rgba(15, 27, 45, 0.05)',
    md: '0 2px 4px rgba(15, 27, 45, 0.05), 0 8px 24px rgba(15, 27, 45, 0.08)',
    lg: '0 4px 8px rgba(15, 27, 45, 0.06), 0 16px 40px rgba(15, 27, 45, 0.12)',
    xl: '0 8px 16px rgba(15, 27, 45, 0.08), 0 24px 64px rgba(15, 27, 45, 0.16)',
  },
  // Foco siempre visible al navegar con teclado (OPLOG-ACC-01, WCAG 2.4.7).
  focusRing: 'always',
  components: {
    Button: { defaultProps: { radius: 'md' }, styles: { root: { fontWeight: 600 } } },
    SegmentedControl: { defaultProps: { radius: 'xl', color: 'primario' } },
    Tabs: { defaultProps: { variant: 'pills', color: 'primario', radius: 'md' } },
    // Una insignia nunca se corta ("Sin cond…"): ocupa lo que necesita su texto.
    Badge: { defaultProps: { radius: 'sm' }, styles: { root: { fontWeight: 600, textTransform: 'none', flexShrink: 0 }, label: { overflow: 'visible' } } },
    Avatar: { defaultProps: { color: 'primario', variant: 'light' } },
    ThemeIcon: { defaultProps: { color: 'primario' } },
    Table: { defaultProps: { verticalSpacing: 'sm', highlightOnHover: true } },
    Paper: { defaultProps: { radius: 'lg' } },
    // Los botones de cerrar (la X) necesitan un nombre para los lectores de pantalla (WCAG 4.1.2).
    Modal: { defaultProps: { closeButtonProps: { 'aria-label': 'Cerrar' }, radius: 'lg', shadow: 'xl' } },
    Notification: { defaultProps: { closeButtonProps: { 'aria-label': 'Cerrar la notificación' }, radius: 'md' } },
  },
});

import { createTheme, type MantineColorsTuple, virtualColor } from '@mantine/core';

// Estilo "quiosco de transporte" (pedido del usuario, 2026-10-21): la interfaz es blanco y negro; el color queda para lo
// que significa algo (semáforo, severidad, estados), nunca para decorar. Plano: sin sombras, bordes finos y radio de 8 px
// (píldora solo para los selectores de modo). Inter como tipografía, con peso normal en los títulos grandes.

// Negro para el tema claro (relleno #000, hover #222) y blanco para el oscuro: el primario se invierte con el tema.
const negro: MantineColorsTuple = ['#f6f6f6', '#e8e8e8', '#d1d1d1', '#afafaf', '#8a8a8a', '#5e5e5e', '#000000', '#222222', '#000000', '#222222'];
const blanco: MantineColorsTuple = ['#ffffff', '#ffffff', '#f6f6f6', '#e8e8e8', '#d1d1d1', '#afafaf', '#ffffff', '#e8e8e8', '#ffffff', '#e8e8e8'];
// Azul claro: la única señal de color de la interfaz, para la pestaña activa.
const cielo: MantineColorsTuple = ['#eef7f9', '#ddeff2', '#c7e4ea', '#b3d9e0', '#a6d2db', '#9dcdd6', '#9dcdd6', '#8cc1cb', '#9dcdd6', '#8cc1cb'];

const fuente = '"Inter Variable", Inter, system-ui, -apple-system, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif';

export const tema = createTheme({
  primaryColor: 'primario',
  colors: { negro, blanco, cielo, primario: virtualColor({ name: 'primario', light: 'negro', dark: 'blanco' }) },
  white: '#ffffff',
  black: '#000000',
  // Texto blanco o negro según el fondo: el botón negro lleva texto blanco y el blanco (tema oscuro) texto negro.
  autoContrast: true,
  defaultRadius: 'md',
  fontFamily: fuente,
  fontFamilyMonospace: 'ui-monospace, "Cascadia Mono", Consolas, monospace',
  headings: {
    fontFamily: fuente,
    fontWeight: '500',
    sizes: {
      h1: { fontSize: '2.25rem', lineHeight: '1.22', fontWeight: '400' },
      h2: { fontSize: '1.5rem', lineHeight: '1.33' },
      h5: { fontSize: '1rem', lineHeight: '1.4' },
    },
  },
  // Plano: la profundidad sale del paso de superficie (blanco → gris niebla → negro), no de sombras.
  shadows: { xs: 'none', sm: 'none', md: 'none', lg: 'none', xl: 'none' },
  // Foco siempre visible al navegar con teclado (OPLOG-ACC-01, WCAG 2.4.7).
  focusRing: 'always',
  components: {
    Button: { defaultProps: { radius: 'md' }, styles: { root: { fontWeight: 500 } } },
    SegmentedControl: { defaultProps: { radius: 'xl', color: 'primario' } },
    Tabs: { defaultProps: { variant: 'pills', color: 'cielo', radius: 'md' } },
    Badge: { defaultProps: { radius: 'sm' }, styles: { root: { fontWeight: 500, textTransform: 'none' } } },
    Avatar: { defaultProps: { color: 'primario', variant: 'filled' } },
    ThemeIcon: { defaultProps: { color: 'primario' } },
    Table: { defaultProps: { verticalSpacing: 'sm', highlightOnHover: true } },
    // Los botones de cerrar (la X) necesitan un nombre para los lectores de pantalla (WCAG 4.1.2).
    Modal: { defaultProps: { closeButtonProps: { 'aria-label': 'Cerrar' }, radius: 'md' } },
    Notification: { defaultProps: { closeButtonProps: { 'aria-label': 'Cerrar la notificación' } } },
  },
});

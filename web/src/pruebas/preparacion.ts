import '@testing-library/jest-dom/vitest';
import { notifications } from '@mantine/notifications';
import { cleanup } from '@testing-library/react';
import { afterEach } from 'vitest';

// jsdom no trae lo que Mantine usa para el tema y los menús (recomendación de la documentación de Mantine).
Object.defineProperty(window, 'matchMedia', {
  writable: true,
  value: (consulta: string) => ({
    matches: false,
    media: consulta,
    onchange: null,
    addListener: () => {},
    removeListener: () => {},
    addEventListener: () => {},
    removeEventListener: () => {},
    dispatchEvent: () => false,
  }),
});

class ResizeObserverDePrueba {
  observe() {}
  unobserve() {}
  disconnect() {}
}
window.ResizeObserver = ResizeObserverDePrueba;
window.HTMLElement.prototype.scrollIntoView = () => {};
// axe-core prueba ligaduras de iconos con un canvas; jsdom no lo implementa y sin esto solo ensucia la salida.
window.HTMLCanvasElement.prototype.getContext = () => null;

afterEach(() => {
  cleanup();
  // Las notificaciones de Mantine viven en un almacén global: sin limpiarlo, pasadas cinco las nuevas quedan en cola y
  // no se pintan en las pruebas siguientes.
  notifications.clean();
});

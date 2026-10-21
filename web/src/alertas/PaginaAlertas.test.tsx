import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { alerta, pagina } from '../pruebas/datos';
import { apiFalsa, renderizar, sinViolacionesDeAccesibilidad } from '../pruebas/utilidades';
import { PaginaAlertas } from './PaginaAlertas';

describe('Alertas (RF-45)', () => {
  it('muestra la severidad con texto (no solo color) y marca una como leída', async () => {
    const { peticiones } = apiFalsa({
      'GET /api/alertas': () => pagina([alerta('a-1', 'Critica'), alerta('a-2', 'Info')]),
      'POST /api/alertas/a-1/leida': () => ({ id: 'a-1', leidaEn: '2026-10-21T12:10:00Z' }),
    });
    const usuario = userEvent.setup();
    renderizar(<PaginaAlertas />);

    expect(await screen.findByText('Crítica')).toBeInTheDocument();
    expect(screen.getByText('Información')).toBeInTheDocument();
    await sinViolacionesDeAccesibilidad();
    await usuario.click(screen.getByRole('button', { name: 'Marcar como leída: Alerta a-1' }));

    await screen.findByText('Crítica');
    expect(peticiones.some((p) => p.method === 'POST' && p.url.endsWith('/a-1/leida'))).toBe(true);
    expect(new URL(peticiones[0]!.url).searchParams.get('SoloNoLeidas')).toBe('true');
  });
});

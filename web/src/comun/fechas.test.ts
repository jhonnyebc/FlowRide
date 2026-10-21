import { describe, expect, it } from 'vitest';
import { horaLocal, hoyIso, instante } from './fechas';

describe('fechas', () => {
  it('muestra la hora local de un evento futuro sin convertirla, con el nombre de su zona', () => {
    const texto = horaLocal('2026-10-22T06:00:00', 'America/Bogota');
    expect(texto).toContain('6:00');
    expect(texto).toContain('2026');
    expect(texto).toContain('(America/Bogota)');
  });

  it('sin valor muestra un guion', () => {
    expect(horaLocal(null, 'America/Bogota')).toBe('—');
    expect(instante(undefined)).toBe('—');
  });

  it('da el día de hoy en formato yyyy-MM-dd', () => {
    expect(hoyIso(new Date(2026, 9, 21, 23, 30))).toBe('2026-10-21');
  });
});

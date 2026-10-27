import { describe, expect, it } from 'vitest';
import { diasHasta, fechaCorta, horaLocal, hoyIso, instante } from './fechas';

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

  it('muestra una fecha sin hora tal cual, sin correrla de día por la zona', () => {
    expect(fechaCorta('2026-11-30')).toBe('30 de nov de 2026');
    expect(fechaCorta(null)).toBe('—');
  });

  it('cuenta los días que faltan para un vencimiento (negativo si ya pasó)', () => {
    const ahora = new Date(2026, 9, 24, 22, 0);
    expect(diasHasta('2026-10-24', ahora)).toBe(0);
    expect(diasHasta('2026-11-23', ahora)).toBe(30);
    expect(diasHasta('2026-10-23', ahora)).toBe(-1);
  });
});

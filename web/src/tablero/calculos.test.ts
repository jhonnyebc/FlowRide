import { describe, expect, it } from 'vitest';
import { servicioVista } from '../pruebas/datos';
import { horaPico, porcentaje, rangoDelPeriodo, saludo, serviciosPorHora } from './calculos';

describe('cálculos del tablero', () => {
  it('el periodo son días completos de la zona del navegador, terminando al final de hoy', () => {
    const ahora = new Date(2026, 9, 21, 15, 30);

    const hoy = rangoDelPeriodo('hoy', ahora);
    const semana = rangoDelPeriodo('7d', ahora);

    expect(new Date(hoy.desde)).toEqual(new Date(2026, 9, 21));
    expect(new Date(hoy.hasta)).toEqual(new Date(2026, 9, 22));
    expect(new Date(semana.desde)).toEqual(new Date(2026, 9, 15));
    expect(semana.hasta).toBe(hoy.hasta);
  });

  it('el porcentaje sin base es null, nunca un 0 % engañoso', () => {
    expect(porcentaje(9, 11)).toBe(81.8);
    expect(porcentaje(0, 0)).toBeNull();
  });

  it('agrupa los servicios por hora y semáforo sin dejar huecos entre la primera y la última', () => {
    const a = (hora: number) => new Date(2026, 9, 21, hora, 15).toISOString();
    const filas = serviciosPorHora([
      { servicio: servicioVista('1', 'Pendiente', { fechaHoraServicio: a(6) }), nivel: 'Atencion', motivo: null },
      { servicio: servicioVista('2', 'SinCandidatos', { fechaHoraServicio: a(6) }), nivel: 'Critico', motivo: null },
      { servicio: servicioVista('3', 'Asignada', { fechaHoraServicio: a(8) }), nivel: 'Normal', motivo: null },
    ]);

    expect(filas).toEqual([
      { hora: '06:00', Critico: 1, Atencion: 1, Normal: 0 },
      { hora: '07:00', Critico: 0, Atencion: 0, Normal: 0 },
      { hora: '08:00', Critico: 0, Atencion: 0, Normal: 1 },
    ]);
    expect(serviciosPorHora([])).toEqual([]);
  });

  it('la hora pico es la de más servicios; si empatan, la primera', () => {
    const fila = (hora: string, Critico: number, Atencion: number, Normal: number) => ({ hora, Critico, Atencion, Normal });

    expect(horaPico([fila('06:00', 1, 0, 0), fila('07:00', 0, 2, 1), fila('08:00', 1, 1, 1)])?.hora).toBe('07:00');
    expect(horaPico([fila('06:00', 1, 1, 0), fila('07:00', 0, 0, 2)])?.hora).toBe('06:00');
    expect(horaPico([])).toBeNull();
  });

  it('saluda según la hora', () => {
    expect(saludo(new Date(2026, 9, 21, 7))).toBe('Buenos días');
    expect(saludo(new Date(2026, 9, 21, 15))).toBe('Buenas tardes');
    expect(saludo(new Date(2026, 9, 21, 21))).toBe('Buenas noches');
  });
});

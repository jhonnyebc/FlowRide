import { describe, expect, it } from 'vitest';
import { autoridad, operadorActual } from './configuracion';

describe('configuración', () => {
  it('toma el operador del subdominio (ADR-0012)', () => {
    expect(operadorActual('tenant-b.flowride.co')).toBe('tenant-b');
    expect(operadorActual('tenant-a.dev.localhost')).toBe('tenant-a');
  });

  it('arma el realm del operador (ADR-0003)', () => {
    expect(autoridad('tenant-a')).toBe('http://localhost:8080/realms/flowride-tenant-a');
  });
});

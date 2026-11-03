import { IconAlertOctagon, IconAlertTriangle, IconInfoCircle } from '@tabler/icons-react';

// Severidad con color, icono y forma distintos: nunca solo color (OPLOG-ACC-02, WCAG 1.4.1).

export function colorSeveridad(severidad: string): string {
  return severidad === 'Critica' ? 'red' : severidad === 'Advertencia' ? 'orange' : 'blue';
}

export function IconoSeveridad({ severidad, tamano = 18 }: { severidad: string; tamano?: number }) {
  const Icono = severidad === 'Critica' ? IconAlertOctagon : severidad === 'Advertencia' ? IconAlertTriangle : IconInfoCircle;
  return <Icono size={tamano} aria-hidden />;
}

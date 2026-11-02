import { Group, Text } from '@mantine/core';
import { fuenteSenal } from '../tema';
import { textos } from '../textos';

/** Marca de FlowRide sobre el asfalto: la ruta amarilla de la recogida (círculo blanco) al destino (cuadro). Decorativa. */
export function Logo() {
  return (
    <Group gap={12} wrap="nowrap">
      <svg width="36" height="36" viewBox="0 0 26 26" aria-hidden focusable="false">
        <path d="M6 20c0-5 4-6.5 7-7.5s7-2.5 7-7.5" fill="none" stroke="#ffc21a" strokeWidth="2.4" strokeLinecap="round" />
        <circle cx="6" cy="20" r="3.2" fill="#fff" />
        <rect x="17" y="2" width="6" height="6" rx="1" fill="#ffc21a" />
      </svg>
      <Text fw={800} fz={28} c="white" lh={1} ff={fuenteSenal}>
        {textos.app.nombre}
      </Text>
    </Group>
  );
}

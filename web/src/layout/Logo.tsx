import { Group, Text } from '@mantine/core';
import { textos } from '../textos';

/** Marca de FlowRide sobre la banda negra: una ruta con origen (círculo) y destino (cuadro), en blanco. Decorativa. */
export function Logo() {
  return (
    <Group gap={10} wrap="nowrap">
      <svg width="26" height="26" viewBox="0 0 26 26" aria-hidden focusable="false">
        <path d="M6 20c0-5 4-6.5 7-7.5s7-2.5 7-7.5" fill="none" stroke="#fff" strokeWidth="2.2" strokeLinecap="round" />
        <circle cx="6" cy="20" r="3" fill="#fff" />
        <rect x="17" y="2" width="6" height="6" fill="#fff" />
      </svg>
      <Text fw={700} size="xl" c="white" lh={1}>
        {textos.app.nombre}
      </Text>
    </Group>
  );
}

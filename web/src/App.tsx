import { Anchor, Stack, Text, Title } from '@mantine/core';
import { Link, Route, Routes } from 'react-router';
import { PaginaAlertas } from './alertas/PaginaAlertas';
import { ProveedorAlertas } from './alertas/useAlertas';
import { RequiereSesion } from './auth/RequiereSesion';
import { Marco } from './layout/Marco';
import { VistaDelDia } from './operacion/VistaDelDia';
import { DetalleSolicitud } from './solicitudes/DetalleSolicitud';
import { PaginaSolicitudes } from './solicitudes/PaginaSolicitudes';
import { Tablero } from './tablero/Tablero';
import { DetalleViaje } from './viajes/DetalleViaje';
import { PaginaViajes } from './viajes/PaginaViajes';

/** Rutas del panel (11a: tablero y operación). Todo exige sesión y un usuario interno activo. */
export function App() {
  return (
    <RequiereSesion>
      <ProveedorAlertas>
        <Routes>
          <Route element={<Marco />}>
            <Route index element={<Tablero />} />
            <Route path="dia" element={<VistaDelDia />} />
            <Route path="solicitudes" element={<PaginaSolicitudes />} />
            <Route path="solicitudes/:id" element={<DetalleSolicitud />} />
            <Route path="viajes" element={<PaginaViajes />} />
            <Route path="viajes/:id" element={<DetalleViaje />} />
            <Route path="alertas" element={<PaginaAlertas />} />
            <Route path="*" element={<NoEncontrada />} />
          </Route>
        </Routes>
      </ProveedorAlertas>
    </RequiereSesion>
  );
}

function NoEncontrada() {
  return (
    <Stack>
      <Title order={1} size="h2">
        Página no encontrada
      </Title>
      <Text>
        <Anchor component={Link} to="/">
          Volver al tablero
        </Anchor>
      </Text>
    </Stack>
  );
}

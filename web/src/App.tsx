import { Anchor, Stack, Text, Title } from '@mantine/core';
import { IconLock } from '@tabler/icons-react';
import type { ReactNode } from 'react';
import { Link, Route, Routes } from 'react-router';
import { PaginaConductores } from './administracion/PaginaConductores';
import { PaginaEmpresas } from './administracion/PaginaEmpresas';
import { PaginaItinerarios } from './administracion/PaginaItinerarios';
import { PaginaPasajeros } from './administracion/PaginaPasajeros';
import { PaginaUsuarios } from './administracion/PaginaUsuarios';
import { PaginaVehiculos } from './administracion/PaginaVehiculos';
import { PaginaAlertas } from './alertas/PaginaAlertas';
import { ProveedorAlertas } from './alertas/useAlertas';
import { RequiereSesion } from './auth/RequiereSesion';
import { esAdministrador, useUsuario } from './auth/usuario';
import { EstadoVacio } from './comun/Componentes';
import { Marco } from './layout/Marco';
import { VistaDelDia } from './operacion/VistaDelDia';
import { DetalleSolicitud } from './solicitudes/DetalleSolicitud';
import { PaginaSolicitudes } from './solicitudes/PaginaSolicitudes';
import { Tablero } from './tablero/Tablero';
import { DetalleViaje } from './viajes/DetalleViaje';
import { PaginaViajes } from './viajes/PaginaViajes';

/** Rutas del panel (11a operación, 11b administración). Todo exige sesión y un usuario interno activo. */
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
            <Route
              path="administracion/usuarios"
              element={
                <SoloAdministrador>
                  <PaginaUsuarios />
                </SoloAdministrador>
              }
            />
            <Route path="administracion/empresas" element={<PaginaEmpresas />} />
            <Route path="administracion/pasajeros" element={<PaginaPasajeros />} />
            <Route path="administracion/conductores" element={<PaginaConductores />} />
            <Route path="administracion/vehiculos" element={<PaginaVehiculos />} />
            <Route path="administracion/itinerarios" element={<PaginaItinerarios />} />
            <Route path="*" element={<NoEncontrada />} />
          </Route>
        </Routes>
      </ProveedorAlertas>
    </RequiereSesion>
  );
}

/** El menú no muestra la sección a otros roles; si alguien llega por la URL, lo dice (la API igual responde 403). */
function SoloAdministrador({ children }: { children: ReactNode }) {
  const usuario = useUsuario();
  if (esAdministrador(usuario)) return <>{children}</>;
  return (
    <Stack>
      <Title order={1} size="h2">
        Sin acceso
      </Title>
      <EstadoVacio icono={IconLock} titulo="Esta sección es del Administrador" descripcion="Pídele a un administrador del operador que haga este cambio." />
    </Stack>
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

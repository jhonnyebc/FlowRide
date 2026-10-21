import {
  ActionIcon,
  Anchor,
  AppShell,
  Avatar,
  Badge,
  Burger,
  Group,
  Indicator,
  Menu,
  NavLink,
  Paper,
  Text,
  Tooltip,
  UnstyledButton,
  useComputedColorScheme,
  useMantineColorScheme,
} from '@mantine/core';
import { useDisclosure } from '@mantine/hooks';
import {
  IconAlertTriangle,
  IconBell,
  IconBuildingSkyscraper,
  IconCalendarEvent,
  IconChevronDown,
  IconLayoutDashboard,
  IconListDetails,
  IconLogout,
  IconMoon,
  IconRoute,
  IconSun,
} from '@tabler/icons-react';
import { useQuery } from '@tanstack/react-query';
import { useAuth } from 'react-oidc-context';
import { NavLink as EnlaceRuta, Outlet, useLocation } from 'react-router';
import { monitoreo, vistaDelDia } from '../api/consultas';
import { useAlertasNoLeidas } from '../alertas/useAlertas';
import { useUsuario } from '../auth/usuario';
import { hoyIso } from '../comun/fechas';
import { operadorActual } from '../configuracion';
import { texto, textos } from '../textos';
import { Logo } from './Logo';

interface Contador {
  valor: number;
  color: string;
  /** Qué cuenta, para el lector de pantalla: «3 por atender». */
  descripcion: string;
}

/** Lo que espera al usuario en cada sección; sale de las mismas consultas del tablero (sin pedir dos veces). */
function useContadores(): Record<string, Contador | undefined> {
  const noLeidas = useAlertasNoLeidas();
  const vista = useQuery(vistaDelDia(hoyIso()));
  const viajes = useQuery(monitoreo());
  const porAtender = vista.data?.servicios.filter((s) => s.servicio.estado === 'Pendiente' || s.servicio.estado === 'SinCandidatos').length;
  const enCurso = viajes.data?.filter((v) => v.viaje.estado === 'EnCurso').length;
  const contador = (valor: number | undefined, color: string, descripcion: string) =>
    valor ? { valor, color, descripcion } : undefined;
  return {
    '/dia': contador(vista.data?.criticos, 'red', 'críticos hoy'),
    '/solicitudes': contador(porAtender, 'yellow', 'por atender hoy'),
    '/viajes': contador(enCurso, 'teal', 'en curso'),
    '/alertas': contador(noLeidas, 'red', 'sin leer'),
  };
}

const secciones = [
  { ruta: '/', etiqueta: textos.menu.tablero, icono: IconLayoutDashboard, exacta: true },
  { ruta: '/dia', etiqueta: textos.menu.vistaDelDia, icono: IconCalendarEvent, exacta: false },
  { ruta: '/solicitudes', etiqueta: textos.menu.solicitudes, icono: IconListDetails, exacta: false },
  { ruta: '/viajes', etiqueta: textos.menu.viajesEnCurso, icono: IconRoute, exacta: false },
  { ruta: '/alertas', etiqueta: textos.menu.alertas, icono: IconAlertTriangle, exacta: false },
];

/** Marco del panel: cabecera, menú lateral con lo pendiente de cada sección y el contenido. Todo usable con teclado (OPLOG-ACC-01). */
export function Marco() {
  const [abierto, { toggle, close }] = useDisclosure();
  const { pathname } = useLocation();
  const usuario = useUsuario();
  const auth = useAuth();
  const noLeidas = useAlertasNoLeidas();
  const contadores = useContadores();
  const { setColorScheme } = useMantineColorScheme();
  const esquema = useComputedColorScheme('light');
  const etiquetaTema = esquema === 'dark' ? textos.menu.temaClaro : textos.menu.temaOscuro;
  const etiquetaAlertas = `${textos.menu.alertas}: ${noLeidas} sin leer`;
  const rol = texto(textos.roles, usuario.rol);

  return (
    <AppShell header={{ height: 60 }} navbar={{ width: 260, breakpoint: 'sm', collapsed: { mobile: !abierto } }} padding="lg">
      <Anchor href="#contenido" className="saltar-al-contenido">
        {textos.app.saltarAlContenido}
      </Anchor>
      <AppShell.Header className="marco-cabecera">
        <Group h="100%" px="md" justify="space-between" wrap="nowrap">
          <Group gap="sm" wrap="nowrap">
            <Burger opened={abierto} onClick={toggle} hiddenFrom="sm" size="sm" color="white" aria-label={textos.menu.abrirMenu} />
            <Logo />
          </Group>
          <Group gap={6} wrap="nowrap">
            <Tooltip label={etiquetaAlertas}>
              <Indicator label={noLeidas > 99 ? '99+' : noLeidas} size={16} color="red" disabled={noLeidas === 0} offset={4}>
                <ActionIcon component={EnlaceRuta} to="/alertas" variant="subtle" size="lg" radius="xl" className="marco-accion" aria-label={etiquetaAlertas}>
                  <IconBell size={18} aria-hidden />
                </ActionIcon>
              </Indicator>
            </Tooltip>
            <Tooltip label={etiquetaTema}>
              <ActionIcon
                variant="subtle"
                size="lg"
                radius="xl"
                className="marco-accion"
                onClick={() => setColorScheme(esquema === 'dark' ? 'light' : 'dark')}
                aria-label={etiquetaTema}
              >
                {esquema === 'dark' ? <IconSun size={18} aria-hidden /> : <IconMoon size={18} aria-hidden />}
              </ActionIcon>
            </Tooltip>
            <Menu position="bottom-end" width={240}>
              <Menu.Target>
                <UnstyledButton aria-label={`${usuario.nombre}, ${rol}`} className="marco-usuario">
                  <Group gap={8} wrap="nowrap">
                    <Avatar name={usuario.nombre} size={32} color="blanco" aria-hidden />
                    <div className="marco-usuario-texto">
                      <Text size="sm" fw={500} lh={1.15}>
                        {usuario.nombre}
                      </Text>
                      <Text size="xs" c="#afafaf" lh={1.15}>
                        {rol}
                      </Text>
                    </div>
                    <IconChevronDown size={14} aria-hidden />
                  </Group>
                </UnstyledButton>
              </Menu.Target>
              <Menu.Dropdown>
                <Menu.Label>{usuario.correo ?? usuario.nombre}</Menu.Label>
                <Menu.Item leftSection={<IconLogout size={16} aria-hidden />} color="red" onClick={() => void auth.signoutRedirect()}>
                  {textos.menu.cerrarSesion}
                </Menu.Item>
              </Menu.Dropdown>
            </Menu>
          </Group>
        </Group>
      </AppShell.Header>
      <AppShell.Navbar p="sm" aria-label="Menú principal" className="marco-navbar">
        <AppShell.Section grow style={{ overflowY: 'auto' }}>
          <Text size="xs" fw={500} c="var(--fr-texto-suave)" px="sm" pt={4} pb={8}>
            {textos.menu.operacion}
          </Text>
          {secciones.map(({ ruta, etiqueta, icono: Icono, exacta }) => {
            const activa = exacta ? pathname === ruta : pathname.startsWith(ruta);
            const contador = contadores[ruta];
            return (
              <NavLink
                key={ruta}
                component={EnlaceRuta}
                to={ruta}
                label={etiqueta}
                active={activa}
                onClick={close}
                aria-current={activa ? 'page' : undefined}
                aria-description={contador ? `${contador.valor} ${contador.descripcion}` : undefined}
                leftSection={<Icono size={19} stroke={1.7} aria-hidden />}
                rightSection={
                  contador && (
                    <Badge size="sm" radius="xl" circle={contador.valor < 10} color={contador.color} variant="filled" aria-hidden>
                      {contador.valor > 99 ? '99+' : contador.valor}
                    </Badge>
                  )
                }
                className="marco-enlace"
              />
            );
          })}
        </AppShell.Section>
        <AppShell.Section>
          <Paper p="sm" className="superficie">
            <Group gap="xs" wrap="nowrap">
              <IconBuildingSkyscraper size={18} aria-hidden />
              <div>
                <Text size="xs" c="dimmed" lh={1.2}>
                  Operador
                </Text>
                <Text size="sm" fw={500} lh={1.2}>
                  {operadorActual()}
                </Text>
              </div>
            </Group>
          </Paper>
        </AppShell.Section>
      </AppShell.Navbar>
      <AppShell.Main id="contenido" tabIndex={-1} className="marco-contenido">
        <div className="marco-pagina">
          <Outlet />
        </div>
      </AppShell.Main>
    </AppShell>
  );
}

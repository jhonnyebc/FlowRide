import { Alert, Button, Center, Loader, Stack, Text } from '@mantine/core';
import { notifications } from '@mantine/notifications';
import { useQuery } from '@tanstack/react-query';
import { type ReactNode, useEffect } from 'react';
import { useAuth } from 'react-oidc-context';
import { api, conectarSesion, datos } from '../api/cliente';
import { textos } from '../textos';
import { ContextoUsuario } from './usuario';

/**
 * Exige sesión: sin ella abre el inicio de sesión de Keycloak (y vuelve a la misma página). Luego resuelve el usuario
 * interno con /api/usuarios/yo; si FlowRide no lo reconoce (o está inactivo), no hay panel.
 */
export function RequiereSesion({ children }: { children: ReactNode }) {
  const auth = useAuth();
  const conSesion = auth.isAuthenticated && !!auth.user;

  useEffect(() => {
    conectarSesion(
      () => auth.user?.access_token,
      () => {
        notifications.show({ color: 'yellow', message: textos.sesion.reautenticar });
        // max_age=0: Keycloak vuelve a pedir la contraseña (y el TOTP); al volver, el usuario repite la acción.
        void auth.signinRedirect({ state: window.location.pathname, max_age: 0 });
      },
    );
  }, [auth]);

  useEffect(() => {
    if (!auth.isLoading && !auth.isAuthenticated && !auth.activeNavigator && !auth.error)
      void auth.signinRedirect({ state: window.location.pathname });
  }, [auth]);

  const usuario = useQuery({
    queryKey: ['usuario-actual', auth.user?.profile.sub],
    queryFn: () => datos(api.GET('/api/usuarios/yo')),
    enabled: conSesion,
    retry: false,
  });

  if (auth.error)
    return (
      <Center h="100vh" p="md">
        <Stack align="center">
          <Alert color="red" title="No se pudo iniciar sesión">
            {auth.error.message}
          </Alert>
          <Button onClick={() => void auth.signinRedirect()}>{textos.app.reintentar}</Button>
        </Stack>
      </Center>
    );
  if (!conSesion || usuario.isPending)
    return (
      <Center h="100vh" role="status">
        <Stack align="center" gap="xs">
          <Loader aria-hidden />
          <Text>{conSesion ? textos.app.cargando : textos.sesion.iniciando}</Text>
        </Stack>
      </Center>
    );
  if (usuario.isError)
    return (
      <Center h="100vh" p="md">
        <Stack align="center">
          <Alert color="red" title={textos.sesion.sinAcceso}>
            {usuario.error.message}
          </Alert>
          <Button variant="default" onClick={() => void auth.signoutRedirect()}>
            {textos.menu.cerrarSesion}
          </Button>
        </Stack>
      </Center>
    );
  return <ContextoUsuario.Provider value={usuario.data}>{children}</ContextoUsuario.Provider>;
}

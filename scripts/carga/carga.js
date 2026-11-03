// Prueba de carga de FlowRide (QA-03 y QA-07 del DAS): hasta 200 usuarios concurrentes por instancia con P95 < 500 ms
// y P99 < 1,5 s. Cada usuario virtual recorre el camino completo WAF → borde → API del operador → PostgreSQL:
// abre el Panel Web, pide la salud del stack (que consulta su base) y llama a la API, que el borde rechaza con 401
// tras validar que no hay token. No incluye operaciones autenticadas: el único usuario del panel exige TOTP.
//
// Perfiles (variable PERFIL): "humo" (1 usuario, 10 s, para validar) y "carga" (por defecto: sube a USUARIOS en 30 s,
// los mantiene 1 min y baja). La ejecuta scripts/prueba-carga.ps1 dentro de la red de docker compose.
import http from 'k6/http';
import { check, sleep } from 'k6';

const BASE = __ENV.FLOWRIDE_URL || 'https://flowride-waf:8443';
const HOST = __ENV.FLOWRIDE_HOST || 'tenant-a.dev.localhost';
const USUARIOS = Number(__ENV.USUARIOS || 200);

const etapas = __ENV.PERFIL === 'humo'
  ? [{ duration: '1s', target: 1 }, { duration: '10s', target: 1 }]
  : [{ duration: '30s', target: USUARIOS }, { duration: '1m', target: USUARIOS }, { duration: '15s', target: 0 }];

const objetivos = ['p(95)<500', 'p(99)<1500'];

export const options = {
  // Desde la red de docker se llega al WAF por su nombre de contenedor, que no está en su certificado (*.dev.localhost).
  insecureSkipTLSVerify: true,
  scenarios: {
    usuarios_del_panel: { executor: 'ramping-vus', startVUs: 0, stages: etapas, gracefulRampDown: '10s' },
  },
  thresholds: {
    // Sin iteraciones completas, el resto de umbrales "pasarían" sin datos.
    iterations: ['count>0'],
    http_req_failed: ['rate<0.01'],
    checks: ['rate>0.99'],
    'http_req_duration{tipo:panel}': objetivos,
    'http_req_duration{tipo:salud}': objetivos,
    'http_req_duration{tipo:api}': objetivos,
  },
};

// Un 401 de la API sin token es la respuesta correcta, no un error.
http.setResponseCallback(http.expectedStatuses(200, 401));

const cabeceras = { Host: HOST, 'User-Agent': 'Mozilla/5.0 (prueba de carga de FlowRide)' };

export default function () {
  const panel = http.get(`${BASE}/`, { headers: cabeceras, tags: { tipo: 'panel' } });
  check(panel, { 'el Panel Web responde': (r) => r.status === 200 });

  const salud = http.get(`${BASE}/health/ready`, { headers: cabeceras, tags: { tipo: 'salud' } });
  check(salud, { 'el stack y su base están sanos': (r) => r.status === 200 });

  const api = http.get(`${BASE}/api/solicitudes?pagina=1&tamano=20`, { headers: cabeceras, tags: { tipo: 'api' } });
  check(api, { 'la API exige token (401)': (r) => r.status === 401 });

  sleep(1);
}

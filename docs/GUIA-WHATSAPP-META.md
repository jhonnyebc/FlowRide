# Probar el bot con WhatsApp real (número de prueba de Meta)

FlowRide funciona sin WhatsApp real: por defecto usa el proveedor **simulado** (pruebas y desarrollo local). Esta guía
conecta el stack local de `tenant-a` con el **número de prueba de Meta Cloud API** (ADR-0008) para escribirle al bot
desde tu celular. Todo es gratis: el número de prueba, las respuestas dentro de la ventana de 24 horas y el plan
gratuito de ngrok.

> Los valores secretos (token de acceso, secreto de la app y token de verificación) van **solo** en el almacén local
> del operador. Nunca en `appsettings*.json`, en el código ni en un commit.

## 1. App de Meta y número de prueba

1. En <https://developers.facebook.com> → **Mis apps** → **Crear app** (tipo empresa) y agrega el producto **WhatsApp**.
2. En **WhatsApp → Configuración de la API** verás el número de prueba y su **Identificador del número de teléfono**
   (`PhoneNumberId`, no es secreto).
3. En **Para**, agrega tu celular como destinatario y confírmalo con el código que te llega (máximo 5 números).
4. **Secreto de la app** (`AppSecret`): **Configuración de la app → Básica → Clave secreta de la app**.

## 2. Token de acceso que no vence

El token temporal de la página de la API vence en pocas horas. Para la demostración usa uno que no vence:

1. En <https://business.facebook.com/settings> → **Usuarios → Usuarios del sistema** → agrega uno con rol de administrador.
2. **Asignar activos** → tu app y tu cuenta de WhatsApp → control total.
3. **Generar token** → tu app → permisos `whatsapp_business_messaging` y `whatsapp_business_management`, caducidad
   **Nunca**. No lo muestres en capturas; si se expone, revócalo y genera otro.

## 3. Guardar la configuración en el almacén del operador

Inventa un **token de verificación** (cualquier texto largo) y guarda todo en el almacén de `tenant-a`:

```bash
dotnet user-secrets set "FlowRide:WhatsApp:Proveedor" "MetaSandbox" --id flowride-tenant-a
dotnet user-secrets set "FlowRide:WhatsApp:PhoneNumberId" "<identificador del número>" --id flowride-tenant-a
dotnet user-secrets set "FlowRide:WhatsApp:AccessToken" "<token del usuario del sistema>" --id flowride-tenant-a
dotnet user-secrets set "FlowRide:WhatsApp:AppSecret" "<clave secreta de la app>" --id flowride-tenant-a
dotnet user-secrets set "FlowRide:WhatsApp:VerifyToken" "<token de verificación inventado>" --id flowride-tenant-a
```

Para volver al simulado: `dotnet user-secrets set "FlowRide:WhatsApp:Proveedor" "Simulado" --id flowride-tenant-a`.

## 4. Túnel público con ngrok (plan gratuito)

Meta necesita una URL HTTPS pública para el webhook. El dominio estático gratuito de ngrok no cambia, así que se
configura una sola vez en Meta.

1. Crea una cuenta en <https://ngrok.com>, instala el agente (`winget install ngrok.ngrok`) y **actualízalo**
   (`ngrok update`): las cuentas gratuitas exigen la versión 3.20 o mayor. Si la terminal no reconoce `ngrok`, abre una
   nueva o reinicia Rider. Registra tu token: `ngrok config add-authtoken <token de ngrok>`.
2. En el panel de ngrok → **Domains**, reclama tu dominio estático gratuito (`<algo>.ngrok-free.app` o `.ngrok-free.dev`).
3. Levanta la API (`dotnet run --project src/FlowRide.API`) y, en otra terminal:
   `ngrok http --domain=<algo>.ngrok-free.dev 5237`. El inspector de ngrok (<http://127.0.0.1:4040>) muestra cada
   petición que llega.

## 5. Webhook en Meta

1. **WhatsApp → Configuración → Webhook → Editar**:
   - URL de devolución de llamada: `https://<algo>.ngrok-free.app/api/whatsapp/webhook`
   - Token de verificación: el que inventaste en el paso 3.
2. **Verificar y guardar** (FlowRide responde el reto si el token coincide) y suscríbete al campo **messages**. Si
   cambias `VerifyToken`, reinicia la API: la configuración se lee al arrancar.
3. Suscribe la cuenta de WhatsApp Business a tu app; sin esto Meta verifica el webhook pero no envía los mensajes.
   El id de la cuenta aparece junto al número de prueba:

   ```powershell
   $token = "<token del usuario del sistema>"
   Invoke-RestMethod -Method Post -Uri "https://graph.facebook.com/v23.0/<id de la cuenta de WhatsApp>/subscribed_apps" -Headers @{ Authorization = "Bearer $token" }
   ```

   Debe responder `success : True`.

## 6. Probar

1. Registra en FlowRide un pasajero o un conductor con **tu** número en formato E.164 (`+57…`). Sin Panel Web, usa las
   peticiones del final de `src/FlowRide.API/FlowRide.API.http` con el entorno `local` de Rider: la primera abre el
   inicio de sesión de Keycloak (administrador del operador, contraseña definida desde el correo de Mailpit).
2. Desde tu WhatsApp escríbele `hola` al número de prueba: el bot responde con el menú en unos segundos.
3. La conversación se ve en `GET /api/whatsapp/conversaciones/{teléfono}` (Panel Web).

## 7. Pasar el webhook por el borde común (Fase 10, ADR-0012)

Con el borde, Meta llama a un único callback para todos los operadores: el borde valida la firma, busca el operador por
el `phone_number_id` y reenvía el evento a su stack, que vuelve a validar la firma. La URL configurada en Meta **no
cambia** (`/api/whatsapp/webhook`); solo cambia a dónde apunta ngrok.

1. Registra la ruta del operador (subdominio, URL de su API y su `phone_number_id`, el mismo de `FlowRide:WhatsApp:PhoneNumberId`):
   `dotnet run --project src/FlowRide.Aprovisionamiento -- registrar-ruta --id tenant-a --url http://localhost:5237 --phone-number-id <id del número de prueba>`
2. Secretos del borde (una sola vez, en el almacén `flowride-borde`): la cadena del catálogo (la misma de la herramienta),
   y el `AppSecret` y el `VerifyToken` de la app de Meta (los mismos del paso 3, porque es una sola app):
   `dotnet user-secrets set "ConnectionStrings:Catalogo" "<cadena del catálogo>" --project src/FlowRide.Borde`
   `dotnet user-secrets set "Borde:WhatsApp:AppSecret" "<clave secreta de la app>" --project src/FlowRide.Borde`
   `dotnet user-secrets set "Borde:WhatsApp:VerifyToken" "<tu token de verificación>" --project src/FlowRide.Borde`
3. Levanta la API y el borde (`dotnet run --project src/FlowRide.Borde --launch-profile https`, queda en
   <https://localhost:8443>) y apunta ngrok al borde: `ngrok http --domain=<algo>.ngrok-free.dev https://localhost:8443`.
4. El Panel Web y la API del operador quedan en `https://tenant-a.dev.localhost:8443` (el certificado de desarrollo de
   .NET cubre `*.dev.localhost`; si el navegador no confía en él: `dotnet dev-certs https --trust`).

## Errores frecuentes de Meta

| Código | Qué pasa | Qué hacer |
|---|---|---|
| 131030 | El destinatario no está en la lista del número de prueba | Agregarlo en **Configuración de la API → Para** |
| 131047 | Pasaron más de 24 horas desde el último mensaje del usuario | El usuario escribe primero (las notificaciones con plantilla llegan en la Fase 8) |
| 190 | Token vencido o inválido | Generar el token del usuario del sistema (paso 2) |
| Webhook verificado, sin `POST` | La cuenta de WhatsApp no está suscrita a la app | `subscribed_apps` (paso 5.3) |
| `ERR_NGROK_121` | Agente de ngrok demasiado viejo | `ngrok update` |
| Webhook 401 | La firma no coincide | Revisar `AppSecret` |
| Verificación 403 | El token de verificación no coincide | Revisar `VerifyToken` |

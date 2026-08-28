# SIGOP

Portal Angular y servicios .NET para registrar y dar seguimiento a solicitudes
presupuestarias de varias entidades públicas. Valida contra un sistema legado SOAP,
confirma con un servicio bancario y procesa de forma asíncrona sobre RabbitMQ.

Diseño y decisiones técnicas: carpeta `docs/`.

## Requisitos

- .NET SDK 10.0
- Node.js 20 o superior
- PostgreSQL 17 en `localhost:5432`
- RabbitMQ en `localhost:5672`

Habilitar la consola de RabbitMQ (opcional, sirve para ver las colas):

```bash
rabbitmq-plugins enable rabbitmq_management
```

Los paquetes se restauran con `dotnet build` y `npm install`.

## Configuración

Hay que editar tres archivos y sustituir en ellos los marcadores `__NOMBRE__`:

```
backend/Sigop.Api/appsettings.json
backend/Sigop.Worker.Outbox/appsettings.json
backend/Sigop.Worker.Consumidor/appsettings.json
```

Cada instalación usa sus propias credenciales de PostgreSQL y RabbitMQ, y sus propias
claves. Ningún valor tiene que coincidir con el de otra instalación.

Valores:

| Marcador | Valor |
|---|---|
| `__USUARIO_BD__`, `__CONTRASENA_BD__` | credenciales de PostgreSQL |
| `__USUARIO_RABBIT__`, `__CONTRASENA_RABBIT__` | credenciales de RabbitMQ, por defecto `guest` / `guest` |
| `__CLAVE_FIRMA_BASE64_32_BYTES__` | firma del JWT, base64 de 32 bytes |
| `__CLAVE_CIFRADO_BASE64_32_BYTES__` | AES-GCM, base64 de 16, 24 o 32 bytes |

La clave de cifrado debe ser **la misma en los tres archivos**: la API cifra el NIT y la
cuenta bancaria al guardar, y el consumidor los descifra para llamar al banco.

Para generar las dos claves, en PowerShell:

```powershell
$b = New-Object byte[] 32
(New-Object Security.Cryptography.RNGCryptoServiceProvider).GetBytes($b)
[Convert]::ToBase64String($b)
```

Los datos de prueba guardan el NIT y la cuenta en claro, así que una clave recién generada
lee el juego de datos sin problema.

## Base de datos

El esquema son scripts SQL, no migraciones de EF Core.

```bash
psql -U postgres -c "CREATE DATABASE sigop;"
psql -U postgres -d sigop -f database/01-esquema.sql
psql -U postgres -d sigop -f database/02-datos-prueba.sql
```

- `01-esquema.sql`: 7 tablas, 1 secuencia, índices y la vista `vw_trazabilidad`.
- `02-datos-prueba.sql`: 2 entidades, 4 unidades ejecutoras, 7 solicitudes y 29 cambios de estado.

Los usuarios se crean solos al arrancar la API.

Para recrear la base hay que cerrar antes las conexiones abiertas, pgAdmin las mantiene:

```bash
psql -U postgres -c "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname='sigop' AND pid <> pg_backend_pid();"
```

## Ejecución

```bash
dotnet build backend/Sigop.sln
```

Tres procesos, en terminales separadas:

```bash
dotnet run --project backend/Sigop.Api
dotnet run --project backend/Sigop.Worker.Outbox
dotnet run --project backend/Sigop.Worker.Consumidor
```

| Proceso | Función | Puerto |
|---|---|---|
| `Sigop.Api` | API REST, autenticación y legado SOAP simulado | 5005 |
| `Sigop.Worker.Outbox` | publica a RabbitMQ los mensajes pendientes | |
| `Sigop.Worker.Consumidor` | orquesta validación y confirmación bancaria | |

Portal:

```bash
cd frontend/sigop-portal
npm install
npm start
```

- Portal: `http://localhost:4200`
- Swagger: `http://localhost:5005/swagger`
- Health check: `http://localhost:5005/health`

La URL de la API se configura en `src/environments/environment.ts`.

## Usuarios de prueba

Contraseña `12345` para ambos. Cada uno ve solo las solicitudes de su entidad.

| Usuario | Nombre | Entidad |
|---|---|---|
| `jperez` | Juan Perez | 11130007, Ministerio de Finanzas Públicas |
| `rmorales` | Rosa Morales | 11130008, Ministerio de Gobernación |

## Escenarios

El resultado depende del monto:

| Monto | Legado | Banco | Estado final |
|---|---|---|---|
| menor a Q500,000 | disponible | confirma | Ejecutada |
| Q500,000 a Q1,000,000 | disponible | agota el tiempo | Fallida, reprocesable |
| mayor a Q1,000,000 | sin disponibilidad | no se invoca | Rechazada |

Umbrales: `Banco:MontoQueFalla` en el appsettings del consumidor, y `TechoDisponible` en
`LegadoController.cs`.

Una solicitud fallida se reprocesa desde la pantalla de detalle, hasta 3 veces.

Para ver la escalera de reintentos y la cola de mensajes muertos, detener la API mientras
el consumidor sigue arriba: el legado simulado vive dentro de la API, así que la llamada
falla y el consumidor reintenta cada 30 segundos, 3 veces, antes de marcar la solicitud
como fallida.

## Estructura

```
backend/
  Sigop.Dominio            agregado, máquina de estados, eventos
  Sigop.Aplicacion         casos de uso, puertos y DTOs
  Sigop.Infraestructura    EF Core, RabbitMQ, seguridad, integraciones
  Sigop.Api                controladores REST y legado SOAP simulado
  Sigop.Worker.Outbox      despachador del outbox
  Sigop.Worker.Consumidor  consumidor orquestador
database/                  esquema y datos de prueba
docs/                      documentos de diseño
frontend/sigop-portal/     portal Angular
```

`Sigop.Dominio` no tiene paquetes NuGet ni referencias a otros proyectos.

## Trazabilidad

Cada petición lleva la cabecera `X-Correlation-Id`. Si no viene, la API genera una
(`K7M2-9PQR-4WXY`) y la devuelve en la respuesta. Ese identificador queda en el historial
de estados y en el outbox, y viaja en los mensajes de RabbitMQ.

```sql
SELECT * FROM vw_trazabilidad WHERE numero = 'SOL-2026-000002' ORDER BY momento;
```

## Endpoints

| Método | Ruta | Descripción |
|---|---|---|
| `POST` | `/api/auth/login` | autenticación, devuelve el JWT |
| `GET` | `/api/unidades` | unidades ejecutoras de la entidad del usuario |
| `POST` | `/api/solicitudes` | registro; 201 si es síncrona, 202 si es asíncrona |
| `GET` | `/api/solicitudes` | listado con filtros y paginación |
| `GET` | `/api/solicitudes/{id}` | detalle con historial |
| `PUT` | `/api/solicitudes/{id}` | actualización, solo en estado Registrada |
| `POST` | `/api/solicitudes/{id}/estado` | cambio de estado manual |
| `POST` | `/api/solicitudes/{id}/reproceso` | reproceso de una operación fallida |
| `POST` | `/legado/presupuesto` | servicio SOAP simulado del sistema central |
| `GET` | `/health` | estado de la API y de la base de datos |

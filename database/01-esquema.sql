CREATE TABLE entidades (
    id         bigint       PRIMARY KEY,
    nombre     varchar(200) NOT NULL,
    tipo       varchar(30)  NOT NULL,
    estado     varchar(20)  NOT NULL DEFAULT 'ACTIVA',
    creado_en  timestamptz  NOT NULL DEFAULT now(),

    CONSTRAINT ck_entidades_tipo   CHECK (tipo IN ('CENTRALIZADA','DESCENTRALIZADA','AUTONOMA','MUNICIPAL')),
    CONSTRAINT ck_entidades_estado CHECK (estado IN ('ACTIVA','INACTIVA'))
);

CREATE TABLE unidades_ejecutoras (
    id          bigint       PRIMARY KEY,
    entidad_id  bigint       NOT NULL REFERENCES entidades(id),
    codigo      varchar(10)  NOT NULL,
    nombre      varchar(200) NOT NULL,
    estado      varchar(20)  NOT NULL DEFAULT 'ACTIVA',
    creado_en   timestamptz  NOT NULL DEFAULT now(),

    CONSTRAINT ux_unidades_entidad_codigo UNIQUE (entidad_id, codigo),
    CONSTRAINT ck_unidades_estado CHECK (estado IN ('ACTIVA','INACTIVA'))
);

CREATE TABLE usuarios (
    id                   bigserial    PRIMARY KEY,
    usuario              varchar(50)  NOT NULL,
    nombre               varchar(200) NOT NULL,
    hash_password        varchar(500) NOT NULL,
    entidad_id           bigint       NOT NULL REFERENCES entidades(id),
    estado               varchar(20)  NOT NULL DEFAULT 'ACTIVO',
    intentos_fallidos    smallint     NOT NULL DEFAULT 0,
    bloqueado_hasta      timestamptz  NULL,
    ultimo_acceso        timestamptz  NULL,
    creado_en            timestamptz  NOT NULL DEFAULT now(),

    CONSTRAINT ux_usuarios_usuario UNIQUE (usuario),
    CONSTRAINT ck_usuarios_estado  CHECK (estado IN ('ACTIVO','INACTIVO','BLOQUEADO'))
);

CREATE SEQUENCE seq_solicitud START 1;

CREATE TABLE solicitudes (
    id                   uuid          PRIMARY KEY,
    numero               varchar(20)   NOT NULL,
    entidad_id           bigint        NOT NULL REFERENCES entidades(id),
    unidad_ejecutora_id  bigint        NOT NULL REFERENCES unidades_ejecutoras(id),
    tipo_operacion       varchar(40)   NOT NULL,
    monto                numeric(18,2) NOT NULL,
    moneda               char(3)       NOT NULL,
    concepto             varchar(500)  NOT NULL,
    beneficiario_nombre  varchar(200)  NULL,
    beneficiario_nit     varchar(500)  NULL,
    cuenta_bancaria      varchar(500)  NULL,
    estado               varchar(20)   NOT NULL,
    intentos_reproceso   smallint      NOT NULL DEFAULT 0,
    ultimo_error         varchar(1000) NULL,
    correlation_id       varchar(50)   NOT NULL,
    creado_por           varchar(50)   NOT NULL,
    creado_en            timestamptz   NOT NULL DEFAULT now(),
    actualizado_por      varchar(50)   NULL,
    actualizado_en       timestamptz   NOT NULL DEFAULT now(),

    CONSTRAINT ux_solicitudes_numero UNIQUE (numero),
    CONSTRAINT ck_solicitudes_monto  CHECK (monto > 0),
    CONSTRAINT ck_solicitudes_tipo   CHECK (tipo_operacion IN (
        'PAGO_PROVEEDOR','TRANSFERENCIA_PRESUPUESTARIA',
        'MODIFICACION_PRESUPUESTARIA','CONSULTA_DISPONIBILIDAD')),
    CONSTRAINT ck_solicitudes_estado CHECK (estado IN (
        'REGISTRADA','EN_VALIDACION','VALIDADA','RECHAZADA',
        'EN_PROCESO','EJECUTADA','FALLIDA','ANULADA'))
);

CREATE INDEX ix_solicitudes_entidad_estado ON solicitudes (entidad_id, estado, creado_en DESC);
CREATE INDEX ix_solicitudes_correlation    ON solicitudes (correlation_id);

CREATE TABLE historial_estados (
    id               bigserial     PRIMARY KEY,
    solicitud_id     uuid          NOT NULL REFERENCES solicitudes(id),
    estado_anterior  varchar(20)   NULL,
    estado_nuevo     varchar(20)   NOT NULL,
    motivo           varchar(1000) NOT NULL,
    origen           varchar(20)   NOT NULL,
    realizado_por    varchar(50)   NOT NULL,
    correlation_id   varchar(50)   NOT NULL,
    ocurrido_en      timestamptz   NOT NULL DEFAULT now(),

    CONSTRAINT ck_historial_origen CHECK (origen IN ('USUARIO','VALIDACION','SAGA','REPROCESO'))
);

CREATE INDEX ix_historial_solicitud ON historial_estados (solicitud_id, ocurrido_en DESC);

CREATE TABLE outbox_mensajes (
    id               uuid         PRIMARY KEY,
    exchange         varchar(100) NOT NULL,
    routing_key      varchar(200) NOT NULL,
    tipo_mensaje     varchar(150) NOT NULL,
    contenido        jsonb        NOT NULL,
    entidad_id       bigint       NOT NULL,
    solicitud_numero varchar(20)  NULL,
    correlation_id   varchar(50)  NOT NULL,
    estado           varchar(20)  NOT NULL DEFAULT 'PENDIENTE',
    intentos         smallint     NOT NULL DEFAULT 0,
    creado_en        timestamptz  NOT NULL DEFAULT now(),
    publicado_en     timestamptz  NULL,

    CONSTRAINT ck_outbox_estado CHECK (estado IN ('PENDIENTE','PUBLICADO','FALLIDO'))
);

CREATE INDEX ix_outbox_solicitud ON outbox_mensajes (solicitud_numero);

CREATE INDEX ix_outbox_pendientes ON outbox_mensajes (creado_en)
    WHERE estado = 'PENDIENTE';

CREATE TABLE mensajes_procesados (
    message_id    uuid         PRIMARY KEY,
    procesado_en  timestamptz  NOT NULL DEFAULT now()
);

CREATE INDEX ix_mensajes_procesados_fecha ON mensajes_procesados (procesado_en);


CREATE VIEW vw_trazabilidad AS
SELECT
    s.numero                                                        AS numero,
    h.ocurrido_en                                                   AS momento,
    'CAMBIO_ESTADO'                                                 AS tipo,
    coalesce(h.estado_anterior, '-') || ' -> ' || h.estado_nuevo     AS detalle,
    h.origen                                                        AS origen,
    h.realizado_por                                                 AS actor,
    h.motivo                                                        AS motivo,
    h.correlation_id                                                AS correlacion
FROM historial_estados h
JOIN solicitudes s ON s.id = h.solicitud_id

UNION ALL

SELECT
    o.solicitud_numero,
    o.creado_en,
    'MENSAJE',
    o.tipo_mensaje || ' [' || o.estado || ']',
    o.entidad_id::text,
    'outbox',
    o.routing_key,
    o.correlation_id
FROM outbox_mensajes o
WHERE o.solicitud_numero IS NOT NULL;

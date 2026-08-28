SET client_encoding TO 'UTF8';

INSERT INTO entidades (id, nombre, tipo) VALUES
    (11130007, 'Ministerio de Finanzas Públicas', 'CENTRALIZADA'),
    (11130008, 'Ministerio de Gobernación',       'CENTRALIZADA');

INSERT INTO unidades_ejecutoras (id, entidad_id, codigo, nombre) VALUES
    (11130007001, 11130007, '001', 'Dirección Técnica del Presupuesto'),
    (11130007002, 11130007, '002', 'Tesorería Nacional'),
    (11130008001, 11130008, '001', 'Dirección de Planificación y Presupuesto'),
    (11130008002, 11130008, '002', 'Dirección General de la Policía Nacional Civil');

INSERT INTO solicitudes (
    id, numero, entidad_id, unidad_ejecutora_id, tipo_operacion, monto, moneda, concepto,
    beneficiario_nombre, beneficiario_nit, cuenta_bancaria, estado, intentos_reproceso,
    ultimo_error, correlation_id, creado_por, creado_en, actualizado_por, actualizado_en
) VALUES
    ('a0000001-0000-0000-0000-000000000001', 'SOL-2026-000001',
     11130007, 11130007001,
     'TRANSFERENCIA_PRESUPUESTARIA', 125000.00, 'GTQ',
     'Traslado de partida para adquisición de insumos médicos',
     NULL, NULL, NULL, 'REGISTRADA', 0, NULL,
     'seed-0001', 'jperez', now() - interval '2 hours', NULL, now() - interval '2 hours'),

    ('a0000002-0000-0000-0000-000000000002', 'SOL-2026-000002',
     11130007, 11130007002,
     'PAGO_PROVEEDOR', 87450.00, 'GTQ',
     'Pago por servicios de mantenimiento de equipo informático',
     'Suministros del Valle, S.A.', '1234567-8', '0011-2233-4455', 'EJECUTADA', 0, NULL,
     'seed-0002', 'jperez', now() - interval '5 days', 'saga', now() - interval '5 days' + interval '12 minutes'),

    ('a0000003-0000-0000-0000-000000000003', 'SOL-2026-000003',
     11130007, 11130007001,
     'PAGO_PROVEEDOR', 2300000.00, 'GTQ',
     'Adquisición de vehículos para supervisión regional',
     'Automotores Centrales, S.A.', '9876543-2', '0099-8877-6655', 'RECHAZADA', 0, NULL,
     'seed-0003', 'jperez', now() - interval '3 days', 'validacion-legado', now() - interval '3 days' + interval '4 minutes'),

    ('a0000004-0000-0000-0000-000000000004', 'SOL-2026-000004',
     11130007, 11130007002,
     'PAGO_PROVEEDOR', 45000.00, 'GTQ',
     'Pago de arrendamiento de bodega regional',
     'Inmobiliaria del Norte, S.A.', '5555555-1', '0044-5566-7788', 'FALLIDA', 0,
     'Tiempo de espera agotado al confirmar la operación con el sistema bancario.',
     'seed-0004', 'jperez', now() - interval '1 day', 'saga', now() - interval '1 day' + interval '9 minutes'),

    ('a0000005-0000-0000-0000-000000000005', 'SOL-2026-000005',
     11130008, 11130008001,
     'MODIFICACION_PRESUPUESTARIA', 500000.00, 'GTQ',
     'Ampliación presupuestaria para equipamiento policial',
     NULL, NULL, NULL, 'REGISTRADA', 0, NULL,
     'seed-0005', 'rmorales', now() - interval '6 hours', NULL, now() - interval '6 hours'),

    ('a0000006-0000-0000-0000-000000000006', 'SOL-2026-000006',
     11130008, 11130008002,
     'PAGO_PROVEEDOR', 132800.00, 'GTQ',
     'Pago de combustible para patrullaje departamental',
     'Combustibles Unidos, S.A.', '2233445-6', '0077-1122-3344', 'EJECUTADA', 0, NULL,
     'seed-0006', 'rmorales', now() - interval '4 days', 'saga', now() - interval '4 days' + interval '11 minutes'),

    ('a0000007-0000-0000-0000-000000000007', 'SOL-2026-000007',
     11130008, 11130008002,
     'PAGO_PROVEEDOR', 780000.00, 'GTQ',
     'Adquisición de chalecos antibalas para la fuerza de tarea',
     'Equipamiento Táctico, S.A.', '7788990-3', '0055-9988-7766', 'FALLIDA', 1,
     'Tiempo de espera agotado al confirmar la operación con el sistema bancario.',
     'seed-0007', 'rmorales', now() - interval '2 days', 'saga', now() - interval '2 days' + interval '22 minutes');

INSERT INTO historial_estados (
    solicitud_id, estado_anterior, estado_nuevo, motivo, origen, realizado_por, correlation_id, ocurrido_en
) VALUES
    ('a0000001-0000-0000-0000-000000000001', NULL, 'REGISTRADA',
     'Solicitud registrada por el usuario.', 'USUARIO', 'jperez', 'seed-0001', now() - interval '2 hours'),

    ('a0000002-0000-0000-0000-000000000002', NULL, 'REGISTRADA',
     'Solicitud registrada por el usuario.', 'USUARIO', 'jperez', 'seed-0002', now() - interval '5 days'),
    ('a0000002-0000-0000-0000-000000000002', 'REGISTRADA', 'EN_VALIDACION',
     'Inicio de validación contra el sistema legado.', 'VALIDACION', 'validacion-legado', 'seed-0002', now() - interval '5 days' + interval '2 minutes'),
    ('a0000002-0000-0000-0000-000000000002', 'EN_VALIDACION', 'VALIDADA',
     'Disponibilidad presupuestaria confirmada. Saldo disponible 1,250,000.00.', 'VALIDACION', 'validacion-legado', 'seed-0002', now() - interval '5 days' + interval '3 minutes'),
    ('a0000002-0000-0000-0000-000000000002', 'VALIDADA', 'EN_PROCESO',
     'Reserva presupuestaria RES-20260822-4F2A1B creada.', 'SAGA', 'saga', 'seed-0002', now() - interval '5 days' + interval '5 minutes'),
    ('a0000002-0000-0000-0000-000000000002', 'EN_PROCESO', 'EJECUTADA',
     'Operación confirmada por el sistema bancario. Comprobante CNF-20260822-8B3C9D.', 'SAGA', 'saga', 'seed-0002', now() - interval '5 days' + interval '12 minutes'),

    ('a0000003-0000-0000-0000-000000000003', NULL, 'REGISTRADA',
     'Solicitud registrada por el usuario.', 'USUARIO', 'jperez', 'seed-0003', now() - interval '3 days'),
    ('a0000003-0000-0000-0000-000000000003', 'REGISTRADA', 'EN_VALIDACION',
     'Inicio de validación contra el sistema legado.', 'VALIDACION', 'validacion-legado', 'seed-0003', now() - interval '3 days' + interval '2 minutes'),
    ('a0000003-0000-0000-0000-000000000003', 'EN_VALIDACION', 'RECHAZADA',
     'SIN_DISPONIBILIDAD: el monto solicitado excede el saldo de la unidad ejecutora.', 'VALIDACION', 'validacion-legado', 'seed-0003', now() - interval '3 days' + interval '4 minutes'),

    ('a0000004-0000-0000-0000-000000000004', NULL, 'REGISTRADA',
     'Solicitud registrada por el usuario.', 'USUARIO', 'jperez', 'seed-0004', now() - interval '1 day'),
    ('a0000004-0000-0000-0000-000000000004', 'REGISTRADA', 'EN_VALIDACION',
     'Inicio de validación contra el sistema legado.', 'VALIDACION', 'validacion-legado', 'seed-0004', now() - interval '1 day' + interval '2 minutes'),
    ('a0000004-0000-0000-0000-000000000004', 'EN_VALIDACION', 'VALIDADA',
     'Disponibilidad presupuestaria confirmada.', 'VALIDACION', 'validacion-legado', 'seed-0004', now() - interval '1 day' + interval '3 minutes'),
    ('a0000004-0000-0000-0000-000000000004', 'VALIDADA', 'EN_PROCESO',
     'Reserva presupuestaria RES-20260826-1A7E4C creada.', 'SAGA', 'saga', 'seed-0004', now() - interval '1 day' + interval '5 minutes'),
    ('a0000004-0000-0000-0000-000000000004', 'EN_PROCESO', 'FALLIDA',
     'Tiempo de espera agotado al confirmar la operación con el sistema bancario.', 'SAGA', 'saga', 'seed-0004', now() - interval '1 day' + interval '9 minutes'),

    ('a0000005-0000-0000-0000-000000000005', NULL, 'REGISTRADA',
     'Solicitud registrada por el usuario.', 'USUARIO', 'rmorales', 'seed-0005', now() - interval '6 hours'),

    ('a0000006-0000-0000-0000-000000000006', NULL, 'REGISTRADA',
     'Solicitud registrada por el usuario.', 'USUARIO', 'rmorales', 'seed-0006', now() - interval '4 days'),
    ('a0000006-0000-0000-0000-000000000006', 'REGISTRADA', 'EN_VALIDACION',
     'Inicio de validación contra el sistema legado.', 'VALIDACION', 'validacion-legado', 'seed-0006', now() - interval '4 days' + interval '2 minutes'),
    ('a0000006-0000-0000-0000-000000000006', 'EN_VALIDACION', 'VALIDADA',
     'Disponibilidad presupuestaria confirmada. Saldo disponible 867,200.00.', 'VALIDACION', 'validacion-legado', 'seed-0006', now() - interval '4 days' + interval '4 minutes'),
    ('a0000006-0000-0000-0000-000000000006', 'VALIDADA', 'EN_PROCESO',
     'Reserva presupuestaria RES-20260824-9B3C7D creada.', 'SAGA', 'saga', 'seed-0006', now() - interval '4 days' + interval '6 minutes'),
    ('a0000006-0000-0000-0000-000000000006', 'EN_PROCESO', 'EJECUTADA',
     'Operación confirmada por el sistema bancario. Comprobante CNF-20260824-1E5F2A.', 'SAGA', 'saga', 'seed-0006', now() - interval '4 days' + interval '11 minutes'),

    ('a0000007-0000-0000-0000-000000000007', NULL, 'REGISTRADA',
     'Solicitud registrada por el usuario.', 'USUARIO', 'rmorales', 'seed-0007', now() - interval '2 days'),
    ('a0000007-0000-0000-0000-000000000007', 'REGISTRADA', 'EN_VALIDACION',
     'Inicio de validación contra el sistema legado.', 'VALIDACION', 'validacion-legado', 'seed-0007', now() - interval '2 days' + interval '2 minutes'),
    ('a0000007-0000-0000-0000-000000000007', 'EN_VALIDACION', 'VALIDADA',
     'Disponibilidad presupuestaria confirmada.', 'VALIDACION', 'validacion-legado', 'seed-0007', now() - interval '2 days' + interval '3 minutes'),
    ('a0000007-0000-0000-0000-000000000007', 'VALIDADA', 'EN_PROCESO',
     'Reserva presupuestaria RES-20260826-3C8A1F creada.', 'SAGA', 'saga', 'seed-0007', now() - interval '2 days' + interval '5 minutes'),
    ('a0000007-0000-0000-0000-000000000007', 'EN_PROCESO', 'FALLIDA',
     'Tiempo de espera agotado al confirmar la operación con el sistema bancario.', 'SAGA', 'saga', 'seed-0007', now() - interval '2 days' + interval '9 minutes'),
    ('a0000007-0000-0000-0000-000000000007', 'FALLIDA', 'EN_VALIDACION',
     'Reproceso 1 de 3 solicitado tras incidencia del proveedor bancario.', 'REPROCESO', 'cflores', 'seed-0007', now() - interval '2 days' + interval '15 minutes'),
    ('a0000007-0000-0000-0000-000000000007', 'EN_VALIDACION', 'VALIDADA',
     'Disponibilidad presupuestaria confirmada.', 'VALIDACION', 'validacion-legado', 'seed-0007', now() - interval '2 days' + interval '16 minutes'),
    ('a0000007-0000-0000-0000-000000000007', 'VALIDADA', 'EN_PROCESO',
     'Reserva presupuestaria RES-20260826-3C8A1F creada.', 'SAGA', 'saga', 'seed-0007', now() - interval '2 days' + interval '18 minutes'),
    ('a0000007-0000-0000-0000-000000000007', 'EN_PROCESO', 'FALLIDA',
     'Tiempo de espera agotado al confirmar la operación con el sistema bancario.', 'SAGA', 'saga', 'seed-0007', now() - interval '2 days' + interval '22 minutes');

SELECT setval('seq_solicitud', 7);

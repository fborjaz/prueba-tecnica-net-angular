/* =============================================================================
   03 - Datos iniciales (catálogos, menú por rol, servicios, cajas)
   Los usuarios iniciales (administrador, gestor y cajero de demostración) los
   crea la API al iniciar, porque la contraseña se guarda con hash BCrypt y el
   correo / identificación cifrados con AES (ver Backend/README.md).
   ============================================================================= */

SET IDENTITY_INSERT rol ON;
INSERT INTO rol (rolid, rolname) VALUES
    (1, 'ADMINISTRADOR'),
    (2, 'GESTOR'),
    (3, 'CAJERO'),
    (4, 'CLIENTE');
SET IDENTITY_INSERT rol OFF;
GO

INSERT INTO userstatus (statusid, description) VALUES
    ('ACT', 'Activo'),
    ('PEN', 'Pendiente de aprobación'),
    ('INA', 'Inactivo'),
    ('BLO', 'Bloqueado');
GO

-- El código del tipo de atención es el prefijo del turno (2 letras mayúsculas)
INSERT INTO attentiontype (attentiontypeid, description) VALUES
    ('AC', 'Atención al cliente'),
    ('CT', 'Contratación de servicio'),
    ('PS', 'Pago de servicio'),
    ('CS', 'Cambio de servicio'),
    ('CF', 'Cambio de forma de pago'),
    ('CC', 'Cancelación de contrato');
GO

INSERT INTO attentionstatus (statusid, description) VALUES
    (1, 'Pendiente'),
    (2, 'En atención'),
    (3, 'Atendido'),
    (4, 'Anulado');
GO

INSERT INTO statuscontract (statusid, description) VALUES
    ('VIG', 'Contrato vigente'),
    ('REN', 'Renovación de servicio'),
    ('SUS', 'Contrato sustituido'),
    ('CAN', 'Contrato cancelado');
GO

INSERT INTO methodpayment (description) VALUES
    ('Efectivo'),
    ('Tarjeta de crédito'),
    ('Tarjeta de débito'),
    ('Transferencia bancaria'),
    ('Débito automático');
GO

INSERT INTO service (servicename, servicedescription, speedmbps, price) VALUES
    ('Hogar Básico',   'Plan residencial para navegación y redes sociales',      100,  20.00),
    ('Hogar Plus',     'Plan residencial para streaming HD y teletrabajo',        300,  29.90),
    ('Hogar Gamer',    'Baja latencia, ideal para juegos en línea y 4K',          600,  39.90),
    ('Fibra Ultra',    'Máxima velocidad simétrica para hogares conectados',      1000, 54.90),
    ('Pyme Negocios',  'Plan empresarial con IP fija y soporte prioritario',      500,  69.90);
GO

INSERT INTO device (devicename, service_serviceid) VALUES
    ('Router WiFi 5', 1),
    ('ONT GPON', 1),
    ('Router WiFi 6', 2),
    ('ONT GPON', 2),
    ('Router WiFi 6 Gaming', 3),
    ('ONT GPON', 3),
    ('Router WiFi 6E', 4),
    ('ONT XGS-PON', 4),
    ('Repetidor Mesh', 4),
    ('Router empresarial', 5),
    ('ONT GPON', 5);
GO

INSERT INTO cash (cashdescription) VALUES
    ('Caja 01 - Matriz'),
    ('Caja 02 - Matriz'),
    ('Caja 03 - Sucursal Norte');
GO

/* ---------------------------------- Menú ----------------------------------
   El frontend construye el sidebar a partir de GET /api/menu (según el rol del
   JWT). El campo icon es una clave que el frontend traduce a un ícono SVG.
---------------------------------------------------------------------------- */
SET IDENTITY_INSERT menu ON;
INSERT INTO menu (menuid, name, route, icon, parentid, sortorder) VALUES
    (1, 'Inicio',            '/welcome',   'home',      NULL, 1),
    (2, 'Dashboard',         '/dashboard', 'chart',     NULL, 2),
    (3, 'Usuarios',          '/users',     'users',     NULL, 3),
    (4, 'Cajas',             '/cash',      'cash',      NULL, 4),
    (5, 'Turnos',            '/turns',     'ticket',    NULL, 5),
    (6, 'Procesos de caja',  '/cashier',   'register',  NULL, 6),
    (7, 'Clientes',          '/clients',   'clients',   NULL, 7),
    (8, 'Servicios',         '/services',  'wifi',      NULL, 8);
SET IDENTITY_INSERT menu OFF;
GO

INSERT INTO rolmenu (rol_rolid, menu_menuid) VALUES
    -- ADMINISTRADOR
    (1, 1), (1, 2), (1, 3), (1, 4), (1, 8),
    -- GESTOR
    (2, 1), (2, 3), (2, 4), (2, 5),
    -- CAJERO
    (3, 1), (3, 6), (3, 7);
GO

/* --------------------------- Clientes de demostración --------------------- */
INSERT INTO client (name, lastname, identification, email, phonenumber, address, referenceaddress) VALUES
    ('Juan Carlos', 'Pérez Mendoza', '0912345671', 'juan.perez@correo.com',  '0987654321',
     'Av. 9 de Octubre 1234 y Malecón, Guayaquil', 'Frente al parque Centenario, edificio azul'),
    ('Ana Lucía',   'Torres Ruiz',   '0923456782', 'ana.torres@correo.com',  '0991234567',
     'Cdla. Kennedy Norte Mz. 105 Villa 12',       'Junto a la farmacia, portón negro de metal'),
    ('Pedro José',  'Salazar León',  '1712345678001', 'pedro.salazar@empresa.com', '0976543210',
     'Calle Amazonas N34-56 y Naciones Unidas, Quito', 'Edificio Torre Azul, piso 5 oficina 502');
GO

INSERT INTO contract (startdate, enddate, service_serviceid, statuscontract_statusid, client_clientid, methodpayment_methodpaymentid)
VALUES (DATEADD(MONTH, -2, SYSDATETIMEOFFSET()), DATEADD(MONTH, 10, SYSDATETIMEOFFSET()), 2, 'VIG', 1, 1);
GO

/* =============================================================================
   01 - Esquema de base de datos (SQL Server)
   Basado en el esquema de ejemplo de la prueba, con los ajustes necesarios:
   - users: nombres, identificación, hashes de búsqueda (email/identificación van
     cifrados con AES), control de sesión, intentos fallidos y borrado lógico.
   - service: velocidad (Mbps) y precio DECIMAL.
   - payments: monto, contrato y forma de pago.
   - turn: tipo de atención (define el prefijo del código AC0001, PS0001...).
   - cashsession: controla que 2 usuarios no operen la misma caja a la vez.
   - menu / rolmenu: menú dinámico por rol.
   - passwordreset: recuperación de contraseña.
   - isdeleted en las tablas transaccionales (eliminación lógica).
   ============================================================================= */

/* ---------------------------- Seguridad / usuarios ------------------------- */
CREATE TABLE rol (
    rolid       INT IDENTITY(1,1) NOT NULL CONSTRAINT rol_PK PRIMARY KEY,
    rolname     VARCHAR(50)       NOT NULL CONSTRAINT rol_UQ_name UNIQUE
);
GO

CREATE TABLE userstatus (
    statusid    VARCHAR(3)  NOT NULL CONSTRAINT userstatus_PK PRIMARY KEY,
    description VARCHAR(50) NOT NULL
);
GO

CREATE TABLE users (
    userid              INT IDENTITY(1,1) NOT NULL CONSTRAINT user_PK PRIMARY KEY,
    username            VARCHAR(20)    NOT NULL CONSTRAINT user_UQ_username UNIQUE,
    names               VARCHAR(50)    NOT NULL,
    lastnames           VARCHAR(50)    NOT NULL,
    email               VARCHAR(400)   NOT NULL,              -- cifrado AES-256
    emailhash           CHAR(64)       NOT NULL CONSTRAINT user_UQ_emailhash UNIQUE,          -- HMAC-SHA256 para búsqueda
    identification      VARCHAR(400)   NOT NULL,              -- cifrado AES-256
    identificationhash  CHAR(64)       NOT NULL CONSTRAINT user_UQ_identificationhash UNIQUE, -- HMAC-SHA256 para búsqueda
    password            VARCHAR(100)   NOT NULL,              -- hash BCrypt
    rol_rolid           INT            NOT NULL CONSTRAINT user_rol_FK REFERENCES rol(rolid),
    creationdate        DATETIMEOFFSET NOT NULL CONSTRAINT user_DF_creationdate DEFAULT SYSDATETIMEOFFSET(),
    usercreate          INT            NULL CONSTRAINT user_usercreate_FK REFERENCES users(userid),
    userapproval        INT            NULL CONSTRAINT user_userapproval_FK REFERENCES users(userid),
    dateapproval        DATETIMEOFFSET NULL,
    userstatus_statusid VARCHAR(3)     NOT NULL CONSTRAINT user_userstatus_FK REFERENCES userstatus(statusid),
    sessionactive       BIT            NOT NULL CONSTRAINT user_DF_sessionactive DEFAULT 0,
    lastlogin           DATETIMEOFFSET NULL,
    lastlogout          DATETIMEOFFSET NULL,
    failedattempts      INT            NOT NULL CONSTRAINT user_DF_failedattempts DEFAULT 0,
    isdeleted           BIT            NOT NULL CONSTRAINT user_DF_isdeleted DEFAULT 0
);
GO

CREATE TABLE passwordreset (
    passwordresetid INT IDENTITY(1,1) NOT NULL CONSTRAINT passwordreset_PK PRIMARY KEY,
    user_userid     INT            NOT NULL CONSTRAINT passwordreset_user_FK REFERENCES users(userid),
    tokenhash       CHAR(64)       NOT NULL,
    expiresat       DATETIMEOFFSET NOT NULL,
    used            BIT            NOT NULL CONSTRAINT passwordreset_DF_used DEFAULT 0,
    createdat       DATETIMEOFFSET NOT NULL CONSTRAINT passwordreset_DF_createdat DEFAULT SYSDATETIMEOFFSET()
);
GO

/* ---------------------------------- Menú ---------------------------------- */
CREATE TABLE menu (
    menuid    INT IDENTITY(1,1) NOT NULL CONSTRAINT menu_PK PRIMARY KEY,
    name      VARCHAR(50)  NOT NULL,
    route     VARCHAR(100) NOT NULL,
    icon      VARCHAR(50)  NOT NULL,
    parentid  INT          NULL CONSTRAINT menu_parent_FK REFERENCES menu(menuid),
    sortorder INT          NOT NULL CONSTRAINT menu_DF_sortorder DEFAULT 0,
    active    BIT          NOT NULL CONSTRAINT menu_DF_active DEFAULT 1
);
GO

CREATE TABLE rolmenu (
    rol_rolid   INT NOT NULL CONSTRAINT rolmenu_rol_FK REFERENCES rol(rolid),
    menu_menuid INT NOT NULL CONSTRAINT rolmenu_menu_FK REFERENCES menu(menuid),
    CONSTRAINT rolmenu_PK PRIMARY KEY (rol_rolid, menu_menuid)
);
GO

/* ---------------------------------- Cajas --------------------------------- */
CREATE TABLE cash (
    cashid          INT IDENTITY(1,1) NOT NULL CONSTRAINT cash_PK PRIMARY KEY,
    cashdescription VARCHAR(50) NOT NULL,
    active          BIT         NOT NULL CONSTRAINT cash_DF_active DEFAULT 1,
    isdeleted       BIT         NOT NULL CONSTRAINT cash_DF_isdeleted DEFAULT 0
);
GO

-- Asignación de cajeros a cajas (máximo 2 activos por caja, validado en API y SP)
CREATE TABLE usercash (
    user_userid INT            NOT NULL CONSTRAINT usercash_user_FK REFERENCES users(userid),
    cash_cashid INT            NOT NULL CONSTRAINT usercash_cash_FK REFERENCES cash(cashid),
    assignedby  INT            NULL CONSTRAINT usercash_assignedby_FK REFERENCES users(userid),
    assignedat  DATETIMEOFFSET NOT NULL CONSTRAINT usercash_DF_assignedat DEFAULT SYSDATETIMEOFFSET(),
    active      BIT            NOT NULL CONSTRAINT usercash_DF_active DEFAULT 1,
    CONSTRAINT usercash_PK PRIMARY KEY (user_userid, cash_cashid)
);
GO

-- Sesiones de caja: una sola sesión abierta por caja y por usuario (índices filtrados)
CREATE TABLE cashsession (
    cashsessionid INT IDENTITY(1,1) NOT NULL CONSTRAINT cashsession_PK PRIMARY KEY,
    cash_cashid   INT            NOT NULL CONSTRAINT cashsession_cash_FK REFERENCES cash(cashid),
    user_userid   INT            NOT NULL CONSTRAINT cashsession_user_FK REFERENCES users(userid),
    openedat      DATETIMEOFFSET NOT NULL CONSTRAINT cashsession_DF_openedat DEFAULT SYSDATETIMEOFFSET(),
    closedat      DATETIMEOFFSET NULL
);
GO
CREATE UNIQUE INDEX cashsession_UQ_open_cash ON cashsession(cash_cashid) WHERE closedat IS NULL;
CREATE UNIQUE INDEX cashsession_UQ_open_user ON cashsession(user_userid) WHERE closedat IS NULL;
GO

/* --------------------------------- Clientes -------------------------------- */
CREATE TABLE client (
    clientid         INT IDENTITY(1,1) NOT NULL CONSTRAINT client_PK PRIMARY KEY,
    name             VARCHAR(50)    NOT NULL,
    lastname         VARCHAR(50)    NOT NULL,
    identification   VARCHAR(13)    NOT NULL CONSTRAINT client_UQ_identification UNIQUE,
    email            VARCHAR(120)   NOT NULL,
    phonenumber      VARCHAR(13)    NOT NULL,
    address          VARCHAR(100)   NOT NULL,
    referenceaddress VARCHAR(100)   NOT NULL,
    active           BIT            NOT NULL CONSTRAINT client_DF_active DEFAULT 1,
    createdat        DATETIMEOFFSET NOT NULL CONSTRAINT client_DF_createdat DEFAULT SYSDATETIMEOFFSET(),
    usercreate       INT            NULL CONSTRAINT client_usercreate_FK REFERENCES users(userid),
    isdeleted        BIT            NOT NULL CONSTRAINT client_DF_isdeleted DEFAULT 0
);
GO

/* ------------------------------ Turnos / atención ------------------------- */
CREATE TABLE attentiontype (
    attentiontypeid VARCHAR(3)   NOT NULL CONSTRAINT attentiontype_PK PRIMARY KEY, -- prefijo del turno (2 letras)
    description     VARCHAR(100) NOT NULL
);
GO

CREATE TABLE attentionstatus (
    statusid    INT         NOT NULL CONSTRAINT attentionstatus_PK PRIMARY KEY,
    description VARCHAR(30) NOT NULL
);
GO

CREATE TABLE turn (
    turnid                        INT IDENTITY(1,1) NOT NULL CONSTRAINT turn_PK PRIMARY KEY,
    description                   CHAR(6)        NOT NULL,  -- 2 letras + 4 números (AC0001)
    date                          DATETIMEOFFSET NOT NULL CONSTRAINT turn_DF_date DEFAULT SYSDATETIMEOFFSET(),
    turndate                      DATE           NOT NULL CONSTRAINT turn_DF_turndate DEFAULT CAST(SYSDATETIMEOFFSET() AS DATE),
    cash_cashid                   INT            NOT NULL CONSTRAINT turn_cash_FK REFERENCES cash(cashid),
    usergestorid                  INT            NOT NULL CONSTRAINT turn_gestor_FK REFERENCES users(userid),
    attentiontype_attentiontypeid VARCHAR(3)     NOT NULL CONSTRAINT turn_attentiontype_FK REFERENCES attentiontype(attentiontypeid),
    isdeleted                     BIT            NOT NULL CONSTRAINT turn_DF_isdeleted DEFAULT 0,
    CONSTRAINT turn_CK_description CHECK (description LIKE '[A-Z][A-Z][0-9][0-9][0-9][0-9]'),
    CONSTRAINT turn_UQ_description_day UNIQUE (description, turndate)
);
GO

CREATE TABLE attention (
    attentionid                   INT IDENTITY(1,1) NOT NULL CONSTRAINT attention_PK PRIMARY KEY,
    turn_turnid                   INT            NOT NULL CONSTRAINT attention_turn_FK REFERENCES turn(turnid),
    client_clientid               INT            NULL CONSTRAINT attention_client_FK REFERENCES client(clientid),
    attentiontype_attentiontypeid VARCHAR(3)     NOT NULL CONSTRAINT attention_attentiontype_FK REFERENCES attentiontype(attentiontypeid),
    attentionstatus_statusid      INT            NOT NULL CONSTRAINT attention_attentionstatus_FK REFERENCES attentionstatus(statusid),
    user_userid                   INT            NULL CONSTRAINT attention_user_FK REFERENCES users(userid), -- cajero que atendió
    attendedat                    DATETIMEOFFSET NULL,
    notes                         VARCHAR(250)   NULL,
    CONSTRAINT attention_UQ_turn UNIQUE (turn_turnid)
);
GO

/* ------------------------------ Servicios -------------------------------- */
CREATE TABLE service (
    serviceid          INT IDENTITY(1,1) NOT NULL CONSTRAINT service_PK PRIMARY KEY,
    servicename        VARCHAR(100)  NOT NULL,
    servicedescription VARCHAR(150)  NOT NULL,
    speedmbps          INT           NOT NULL,
    price              DECIMAL(10,2) NOT NULL,
    active             BIT           NOT NULL CONSTRAINT service_DF_active DEFAULT 1,
    isdeleted          BIT           NOT NULL CONSTRAINT service_DF_isdeleted DEFAULT 0
);
GO

CREATE TABLE device (
    deviceid          INT IDENTITY(1,1) NOT NULL CONSTRAINT device_PK PRIMARY KEY,
    devicename        VARCHAR(50) NOT NULL,
    service_serviceid INT         NOT NULL CONSTRAINT device_service_FK REFERENCES service(serviceid),
    isdeleted         BIT         NOT NULL CONSTRAINT device_DF_isdeleted DEFAULT 0
);
GO

/* ------------------------------ Contratos -------------------------------- */
CREATE TABLE methodpayment (
    methodpaymentid INT IDENTITY(1,1) NOT NULL CONSTRAINT methodpayment_PK PRIMARY KEY,
    description     VARCHAR(50) NOT NULL,
    active          BIT         NOT NULL CONSTRAINT methodpayment_DF_active DEFAULT 1,
    isdeleted       BIT         NOT NULL CONSTRAINT methodpayment_DF_isdeleted DEFAULT 0
);
GO

CREATE TABLE statuscontract (
    statusid    VARCHAR(3)  NOT NULL CONSTRAINT statuscontract_PK PRIMARY KEY,
    description VARCHAR(50) NOT NULL
);
GO

CREATE TABLE contract (
    contractid                    INT IDENTITY(1,1) NOT NULL CONSTRAINT contract_PK PRIMARY KEY,
    startdate                     DATETIMEOFFSET NOT NULL,
    enddate                       DATETIMEOFFSET NOT NULL,
    service_serviceid             INT            NOT NULL CONSTRAINT contract_service_FK REFERENCES service(serviceid),
    statuscontract_statusid       VARCHAR(3)     NOT NULL CONSTRAINT contract_statuscontract_FK REFERENCES statuscontract(statusid),
    client_clientid               INT            NOT NULL CONSTRAINT contract_client_FK REFERENCES client(clientid),
    methodpayment_methodpaymentid INT            NOT NULL CONSTRAINT contract_methodpayment_FK REFERENCES methodpayment(methodpaymentid),
    previouscontractid            INT            NULL CONSTRAINT contract_previous_FK REFERENCES contract(contractid),
    createdat                     DATETIMEOFFSET NOT NULL CONSTRAINT contract_DF_createdat DEFAULT SYSDATETIMEOFFSET(),
    usercreate                    INT            NULL CONSTRAINT contract_usercreate_FK REFERENCES users(userid),
    isdeleted                     BIT            NOT NULL CONSTRAINT contract_DF_isdeleted DEFAULT 0
);
GO

CREATE TABLE payments (
    paymentid                     INT IDENTITY(1,1) NOT NULL CONSTRAINT payments_PK PRIMARY KEY,
    paymentdate                   DATETIMEOFFSET NOT NULL CONSTRAINT payments_DF_paymentdate DEFAULT SYSDATETIMEOFFSET(),
    client_clientid               INT            NOT NULL CONSTRAINT payments_client_FK REFERENCES client(clientid),
    contract_contractid           INT            NOT NULL CONSTRAINT payments_contract_FK REFERENCES contract(contractid),
    methodpayment_methodpaymentid INT            NOT NULL CONSTRAINT payments_methodpayment_FK REFERENCES methodpayment(methodpaymentid),
    amount                        DECIMAL(10,2)  NOT NULL CONSTRAINT payments_CK_amount CHECK (amount > 0),
    user_userid                   INT            NULL CONSTRAINT payments_user_FK REFERENCES users(userid),
    isdeleted                     BIT            NOT NULL CONSTRAINT payments_DF_isdeleted DEFAULT 0
);
GO

CREATE INDEX turn_IX_cash_date      ON turn(cash_cashid, turndate);
CREATE INDEX attention_IX_status    ON attention(attentionstatus_statusid, attendedat);
CREATE INDEX contract_IX_client     ON contract(client_clientid, statuscontract_statusid);
CREATE INDEX payments_IX_client     ON payments(client_clientid);
GO

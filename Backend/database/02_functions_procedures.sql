/* =============================================================================
   02 - Funciones y procedimientos almacenados
   Zona horaria de negocio: Ecuador (UTC-05:00). Las fechas se guardan con offset
   y se convierten a fecha local para los conteos diarios.
   ============================================================================= */

/* -----------------------------------------------------------------------------
   fn_local_today: fecha actual en la zona horaria del negocio.
----------------------------------------------------------------------------- */
CREATE OR ALTER FUNCTION dbo.fn_local_today()
RETURNS DATE
AS
BEGIN
    RETURN CAST(SWITCHOFFSET(SYSDATETIMEOFFSET(), '-05:00') AS DATE);
END
GO

/* -----------------------------------------------------------------------------
   fn_count_attended_turns: total de turnos atendidos en una fecha según el rol.
   - ADMINISTRADOR: todos los turnos atendidos.
   - GESTOR: turnos atendidos que fueron asignados por ese gestor.
   - CAJERO: turnos atendidos por ese cajero.
----------------------------------------------------------------------------- */
CREATE OR ALTER FUNCTION dbo.fn_count_attended_turns
(
    @userid  INT,
    @rolname VARCHAR(50),
    @date    DATE
)
RETURNS INT
AS
BEGIN
    DECLARE @total INT;

    SELECT @total = COUNT(1)
    FROM attention a
    INNER JOIN turn t ON t.turnid = a.turn_turnid AND t.isdeleted = 0
    WHERE a.attentionstatus_statusid = 3 -- ATENDIDO
      AND CAST(SWITCHOFFSET(a.attendedat, '-05:00') AS DATE) = @date
      AND (
            @rolname = 'ADMINISTRADOR'
         OR (@rolname = 'GESTOR' AND t.usergestorid = @userid)
         OR (@rolname = 'CAJERO' AND a.user_userid = @userid)
      );

    RETURN ISNULL(@total, 0);
END
GO

/* -----------------------------------------------------------------------------
   sp_generate_turn: genera el turno con código correlativo diario por tipo de
   atención (AC0001, PS0001, ...) y crea la atención en estado PENDIENTE.
   El bloqueo UPDLOCK/HOLDLOCK evita códigos duplicados en concurrencia.
----------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_generate_turn
    @cashid          INT,
    @attentiontypeid VARCHAR(3),
    @gestorid        INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NOT EXISTS (SELECT 1 FROM cash WHERE cashid = @cashid AND active = 1 AND isdeleted = 0)
        THROW 50001, 'La caja no existe o se encuentra inactiva.', 1;

    IF NOT EXISTS (SELECT 1 FROM cashsession WHERE cash_cashid = @cashid AND closedat IS NULL)
        THROW 50004, 'La caja está cerrada. Solo se asignan turnos a cajas abiertas por un cajero.', 1;

    IF LEN(@attentiontypeid) <> 2 OR NOT EXISTS (SELECT 1 FROM attentiontype WHERE attentiontypeid = @attentiontypeid)
        THROW 50002, 'El tipo de atención no es válido.', 1;

    DECLARE @today DATE = dbo.fn_local_today();
    DECLARE @next INT;
    DECLARE @description CHAR(6);
    DECLARE @turnid INT;

    BEGIN TRANSACTION;

        SELECT @next = ISNULL(MAX(CAST(RIGHT(description, 4) AS INT)), 0) + 1
        FROM turn WITH (UPDLOCK, HOLDLOCK)
        WHERE turndate = @today
          AND LEFT(description, 2) = @attentiontypeid;

        IF @next > 9999
            THROW 50003, 'Se alcanzó el máximo de turnos diarios para este tipo de atención.', 1;

        SET @description = UPPER(@attentiontypeid) + RIGHT('0000' + CAST(@next AS VARCHAR(4)), 4);

        INSERT INTO turn (description, date, turndate, cash_cashid, usergestorid, attentiontype_attentiontypeid)
        VALUES (@description, SYSDATETIMEOFFSET(), @today, @cashid, @gestorid, @attentiontypeid);

        SET @turnid = SCOPE_IDENTITY();

        INSERT INTO attention (turn_turnid, attentiontype_attentiontypeid, attentionstatus_statusid)
        VALUES (@turnid, @attentiontypeid, 1); -- PENDIENTE

    COMMIT TRANSACTION;

    SELECT @turnid AS TurnId, @description AS Description;
END
GO

/* -----------------------------------------------------------------------------
   sp_change_contract_service: cambio de servicio (mayor o menor velocidad).
   - El contrato actual pasa a SUS (sustituido).
   - Se crea un nuevo contrato en estado REN (renovación de servicio) que
     conserva la fecha fin del contrato original.
----------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_change_contract_service
    @contractid   INT,
    @newserviceid INT,
    @userid       INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @currentservice INT, @status VARCHAR(3), @enddate DATETIMEOFFSET,
            @clientid INT, @methodpaymentid INT, @newcontractid INT;

    SELECT @currentservice  = service_serviceid,
           @status          = statuscontract_statusid,
           @enddate         = enddate,
           @clientid        = client_clientid,
           @methodpaymentid = methodpayment_methodpaymentid
    FROM contract
    WHERE contractid = @contractid AND isdeleted = 0;

    IF @status IS NULL
        THROW 50010, 'El contrato no existe.', 1;

    IF @status NOT IN ('VIG', 'REN')
        THROW 50011, 'Solo se puede cambiar el servicio de un contrato vigente.', 1;

    IF NOT EXISTS (SELECT 1 FROM service WHERE serviceid = @newserviceid AND active = 1 AND isdeleted = 0)
        THROW 50012, 'El nuevo servicio no existe o está inactivo.', 1;

    IF @currentservice = @newserviceid
        THROW 50013, 'El nuevo servicio debe ser diferente al servicio actual.', 1;

    BEGIN TRANSACTION;

        UPDATE contract
        SET statuscontract_statusid = 'SUS'
        WHERE contractid = @contractid;

        INSERT INTO contract (startdate, enddate, service_serviceid, statuscontract_statusid,
                              client_clientid, methodpayment_methodpaymentid, previouscontractid, usercreate)
        VALUES (SYSDATETIMEOFFSET(), @enddate, @newserviceid, 'REN',
                @clientid, @methodpaymentid, @contractid, @userid);

        SET @newcontractid = SCOPE_IDENTITY();

    COMMIT TRANSACTION;

    SELECT @newcontractid AS ContractId;
END
GO

/* -----------------------------------------------------------------------------
   sp_dashboard_indicators: indicadores del dashboard del administrador.
   Devuelve 5 conjuntos de resultados:
     1. Resumen de usuarios (estados, sesiones, roles)
     2. Indicadores por caja
     3. Indicadores por cajero
     4. Indicadores por gestor
     5. Serie diaria de turnos generados / atendidos
----------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_dashboard_indicators
    @from DATE,
    @to   DATE
AS
BEGIN
    SET NOCOUNT ON;

    /* 1. Usuarios */
    SELECT
        COUNT(1)                                                              AS TotalUsers,
        SUM(CASE WHEN u.userstatus_statusid = 'ACT' THEN 1 ELSE 0 END)        AS ActiveUsers,
        SUM(CASE WHEN u.userstatus_statusid = 'PEN' THEN 1 ELSE 0 END)        AS PendingUsers,
        SUM(CASE WHEN u.userstatus_statusid = 'BLO' THEN 1 ELSE 0 END)        AS BlockedUsers,
        SUM(CASE WHEN u.userstatus_statusid = 'INA' THEN 1 ELSE 0 END)        AS InactiveUsers,
        SUM(CASE WHEN u.sessionactive = 1 THEN 1 ELSE 0 END)                  AS ActiveSessions,
        SUM(CASE WHEN u.sessionactive = 0 THEN 1 ELSE 0 END)                  AS InactiveSessions,
        SUM(CASE WHEN r.rolname = 'ADMINISTRADOR' THEN 1 ELSE 0 END)          AS Administrators,
        SUM(CASE WHEN r.rolname = 'GESTOR' THEN 1 ELSE 0 END)                 AS Managers,
        SUM(CASE WHEN r.rolname = 'CAJERO' THEN 1 ELSE 0 END)                 AS Cashiers
    FROM users u
    INNER JOIN rol r ON r.rolid = u.rol_rolid
    WHERE u.isdeleted = 0;

    /* 2. Cajas */
    SELECT
        c.cashid                                                              AS CashId,
        c.cashdescription                                                     AS Description,
        c.active                                                              AS Active,
        (SELECT COUNT(1) FROM usercash uc WHERE uc.cash_cashid = c.cashid AND uc.active = 1) AS AssignedCashiers,
        (SELECT TOP 1 u.username FROM cashsession cs INNER JOIN users u ON u.userid = cs.user_userid
          WHERE cs.cash_cashid = c.cashid AND cs.closedat IS NULL)            AS CurrentUser,
        (SELECT COUNT(1) FROM turn t WHERE t.cash_cashid = c.cashid AND t.isdeleted = 0
          AND t.turndate BETWEEN @from AND @to)                               AS TurnsGenerated,
        (SELECT COUNT(1) FROM turn t INNER JOIN attention a ON a.turn_turnid = t.turnid
          WHERE t.cash_cashid = c.cashid AND t.isdeleted = 0 AND a.attentionstatus_statusid = 3
          AND CAST(SWITCHOFFSET(a.attendedat, '-05:00') AS DATE) BETWEEN @from AND @to) AS TurnsAttended
    FROM cash c
    WHERE c.isdeleted = 0
    ORDER BY c.cashid;

    /* 3. Cajeros */
    SELECT
        u.userid                                                              AS UserId,
        u.username                                                            AS Username,
        u.names + ' ' + u.lastnames                                           AS FullName,
        u.userstatus_statusid                                                 AS Status,
        u.sessionactive                                                       AS SessionActive,
        (SELECT COUNT(1) FROM usercash uc WHERE uc.user_userid = u.userid AND uc.active = 1) AS AssignedCashes,
        (SELECT COUNT(1) FROM attention a WHERE a.user_userid = u.userid AND a.attentionstatus_statusid = 3
          AND CAST(SWITCHOFFSET(a.attendedat, '-05:00') AS DATE) BETWEEN @from AND @to) AS TurnsAttended,
        (SELECT ISNULL(SUM(p.amount), 0) FROM payments p WHERE p.user_userid = u.userid AND p.isdeleted = 0
          AND CAST(SWITCHOFFSET(p.paymentdate, '-05:00') AS DATE) BETWEEN @from AND @to) AS PaymentsAmount
    FROM users u
    INNER JOIN rol r ON r.rolid = u.rol_rolid
    WHERE u.isdeleted = 0 AND r.rolname = 'CAJERO'
    ORDER BY TurnsAttended DESC, u.username;

    /* 4. Gestores */
    SELECT
        u.userid                                                              AS UserId,
        u.username                                                            AS Username,
        u.names + ' ' + u.lastnames                                           AS FullName,
        u.userstatus_statusid                                                 AS Status,
        (SELECT COUNT(1) FROM turn t WHERE t.usergestorid = u.userid AND t.isdeleted = 0
          AND t.turndate BETWEEN @from AND @to)                               AS TurnsAssigned,
        (SELECT COUNT(1) FROM turn t INNER JOIN attention a ON a.turn_turnid = t.turnid
          WHERE t.usergestorid = u.userid AND t.isdeleted = 0 AND a.attentionstatus_statusid = 3
          AND CAST(SWITCHOFFSET(a.attendedat, '-05:00') AS DATE) BETWEEN @from AND @to) AS TurnsAttended,
        (SELECT COUNT(1) FROM users x WHERE x.usercreate = u.userid AND x.isdeleted = 0) AS UsersCreated,
        (SELECT COUNT(1) FROM users x WHERE x.usercreate = u.userid AND x.isdeleted = 0
          AND x.userstatus_statusid = 'PEN')                                  AS UsersPending
    FROM users u
    INNER JOIN rol r ON r.rolid = u.rol_rolid
    WHERE u.isdeleted = 0 AND r.rolname = 'GESTOR'
    ORDER BY TurnsAssigned DESC, u.username;

    /* 5. Serie diaria */
    ;WITH days AS (
        SELECT @from AS d
        UNION ALL
        SELECT DATEADD(DAY, 1, d) FROM days WHERE d < @to
    )
    SELECT
        d                                                                     AS Day,
        (SELECT COUNT(1) FROM turn t WHERE t.turndate = d AND t.isdeleted = 0) AS TurnsGenerated,
        (SELECT COUNT(1) FROM attention a WHERE a.attentionstatus_statusid = 3
          AND CAST(SWITCHOFFSET(a.attendedat, '-05:00') AS DATE) = d)         AS TurnsAttended
    FROM days
    OPTION (MAXRECURSION 366);
END
GO

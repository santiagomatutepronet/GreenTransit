-- ============================================================
-- Seed idempotente: Profiles + usuario administrador inicial
-- GreenTransit v4.1 — ejecutar una sola vez en entorno nuevo
-- ============================================================

-- ── 1. Perfiles del sistema (9) ───────────────────────────
IF NOT EXISTS (SELECT 1 FROM Profiles WHERE Reference = 'ADMIN')
    INSERT INTO Profiles (Reference, Description, CreateDate)
    VALUES ('ADMIN', 'Administrador del sistema', GETUTCDATE());

IF NOT EXISTS (SELECT 1 FROM Profiles WHERE Reference = 'SCRAP')
    INSERT INTO Profiles (Reference, Description, CreateDate)
    VALUES ('SCRAP', 'Sistema Colectivo de Responsabilidad Ampliada', GETUTCDATE());

IF NOT EXISTS (SELECT 1 FROM Profiles WHERE Reference = 'PRODUCER')
    INSERT INTO Profiles (Reference, Description, CreateDate)
    VALUES ('PRODUCER', 'Productor / Generador de residuos', GETUTCDATE());

IF NOT EXISTS (SELECT 1 FROM Profiles WHERE Reference = 'CARRIER')
    INSERT INTO Profiles (Reference, Description, CreateDate)
    VALUES ('CARRIER', 'Transportista', GETUTCDATE());

IF NOT EXISTS (SELECT 1 FROM Profiles WHERE Reference = 'PLANT_OP')
    INSERT INTO Profiles (Reference, Description, CreateDate)
    VALUES ('PLANT_OP', 'Operador de Planta de Tratamiento', GETUTCDATE());

IF NOT EXISTS (SELECT 1 FROM Profiles WHERE Reference = 'CAC_OP')
    INSERT INTO Profiles (Reference, Description, CreateDate)
    VALUES ('CAC_OP', 'Operador de Centro de Acopio', GETUTCDATE());

IF NOT EXISTS (SELECT 1 FROM Profiles WHERE Reference = 'PUBLIC_ENT')
    INSERT INTO Profiles (Reference, Description, CreateDate)
    VALUES ('PUBLIC_ENT', 'Entidad Pública / Ayuntamiento', GETUTCDATE());

IF NOT EXISTS (SELECT 1 FROM Profiles WHERE Reference = 'COORDINATOR')
    INSERT INTO Profiles (Reference, Description, CreateDate)
    VALUES ('COORDINATOR', 'Coordinador del acuerdo', GETUTCDATE());

IF NOT EXISTS (SELECT 1 FROM Profiles WHERE Reference = 'DISPATCH_OFFICE')
    INSERT INTO Profiles (Reference, Description, CreateDate)
    VALUES ('DISPATCH_OFFICE', 'Oficina de Asignación — Gestor logístico', GETUTCDATE());

IF NOT EXISTS (SELECT 1 FROM Profiles WHERE Reference = 'REGULATOR')
    INSERT INTO Profiles (Reference, Description, CreateDate)
    VALUES ('REGULATOR', 'Regulador — Autoridad de supervisión normativa', GETUTCDATE());

IF NOT EXISTS (SELECT 1 FROM Profiles WHERE Reference = 'CERTIFIER')
    INSERT INTO Profiles (Reference, Description, CreateDate)
    VALUES ('CERTIFIER', 'Certificador / Auditor — Validación y coherencia', GETUTCDATE());

-- ── 2. Usuario administrador inicial ─────────────────────
-- Solo se inserta si no existe ningún usuario con perfil ADMIN.
-- Cambiar Login/Email tras el primer acceso al sistema.
IF NOT EXISTS (
    SELECT 1
    FROM   Users u
    JOIN   Profiles p ON u.IdProfile = p.ID
    WHERE  p.Reference = 'ADMIN'
)
BEGIN
    DECLARE @AdminProfileId INT = (SELECT ID FROM Profiles WHERE Reference = 'ADMIN');

    INSERT INTO Users (Login, CompleteName, Email, IdProfile, OwnerId, CreateDate)
    VALUES (
        'admin@greentransit.dev',
        'Administrador del sistema',
        'admin@greentransit.dev',
        @AdminProfileId,
        NULL,           -- OwnerId null: no ligado a tenant hasta configuración manual
        GETUTCDATE()
    );

    PRINT 'Usuario administrador inicial creado: admin@greentransit.dev';
END
ELSE
BEGIN
    PRINT 'Usuario ADMIN ya existe. Seed omitido.';
END

-- ── 3. Verificación ───────────────────────────────────────
SELECT ID, Reference, Description FROM Profiles ORDER BY ID;
SELECT u.ID, u.Login, u.Email, p.Reference AS Perfil, u.OwnerId
FROM   Users u
JOIN   Profiles p ON u.IdProfile = p.ID
WHERE  p.Reference = 'ADMIN';

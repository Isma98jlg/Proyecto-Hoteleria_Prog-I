USE [HOTEL];
GO

-- Ajusta los correos si fueron personalizados en .env.
DECLARE @DemoAccounts TABLE (cargo_nombre nvarchar(256), email nvarchar(256));
INSERT INTO @DemoAccounts (cargo_nombre, email) VALUES
    (N'admin', N'admin@hoteleria.com'),
    (N'trabajador', N'trabajador@hoteleria.com'),
    (N'usuario', N'usuario@hoteleria.com');

IF OBJECT_ID(N'dbo.Usuarios', N'U') IS NULL
    THROW 51000, 'Falta dbo.Usuarios. Inicia primero el contenedor SQL Server.', 1;
IF OBJECT_ID(N'dbo.Empleados', N'U') IS NULL
    THROW 51001, 'Falta dbo.Empleados. Ejecuta las migraciones de la app.', 1;
IF OBJECT_ID(N'dbo.AspNetUsers', N'U') IS NULL
    THROW 51002, 'Falta dbo.AspNetUsers. Ejecuta las migraciones de la app.', 1;
IF OBJECT_ID(N'dbo.Roles', N'U') IS NULL
    THROW 51003, 'Falta dbo.Roles. Ejecuta las migraciones de la app.', 1;

IF EXISTS (
    SELECT 1
    FROM @DemoAccounts expected
    LEFT JOIN dbo.AspNetUsers auth ON auth.Email = expected.email
    LEFT JOIN dbo.Usuarios u ON u.usuario_id = auth.Id
    LEFT JOIN dbo.Empleados e ON e.usuario_id = auth.Id
    LEFT JOIN dbo.Roles r ON r.cargo_id = u.cargo_id
    WHERE auth.Id IS NULL OR auth.PasswordHash IS NULL OR u.usuario_id IS NULL
       OR e.usuario_id IS NULL OR r.cargo_nombre <> expected.cargo_nombre
       OR NOT EXISTS (
            SELECT 1 FROM dbo.AspNetUserRoles ur
            WHERE ur.UserId = auth.Id AND ur.RoleId = r.cargo_id
       )
)
    THROW 51004, 'Falta una cuenta demo, hash de contraseña, empleado o asignación de cargo. Arranca la app con DB_AUTO_MIGRATE=true.', 1;

SELECT
    N'OK' AS resultado,
    expected.cargo_nombre,
    expected.email,
    auth.Id AS usuario_id_compartido,
    r.ver,
    r.crud_requerimientos,
    r.crud_pisos,
    r.crud_usuarios,
    r.crud_habitaciones,
    r.crud_tipo_vip,
    CAST(1 AS bit) AS empleado_creado,
    CAST(1 AS bit) AS password_hash_presente
FROM @DemoAccounts expected
INNER JOIN dbo.AspNetUsers auth ON auth.Email = expected.email
INNER JOIN dbo.Usuarios u ON u.usuario_id = auth.Id
INNER JOIN dbo.Roles r ON r.cargo_id = u.cargo_id
ORDER BY expected.cargo_nombre;
GO

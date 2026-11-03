IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031162711_InitialCreate'
)
BEGIN
    CREATE TABLE [Pisos] (
        [nro_piso] bigint NOT NULL,
        [ref_piso] nvarchar(20) NULL,
        CONSTRAINT [PK_Pisos] PRIMARY KEY ([nro_piso])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031162711_InitialCreate'
)
BEGIN
    CREATE TABLE [Tipo Vip] (
        [tipo_vip] nvarchar(15) NOT NULL,
        [sigla] char(1) NOT NULL,
        [precio_cama] decimal(12,2) NOT NULL,
        CONSTRAINT [PK_Tipo Vip] PRIMARY KEY ([tipo_vip])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031162711_InitialCreate'
)
BEGIN
    CREATE TABLE [Usuarios] (
        [usuario_id] bigint NOT NULL,
        [tipo_documento] nvarchar(15) NOT NULL,
        [numero_documento] nvarchar(25) NOT NULL,
        [nombres] nvarchar(25) NOT NULL,
        [apellidos] nvarchar(25) NOT NULL,
        [nacionalidad] nvarchar(25) NOT NULL,
        [telefono] nvarchar(25) NOT NULL,
        [e_mail] nvarchar(25) NOT NULL,
        CONSTRAINT [PK_Usuarios] PRIMARY KEY ([usuario_id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031162711_InitialCreate'
)
BEGIN
    CREATE TABLE [Habitaciones] (
        [habitacion_id] bigint NOT NULL,
        [nro_piso] bigint NOT NULL,
        [nro_camas] int NOT NULL,
        [tipo_vip] nvarchar(15) NOT NULL,
        [estado] nvarchar(20) NOT NULL,
        CONSTRAINT [PK_Habitaciones] PRIMARY KEY ([habitacion_id]),
        CONSTRAINT [ck_estado] CHECK (estado IN ('MANTENIMIENTO', 'LIBRE', 'OCUPADO')),
        CONSTRAINT [ck_nro_camas] CHECK (nro_camas >= 1),
        CONSTRAINT [fk_habitacion_piso] FOREIGN KEY ([nro_piso]) REFERENCES [Pisos] ([nro_piso]) ON DELETE CASCADE,
        CONSTRAINT [fk_habitacion_tipovip] FOREIGN KEY ([tipo_vip]) REFERENCES [Tipo Vip] ([tipo_vip]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031162711_InitialCreate'
)
BEGIN
    CREATE TABLE [Reservas] (
        [reserva_id] bigint NOT NULL,
        [usuario_id] bigint NOT NULL,
        [estado_reserva] nvarchar(15) NOT NULL,
        [fecha_reserva_creacion] datetime2(7) NOT NULL,
        [inicio_reserva] datetime2(7) NOT NULL,
        [total_noches] int NOT NULL,
        [fin_reserva] AS DATEADD(day, total_noches, inicio_reserva) PERSISTED,
        CONSTRAINT [PK_Reservas] PRIMARY KEY ([reserva_id]),
        CONSTRAINT [check_estado_reserva] CHECK (estado_reserva IN ('CANCELADA', 'FINALIZADA', 'CONFIRMADA', 'PENDIENTE')),
        CONSTRAINT [check_fin_reserva] CHECK (fin_reserva > inicio_reserva),
        CONSTRAINT [check_total_noches] CHECK (total_noches >= 1),
        CONSTRAINT [fk_reserva_usuario] FOREIGN KEY ([usuario_id]) REFERENCES [Usuarios] ([usuario_id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031162711_InitialCreate'
)
BEGIN
    CREATE TABLE [Reserva Habitaciones] (
        [reserva_id] bigint NOT NULL,
        [habitacion_id] bigint NOT NULL,
        [precio_noche_pactado] decimal(12,2) NOT NULL,
        CONSTRAINT [PK_Reserva Habitaciones] PRIMARY KEY ([reserva_id], [habitacion_id]),
        CONSTRAINT [fk_habitacion_rh] FOREIGN KEY ([habitacion_id]) REFERENCES [Habitaciones] ([habitacion_id]) ON DELETE CASCADE,
        CONSTRAINT [fk_reserva_rh] FOREIGN KEY ([reserva_id]) REFERENCES [Reservas] ([reserva_id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031162711_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Habitaciones_Estado] ON [Habitaciones] ([estado]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031162711_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Habitaciones_Piso] ON [Habitaciones] ([nro_piso]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031162711_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Habitaciones_TipoVip] ON [Habitaciones] ([tipo_vip]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031162711_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Reserva Habitaciones_habitacion_id] ON [Reserva Habitaciones] ([habitacion_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031162711_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Reservas_Estado] ON [Reservas] ([estado_reserva]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031162711_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Reservas_Fechas] ON [Reservas] ([inicio_reserva], [fin_reserva]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031162711_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Reservas_Usuario] ON [Reservas] ([usuario_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031162711_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Tipo Vip_sigla] ON [Tipo Vip] ([sigla]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031162711_InitialCreate'
)
BEGIN
    EXEC(N'CREATE VIEW [dbo].[TotalPrice_Habitacion_plus_Codigo] AS
    SELECT h.[habitacion_id], (CONVERT(VARCHAR(4), h.[nro_piso]) + tv.[sigla]) AS [codigo], h.[nro_piso], h.[nro_camas], h.[tipo_vip], h.[estado], h.[nro_camas] * tv.[precio_cama] AS [precio_total]
    FROM [dbo].[Habitaciones] h
    JOIN [dbo].[Tipo Vip] tv ON h.[tipo_vip] = tv.[tipo_vip]');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031162711_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261031162711_InitialCreate', N'10.0.14');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101141821_IdentityAndHotelRoles'
)
BEGIN
    ALTER TABLE [Usuarios] ADD [cargo_id] bigint NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101141821_IdentityAndHotelRoles'
)
BEGIN
    CREATE TABLE [AspNetUsers] (
        [Id] bigint NOT NULL,
        [UserName] nvarchar(256) NULL,
        [NormalizedUserName] nvarchar(256) NULL,
        [Email] nvarchar(256) NULL,
        [NormalizedEmail] nvarchar(256) NULL,
        [EmailConfirmed] bit NOT NULL,
        [PasswordHash] nvarchar(max) NULL,
        [SecurityStamp] nvarchar(max) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        [PhoneNumber] nvarchar(max) NULL,
        [PhoneNumberConfirmed] bit NOT NULL,
        [TwoFactorEnabled] bit NOT NULL,
        [LockoutEnd] datetimeoffset NULL,
        [LockoutEnabled] bit NOT NULL,
        [AccessFailedCount] int NOT NULL,
        CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetUsers_Usuarios_Id] FOREIGN KEY ([Id]) REFERENCES [Usuarios] ([usuario_id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101141821_IdentityAndHotelRoles'
)
BEGIN
    CREATE TABLE [Empleados] (
        [usuario_id] bigint NOT NULL,
        [fecha_contratacion] date NOT NULL,
        [sueldo] decimal(12,2) NOT NULL,
        CONSTRAINT [PK_Empleados] PRIMARY KEY ([usuario_id]),
        CONSTRAINT [FK_Empleados_Usuarios_usuario_id] FOREIGN KEY ([usuario_id]) REFERENCES [Usuarios] ([usuario_id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101141821_IdentityAndHotelRoles'
)
BEGIN
    CREATE TABLE [Roles] (
        [cargo_id] bigint NOT NULL IDENTITY,
        [ver] bit NOT NULL,
        [crud_requerimientos] bit NOT NULL,
        [crud_pisos] bit NOT NULL,
        [crud_usuarios] bit NOT NULL,
        [crud_habitaciones] bit NOT NULL,
        [crud_tipo_vip] bit NOT NULL,
        [cargo_nombre] nvarchar(256) NULL,
        [nombre_normalizado] nvarchar(256) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        CONSTRAINT [PK_Roles] PRIMARY KEY ([cargo_id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101141821_IdentityAndHotelRoles'
)
BEGIN
    CREATE TABLE [AspNetUserClaims] (
        [Id] int NOT NULL IDENTITY,
        [UserId] bigint NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101141821_IdentityAndHotelRoles'
)
BEGIN
    CREATE TABLE [AspNetUserLogins] (
        [LoginProvider] nvarchar(450) NOT NULL,
        [ProviderKey] nvarchar(450) NOT NULL,
        [ProviderDisplayName] nvarchar(max) NULL,
        [UserId] bigint NOT NULL,
        CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101141821_IdentityAndHotelRoles'
)
BEGIN
    CREATE TABLE [AspNetUserTokens] (
        [UserId] bigint NOT NULL,
        [LoginProvider] nvarchar(450) NOT NULL,
        [Name] nvarchar(450) NOT NULL,
        [Value] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
        CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101141821_IdentityAndHotelRoles'
)
BEGIN
    CREATE TABLE [AspNetRoleClaims] (
        [Id] int NOT NULL IDENTITY,
        [RoleId] bigint NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetRoleClaims_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([cargo_id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101141821_IdentityAndHotelRoles'
)
BEGIN
    CREATE TABLE [AspNetUserRoles] (
        [UserId] bigint NOT NULL,
        [RoleId] bigint NOT NULL,
        CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AspNetUserRoles_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([cargo_id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101141821_IdentityAndHotelRoles'
)
BEGIN
    CREATE INDEX [IX_Usuarios_cargo_id] ON [Usuarios] ([cargo_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101141821_IdentityAndHotelRoles'
)
BEGIN
    CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101141821_IdentityAndHotelRoles'
)
BEGIN
    CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101141821_IdentityAndHotelRoles'
)
BEGIN
    CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101141821_IdentityAndHotelRoles'
)
BEGIN
    CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101141821_IdentityAndHotelRoles'
)
BEGIN
    CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101141821_IdentityAndHotelRoles'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101141821_IdentityAndHotelRoles'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [RoleNameIndex] ON [Roles] ([nombre_normalizado]) WHERE [nombre_normalizado] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101141821_IdentityAndHotelRoles'
)
BEGIN
    ALTER TABLE [Usuarios] ADD CONSTRAINT [FK_Usuarios_Roles_cargo_id] FOREIGN KEY ([cargo_id]) REFERENCES [Roles] ([cargo_id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101141821_IdentityAndHotelRoles'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261101141821_IdentityAndHotelRoles', N'10.0.14');
END;

COMMIT;
GO


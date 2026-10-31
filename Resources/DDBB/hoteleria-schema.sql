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
CREATE TABLE [Pisos] (
    [nro_piso] bigint NOT NULL,
    [ref_piso] nvarchar(20) NULL,
    CONSTRAINT [PK_Pisos] PRIMARY KEY ([nro_piso])
);

CREATE TABLE [Tipo Vip] (
    [tipo_vip] nvarchar(15) NOT NULL,
    [sigla] char(1) NOT NULL,
    [precio_cama] decimal(12,2) NOT NULL,
    CONSTRAINT [PK_Tipo Vip] PRIMARY KEY ([tipo_vip])
);

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

CREATE TABLE [Reserva Habitaciones] (
    [reserva_id] bigint NOT NULL,
    [habitacion_id] bigint NOT NULL,
    [precio_noche_pactado] decimal(12,2) NOT NULL,
    CONSTRAINT [PK_Reserva Habitaciones] PRIMARY KEY ([reserva_id], [habitacion_id]),
    CONSTRAINT [fk_habitacion_rh] FOREIGN KEY ([habitacion_id]) REFERENCES [Habitaciones] ([habitacion_id]) ON DELETE CASCADE,
    CONSTRAINT [fk_reserva_rh] FOREIGN KEY ([reserva_id]) REFERENCES [Reservas] ([reserva_id]) ON DELETE CASCADE
);

CREATE INDEX [IX_Habitaciones_Estado] ON [Habitaciones] ([estado]);

CREATE INDEX [IX_Habitaciones_Piso] ON [Habitaciones] ([nro_piso]);

CREATE INDEX [IX_Habitaciones_TipoVip] ON [Habitaciones] ([tipo_vip]);

CREATE INDEX [IX_Reserva Habitaciones_habitacion_id] ON [Reserva Habitaciones] ([habitacion_id]);

CREATE INDEX [IX_Reservas_Estado] ON [Reservas] ([estado_reserva]);

CREATE INDEX [IX_Reservas_Fechas] ON [Reservas] ([inicio_reserva], [fin_reserva]);

CREATE INDEX [IX_Reservas_Usuario] ON [Reservas] ([usuario_id]);

CREATE UNIQUE INDEX [IX_Tipo Vip_sigla] ON [Tipo Vip] ([sigla]);

CREATE VIEW [dbo].[TotalPrice_Habitacion_plus_Codigo] AS
SELECT h.[habitacion_id], (CONVERT(VARCHAR(4), h.[nro_piso]) + tv.[sigla]) AS [codigo], h.[nro_piso], h.[nro_camas], h.[tipo_vip], h.[estado], h.[nro_camas] * tv.[precio_cama] AS [precio_total]
FROM [dbo].[Habitaciones] h
JOIN [dbo].[Tipo Vip] tv ON h.[tipo_vip] = tv.[tipo_vip];

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261031162711_InitialCreate', N'10.0.12');

COMMIT;
GO


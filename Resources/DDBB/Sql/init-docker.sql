-- Script de inicialización para SQL Server en Linux (Docker)
-- Se ejecuta automáticamente al crear el contenedor

-- Configuración requerida para vistas indexadas y columnas computadas
SET QUOTED_IDENTIFIER ON
SET ANSI_NULLS ON
GO

-- Crear la base de datos
CREATE DATABASE [HOTEL]
GO

USE [HOTEL]
GO

-- Tabla Pisos
CREATE TABLE [dbo].[Pisos] (
    [nro_piso] BIGINT NOT NULL PRIMARY KEY,
    [ref_piso] VARCHAR(20) NULL  -- Aumentado a 20 para 'Planta Baja'
)
GO

-- Tabla Tipo Vip (con espacio como en el script original)
CREATE TABLE [dbo].[Tipo Vip] (
    [tipo_vip] VARCHAR(15) NOT NULL PRIMARY KEY,
    [sigla] CHAR(1) NOT NULL UNIQUE,
    [precio_cama] DECIMAL(12, 2) NOT NULL
)
GO

-- Tabla Habitaciones
CREATE TABLE [dbo].[Habitaciones] (
    [habitacion_id] BIGINT NOT NULL PRIMARY KEY,
    [nro_piso] BIGINT NOT NULL,
    [nro_camas] INT NOT NULL CHECK ([nro_camas] >= 1),
    [tipo_vip] VARCHAR(15) NOT NULL,
    [estado] VARCHAR(20) NOT NULL CHECK ([estado] IN ('MANTENIMIENTO', 'LIBRE', 'OCUPADO')),
    FOREIGN KEY ([nro_piso]) REFERENCES [dbo].[Pisos] ([nro_piso]),
    FOREIGN KEY ([tipo_vip]) REFERENCES [dbo].[Tipo Vip] ([tipo_vip])
)
GO

-- Tabla Usuarios
CREATE TABLE [dbo].[Usuarios] (
    [usuario_id] BIGINT NOT NULL PRIMARY KEY,
    [tipo_documento] VARCHAR(15) NOT NULL,
    [numero_documento] VARCHAR(25) NOT NULL,
    [nombres] VARCHAR(25) NOT NULL,
    [apellidos] VARCHAR(25) NOT NULL,
    [nacionalidad] VARCHAR(25) NOT NULL,
    [telefono] VARCHAR(25) NOT NULL,
    [e_mail] VARCHAR(25) NOT NULL
)
GO

-- Tabla Reservas
CREATE TABLE [dbo].[Reservas] (
    [reserva_id] BIGINT NOT NULL PRIMARY KEY,
    [usuario_id] BIGINT NOT NULL,
    [estado_reserva] VARCHAR(15) NOT NULL CHECK ([estado_reserva] IN ('CANCELADA', 'FINALIZADA', 'CONFIRMADA', 'PENDIENTE')),
    [fecha_reserva_creacion] DATETIME2(7) NOT NULL DEFAULT SYSDATETIME(),
    [inicio_reserva] DATETIME2(7) NOT NULL,
    [total_noches] INT NOT NULL CHECK ([total_noches] >= 1),
    [fin_reserva] AS (DATEADD(DAY, [total_noches], [inicio_reserva])) PERSISTED,
    FOREIGN KEY ([usuario_id]) REFERENCES [dbo].[Usuarios] ([usuario_id]),
    CHECK ([fin_reserva] > [inicio_reserva])
)
GO

-- Tabla Reserva Habitaciones (N:M) - DESPUÉS de Reservas y Habitaciones
CREATE TABLE [dbo].[Reserva Habitaciones] (
    [reserva_id] BIGINT NOT NULL,
    [habitacion_id] BIGINT NOT NULL,
    [precio_noche_pactado] DECIMAL(12, 2) NOT NULL,
    PRIMARY KEY ([reserva_id], [habitacion_id]),
    FOREIGN KEY ([reserva_id]) REFERENCES [dbo].[Reservas] ([reserva_id]),
    FOREIGN KEY ([habitacion_id]) REFERENCES [dbo].[Habitaciones] ([habitacion_id])
)
GO

-- Vista: TotalPrice_Habitacion_plus_Codigo
-- Requiere QUOTED_IDENTIFIER ON
CREATE VIEW [dbo].[TotalPrice_Habitacion_plus_Codigo] AS
SELECT 
    h.[habitacion_id],
    (CONVERT(VARCHAR(4), h.[nro_piso]) + tv.[sigla]) AS [codigo],
    h.[nro_piso],
    h.[nro_camas],
    h.[tipo_vip],
    h.[estado],
    h.[nro_camas] * tv.[precio_cama] AS [precio_total]
FROM [dbo].[Habitaciones] h 
JOIN [dbo].[Tipo Vip] tv ON h.[tipo_vip] = tv.[tipo_vip]
GO

-- Índices adicionales para rendimiento
CREATE INDEX IX_Habitaciones_Estado ON [dbo].[Habitaciones] ([estado])
GO
CREATE INDEX IX_Habitaciones_Piso ON [dbo].[Habitaciones] ([nro_piso])
GO
CREATE INDEX IX_Habitaciones_TipoVip ON [dbo].[Habitaciones] ([tipo_vip])
GO
CREATE INDEX IX_Reservas_Usuario ON [dbo].[Reservas] ([usuario_id])
GO
CREATE INDEX IX_Reservas_Estado ON [dbo].[Reservas] ([estado_reserva])
GO
CREATE INDEX IX_Reservas_Fechas ON [dbo].[Reservas] ([inicio_reserva], [fin_reserva])
GO

-- Datos de ejemplo para pruebas
-- Pisos
INSERT INTO [dbo].[Pisos] ([nro_piso], [ref_piso]) VALUES 
(1, 'Planta Baja'),
(2, 'Primer Piso'),
(3, 'Segundo Piso'),
(4, 'Tercer Piso'),
(5, 'Piso VIP')
GO

-- Tipos VIP
INSERT INTO [dbo].[Tipo Vip] ([tipo_vip], [sigla], [precio_cama]) VALUES 
('ESTANDAR', 'E', 50.00),
('SUPERIOR', 'S', 80.00),
('SUITE', 'U', 150.00),
('PRESIDENCIAL', 'P', 300.00)
GO

-- Habitaciones de ejemplo
INSERT INTO [dbo].[Habitaciones] ([habitacion_id], [nro_piso], [nro_camas], [tipo_vip], [estado]) VALUES 
(101, 1, 2, 'ESTANDAR', 'LIBRE'),
(102, 1, 1, 'ESTANDAR', 'LIBRE'),
(103, 1, 2, 'SUPERIOR', 'LIBRE'),
(201, 2, 2, 'ESTANDAR', 'LIBRE'),
(202, 2, 2, 'SUPERIOR', 'LIBRE'),
(203, 2, 1, 'SUITE', 'LIBRE'),
(301, 3, 2, 'ESTANDAR', 'LIBRE'),
(302, 3, 2, 'SUPERIOR', 'LIBRE'),
(303, 3, 2, 'SUITE', 'LIBRE'),
(401, 4, 2, 'SUPERIOR', 'LIBRE'),
(402, 4, 2, 'SUITE', 'LIBRE'),
(501, 5, 2, 'PRESIDENCIAL', 'LIBRE'),
(502, 5, 3, 'PRESIDENCIAL', 'LIBRE')
GO

-- Usuario de ejemplo (admin)
INSERT INTO [dbo].[Usuarios] ([usuario_id], [tipo_documento], [numero_documento], [nombres], [apellidos], [nacionalidad], [telefono], [e_mail]) VALUES 
(1, 'DNI', '12345678', 'Admin', 'Hoteleria', 'Local', '555-0000', 'admin@hoteleria.com')
GO

PRINT 'Base de datos HOTEL inicializada correctamente.'
GO
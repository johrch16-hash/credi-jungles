-- =============================================
-- LendSoft Pro - Script de Base de Datos (SQL Server)
-- Ejecutar en una base de datos vacía.
-- =============================================

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

-- Roles y Usuarios (Identity)
CREATE TABLE [Roles] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(256) NULL,
    [NormalizedName] nvarchar(256) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    CONSTRAINT [PK_Roles] PRIMARY KEY ([Id])
);

CREATE TABLE [Usuarios] (
    [Id] int NOT NULL IDENTITY,
    [NombreCompleto] nvarchar(100) NOT NULL,
    [Activo] bit NOT NULL DEFAULT CAST(1 AS bit),
    [FechaCreacion] datetime2 NOT NULL,
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
    CONSTRAINT [PK_Usuarios] PRIMARY KEY ([Id])
);

-- Clientes
CREATE TABLE [Clientes] (
    [ClienteId] int NOT NULL IDENTITY,
    [NombreCompleto] nvarchar(150) NOT NULL,
    [Telefono] nvarchar(20) NOT NULL,
    [Email] nvarchar(150) NULL,
    [CedulaCifrada] nvarchar(500) NULL,
    [CedulaHash] nvarchar(100) NULL,
    [Direccion] nvarchar(300) NULL,
    [LugarTrabajo] nvarchar(200) NULL,
    [NivelAtrasos] int NOT NULL DEFAULT 0,
    [Estado] nvarchar(20) NOT NULL DEFAULT N'Normal',
    [Activo] bit NOT NULL DEFAULT CAST(1 AS bit),
    [FechaCreacion] datetime2 NOT NULL,
    CONSTRAINT [PK_Clientes] PRIMARY KEY ([ClienteId])
);

-- Préstamos
CREATE TABLE [Prestamos] (
    [PrestamoId] int NOT NULL IDENTITY,
    [ClienteId] int NOT NULL,
    [NombreAlias] nvarchar(100) NULL,
    [MontoPrestado] decimal(18,2) NOT NULL,
    [PorcentajeInteres] decimal(5,2) NOT NULL DEFAULT 20.0,
    [MontoTotal] decimal(18,2) NOT NULL,
    [SaldoPendiente] decimal(18,2) NOT NULL,
    [FechaInicio] datetime2 NOT NULL,
    [PeriodicidadDias] int NOT NULL DEFAULT 7,
    [NumeroSinpe] nvarchar(30) NULL,
    [CuentasBancarias] nvarchar(500) NULL,
    [Estado] nvarchar(20) NOT NULL DEFAULT N'Activo',
    [FechaCreacion] datetime2 NOT NULL,
    CONSTRAINT [PK_Prestamos] PRIMARY KEY ([PrestamoId]),
    CONSTRAINT [FK_Prestamos_Clientes_ClienteId] FOREIGN KEY ([ClienteId]) REFERENCES [Clientes] ([ClienteId]) ON DELETE NO ACTION
);

-- Cuotas
CREATE TABLE [Cuotas] (
    [CuotaId] int NOT NULL IDENTITY,
    [PrestamoId] int NOT NULL,
    [NumeroCuota] int NOT NULL,
    [Monto] decimal(18,2) NOT NULL,
    [FechaVencimiento] datetime2 NOT NULL,
    [Estado] nvarchar(15) NOT NULL DEFAULT N'Pendiente',
    [FechaPago] datetime2 NULL,
    CONSTRAINT [PK_Cuotas] PRIMARY KEY ([CuotaId]),
    CONSTRAINT [FK_Cuotas_Prestamos_PrestamoId] FOREIGN KEY ([PrestamoId]) REFERENCES [Prestamos] ([PrestamoId]) ON DELETE CASCADE
);

-- Pagos
CREATE TABLE [Pagos] (
    [PagoId] int NOT NULL IDENTITY,
    [CuotaId] int NOT NULL,
    [UsuarioId] int NOT NULL,
    [MontoPagado] decimal(18,2) NOT NULL,
    [FechaPago] datetime2 NOT NULL,
    [ComprobanteEnviado] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_Pagos] PRIMARY KEY ([PagoId]),
    CONSTRAINT [FK_Pagos_Cuotas_CuotaId] FOREIGN KEY ([CuotaId]) REFERENCES [Cuotas] ([CuotaId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Pagos_Usuarios_UsuarioId] FOREIGN KEY ([UsuarioId]) REFERENCES [Usuarios] ([Id]) ON DELETE NO ACTION
);

-- Notificaciones
CREATE TABLE [Notificaciones] (
    [NotificacionId] int NOT NULL IDENTITY,
    [PrestamoId] int NOT NULL,
    [CuotaId] int NOT NULL,
    [ClienteId] int NOT NULL,
    [Tipo] nvarchar(30) NOT NULL,
    [Mensaje] nvarchar(1000) NOT NULL,
    [Canal] nvarchar(20) NOT NULL DEFAULT N'WhatsApp',
    [Estado] nvarchar(15) NOT NULL DEFAULT N'Pendiente',
    [FechaCreacion] datetime2 NOT NULL,
    [FechaEnvio] datetime2 NULL,
    CONSTRAINT [PK_Notificaciones] PRIMARY KEY ([NotificacionId]),
    CONSTRAINT [FK_Notificaciones_Clientes_ClienteId] FOREIGN KEY ([ClienteId]) REFERENCES [Clientes] ([ClienteId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Notificaciones_Cuotas_CuotaId] FOREIGN KEY ([CuotaId]) REFERENCES [Cuotas] ([CuotaId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Notificaciones_Prestamos_PrestamoId] FOREIGN KEY ([PrestamoId]) REFERENCES [Prestamos] ([PrestamoId]) ON DELETE NO ACTION
);

-- Gastos
CREATE TABLE [Gastos] (
    [GastoId] int NOT NULL IDENTITY,
    [Descripcion] nvarchar(200) NOT NULL,
    [Monto] decimal(18,2) NOT NULL,
    [Categoria] nvarchar(50) NOT NULL DEFAULT N'General',
    [Fecha] datetime2 NOT NULL,
    [UsuarioId] int NOT NULL,
    CONSTRAINT [PK_Gastos] PRIMARY KEY ([GastoId]),
    CONSTRAINT [FK_Gastos_Usuarios_UsuarioId] FOREIGN KEY ([UsuarioId]) REFERENCES [Usuarios] ([Id]) ON DELETE NO ACTION
);

-- Bitácora
CREATE TABLE [Bitacoras] (
    [BitacoraId] int NOT NULL IDENTITY,
    [UsuarioId] int NOT NULL,
    [Accion] nvarchar(100) NOT NULL,
    [Detalle] nvarchar(max) NULL,
    [Fecha] datetime2 NOT NULL,
    [DireccionIP] nvarchar(max) NULL,
    CONSTRAINT [PK_Bitacoras] PRIMARY KEY ([BitacoraId]),
    CONSTRAINT [FK_Bitacoras_Usuarios_UsuarioId] FOREIGN KEY ([UsuarioId]) REFERENCES [Usuarios] ([Id]) ON DELETE NO ACTION
);

-- Preferencias (key-value store para plantillas y config)
CREATE TABLE [Preferencias] (
    [Id] int NOT NULL IDENTITY,
    [Clave] nvarchar(100) NOT NULL,
    [Valor] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_Preferencias] PRIMARY KEY ([Id])
);

-- Configuración de WhatsApp
CREATE TABLE [ConfiguracionesWhatsApp] (
    [Id] int NOT NULL IDENTITY,
    [PhoneNumberId] nvarchar(max) NOT NULL,
    [AccessToken] nvarchar(max) NOT NULL,
    [FechaActualizacion] datetime2 NOT NULL
);

-- Historial de contraseñas
CREATE TABLE [PasswordHistories] (
    [Id] int NOT NULL IDENTITY,
    [UsuarioId] int NOT NULL,
    [PasswordHash] nvarchar(500) NOT NULL,
    [FechaCambio] datetime2 NOT NULL,
    CONSTRAINT [PK_PasswordHistories] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PasswordHistories_Usuarios_UsuarioId] FOREIGN KEY ([UsuarioId]) REFERENCES [Usuarios] ([Id]) ON DELETE CASCADE
);

-- Identity tables
CREATE TABLE [AspNetRoleClaims] (
    [Id] int NOT NULL IDENTITY,
    [RoleId] int NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetRoleClaims_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [AspNetUserClaims] (
    [Id] int NOT NULL IDENTITY,
    [UserId] int NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetUserClaims_Usuarios_UserId] FOREIGN KEY ([UserId]) REFERENCES [Usuarios] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [AspNetUserLogins] (
    [LoginProvider] nvarchar(450) NOT NULL,
    [ProviderKey] nvarchar(450) NOT NULL,
    [ProviderDisplayName] nvarchar(max) NULL,
    [UserId] int NOT NULL,
    CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
    CONSTRAINT [FK_AspNetUserLogins_Usuarios_UserId] FOREIGN KEY ([UserId]) REFERENCES [Usuarios] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [AspNetUserRoles] (
    [UserId] int NOT NULL,
    [RoleId] int NOT NULL,
    CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
    CONSTRAINT [FK_AspNetUserRoles_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AspNetUserRoles_Usuarios_UserId] FOREIGN KEY ([UserId]) REFERENCES [Usuarios] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [AspNetUserTokens] (
    [UserId] int NOT NULL,
    [LoginProvider] nvarchar(450) NOT NULL,
    [Name] nvarchar(450) NOT NULL,
    [Value] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
    CONSTRAINT [FK_AspNetUserTokens_Usuarios_UserId] FOREIGN KEY ([UserId]) REFERENCES [Usuarios] ([Id]) ON DELETE CASCADE
);

-- ÍNDICES
CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);

CREATE INDEX [IX_Bitacoras_Fecha] ON [Bitacoras] ([Fecha]);
CREATE INDEX [IX_Bitacoras_UsuarioId] ON [Bitacoras] ([UsuarioId]);

CREATE UNIQUE INDEX [IX_Clientes_CedulaHash] ON [Clientes] ([CedulaHash]) WHERE [CedulaHash] IS NOT NULL;
CREATE UNIQUE INDEX [IX_Clientes_Telefono] ON [Clientes] ([Telefono]);

CREATE INDEX [IX_Cuotas_FechaVencimiento_Estado] ON [Cuotas] ([FechaVencimiento], [Estado]);
CREATE INDEX [IX_Cuotas_PrestamoId] ON [Cuotas] ([PrestamoId]);

CREATE INDEX [IX_Gastos_Categoria] ON [Gastos] ([Categoria]);
CREATE INDEX [IX_Gastos_Fecha] ON [Gastos] ([Fecha]);
CREATE INDEX [IX_Gastos_UsuarioId] ON [Gastos] ([UsuarioId]);

CREATE INDEX [IX_Notificaciones_ClienteId] ON [Notificaciones] ([ClienteId]);
CREATE INDEX [IX_Notificaciones_CuotaId] ON [Notificaciones] ([CuotaId]);
CREATE INDEX [IX_Notificaciones_Estado] ON [Notificaciones] ([Estado]);
CREATE INDEX [IX_Notificaciones_FechaCreacion] ON [Notificaciones] ([FechaCreacion]);
CREATE INDEX [IX_Notificaciones_PrestamoId] ON [Notificaciones] ([PrestamoId]);
CREATE INDEX [IX_Notificaciones_Tipo] ON [Notificaciones] ([Tipo]);

CREATE UNIQUE INDEX [IX_Pagos_CuotaId] ON [Pagos] ([CuotaId]);
CREATE INDEX [IX_Pagos_UsuarioId] ON [Pagos] ([UsuarioId]);

CREATE INDEX [IX_PasswordHistories_UsuarioId] ON [PasswordHistories] ([UsuarioId]);

CREATE UNIQUE INDEX [IX_Preferencias_Clave] ON [Preferencias] ([Clave]);

CREATE INDEX [IX_Prestamos_ClienteId] ON [Prestamos] ([ClienteId]);
CREATE INDEX [IX_Prestamos_Estado] ON [Prestamos] ([Estado]);

CREATE UNIQUE INDEX [RoleNameIndex] ON [Roles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL;
CREATE INDEX [EmailIndex] ON [Usuarios] ([NormalizedEmail]);
CREATE UNIQUE INDEX [UserNameIndex] ON [Usuarios] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL;

-- Registrar migración
INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260510163507_InitialSqlServer', N'10.0.0');

COMMIT;
GO

-- =============================================
-- NOTA: El usuario admin se crea automáticamente
-- al iniciar la app por primera vez (ver Program.cs).
-- Requiere la variable de entorno ADMIN_INITIAL_PASSWORD.
-- =============================================

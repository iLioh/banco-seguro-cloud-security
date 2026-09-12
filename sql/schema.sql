SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.ClientesDemo', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ClientesDemo
    (
        ClienteId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ClientesDemo PRIMARY KEY,
        Nombre NVARCHAR(100) NOT NULL,
        Segmento NVARCHAR(30) NOT NULL,
        FechaCreacion DATETIME2(0) NOT NULL CONSTRAINT DF_ClientesDemo_FechaCreacion DEFAULT SYSUTCDATETIME()
    );
END;

IF OBJECT_ID(N'dbo.CuentasDemo', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CuentasDemo
    (
        CuentaId INT IDENTITY(1001,1) NOT NULL CONSTRAINT PK_CuentasDemo PRIMARY KEY,
        ClienteId INT NOT NULL,
        TipoCuenta NVARCHAR(30) NOT NULL,
        Saldo DECIMAL(18,2) NOT NULL CONSTRAINT CK_CuentasDemo_Saldo CHECK (Saldo >= 0),
        Moneda CHAR(3) NOT NULL,
        CONSTRAINT FK_CuentasDemo_ClientesDemo FOREIGN KEY (ClienteId) REFERENCES dbo.ClientesDemo(ClienteId),
        CONSTRAINT CK_CuentasDemo_Moneda CHECK (Moneda IN ('PEN', 'USD'))
    );
END;

IF OBJECT_ID(N'dbo.OperacionesDemo', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.OperacionesDemo
    (
        OperacionId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_OperacionesDemo PRIMARY KEY,
        CuentaId INT NOT NULL,
        TipoOperacion NVARCHAR(30) NOT NULL,
        Monto DECIMAL(18,2) NOT NULL CONSTRAINT CK_OperacionesDemo_Monto CHECK (Monto > 0),
        FechaOperacion DATETIME2(0) NOT NULL CONSTRAINT DF_OperacionesDemo_FechaOperacion DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_OperacionesDemo_CuentasDemo FOREIGN KEY (CuentaId) REFERENCES dbo.CuentasDemo(CuentaId)
    );
END;

-- Ejecutar con sqlcmd definiendo AppServicePrincipalName con el nombre real de la identidad.
-- Ejemplo: sqlcmd -v AppServicePrincipalName="app-bancoseguro-dev-..."
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'$(AppServicePrincipalName)')
BEGIN
    CREATE USER [$(AppServicePrincipalName)] FROM EXTERNAL PROVIDER;
END;

GRANT SELECT ON OBJECT::dbo.ClientesDemo TO [$(AppServicePrincipalName)];
GRANT SELECT ON OBJECT::dbo.CuentasDemo TO [$(AppServicePrincipalName)];

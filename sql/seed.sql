SET NOCOUNT ON;
SET XACT_ABORT ON;

IF NOT EXISTS (SELECT 1 FROM dbo.ClientesDemo)
BEGIN
    INSERT INTO dbo.ClientesDemo (Nombre, Segmento)
    VALUES
        (N'Cliente Demo Andino', N'Personal'),
        (N'Comercio Ficticio Norte', N'Empresas'),
        (N'Cliente Demo Pacífico', N'Preferente');

    INSERT INTO dbo.CuentasDemo (ClienteId, TipoCuenta, Saldo, Moneda)
    SELECT ClienteId, N'Ahorros', 8450.75, 'PEN' FROM dbo.ClientesDemo WHERE Nombre = N'Cliente Demo Andino'
    UNION ALL
    SELECT ClienteId, N'Corriente', 24350.00, 'PEN' FROM dbo.ClientesDemo WHERE Nombre = N'Comercio Ficticio Norte'
    UNION ALL
    SELECT ClienteId, N'Ahorros', 3220.40, 'USD' FROM dbo.ClientesDemo WHERE Nombre = N'Cliente Demo Pacífico';

    INSERT INTO dbo.OperacionesDemo (CuentaId, TipoOperacion, Monto, FechaOperacion)
    SELECT CuentaId, N'Depósito demo', 500.00, DATEADD(DAY, -2, SYSUTCDATETIME()) FROM dbo.CuentasDemo WHERE CuentaId = 1001
    UNION ALL
    SELECT CuentaId, N'Pago demo', 125.50, DATEADD(DAY, -1, SYSUTCDATETIME()) FROM dbo.CuentasDemo WHERE CuentaId = 1002
    UNION ALL
    SELECT CuentaId, N'Transferencia demo', 80.00, SYSUTCDATETIME() FROM dbo.CuentasDemo WHERE CuentaId = 1003;
END;


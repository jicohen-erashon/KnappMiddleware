namespace KnappMiddleware.Contratos.Admin;

/// <summary>Snapshot de una fila de la tabla configuracion tal como la devuelve GET /api/v1/configurations.</summary>
public sealed record ConfiguracionDto(
    string Clave,
    string Valor,
    string? Descripcion,
    DateTime CreadoEn,
    DateTime ActualizadoEn);

/// <summary>Cuerpo de PUT /api/v1/configurations/{clave}.</summary>
public sealed record ActualizarConfiguracionRequest(
    string Valor,
    string? Descripcion);

/// <summary>Cuerpo de POST /api/v1/configurations.</summary>
public sealed record CrearConfiguracionRequest(
    string Clave,
    string Valor,
    string? Descripcion);

/// <summary>Devuelto por POST/PUT para indicar si la clave fue creada (true) o actualizada (false).</summary>
public sealed record ConfiguracionCambiadaResult(
    ConfiguracionDto Configuracion,
    bool Created);

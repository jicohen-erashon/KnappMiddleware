namespace KnappMiddleware.Domain.Matrix;

/// <summary>Una fila de la matriz de control: emisor×tipo-telegrama×estación → acción.</summary>
public sealed record MatrixEntry(string Emisor, string TipoTelegrama, string Estacion, MatrixAction Accion);

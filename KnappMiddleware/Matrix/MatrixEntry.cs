namespace KnappMiddleware.Matrix;

/// <summary>Una fila de la matriz de control: emisor×tipo-telegrama×estación → acción.</summary>
public sealed record MatrixEntry(string Emisor, string TipoTelegrama, string Estacion, MatrixAction Accion, DateTime CreadoEn, DateTime ActualizadoEn);

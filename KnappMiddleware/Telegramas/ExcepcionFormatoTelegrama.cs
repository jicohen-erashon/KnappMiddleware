namespace KnappMiddleware.Telegramas;

public sealed class ExcepcionFormatoTelegrama : Exception
{
    public ExcepcionFormatoTelegrama(string message) : base(message)
    {
    }

    public ExcepcionFormatoTelegrama(string message, Exception innerException) : base(message, innerException)
    {
    }
}

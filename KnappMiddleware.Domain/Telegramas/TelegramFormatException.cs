namespace KnappMiddleware.Domain.Telegramas;

public sealed class TelegramFormatException : Exception
{
    public TelegramFormatException(string message) : base(message)
    {
    }

    public TelegramFormatException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

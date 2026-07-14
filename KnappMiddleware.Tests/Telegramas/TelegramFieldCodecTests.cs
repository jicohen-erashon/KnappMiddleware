using KnappMiddleware.Domain.Telegramas;

namespace KnappMiddleware.Tests.Telegramas;

public class TelegramFieldCodecTests
{
    private static readonly IReadOnlyList<FieldDefinition> Schema =
    [
        new FieldDefinition("Nombre", 5, FieldKind.AlphaNumeric),
        new FieldDefinition("Cantidad", 3, FieldKind.Numeric),
        new FieldDefinition("FechaEntrega", 8, FieldKind.Date)
    ];

    [Fact]
    public void Encode_PadsEachFieldAccordingToKind()
    {
        var values = new Dictionary<string, string?>
        {
            ["Nombre"] = "AB",
            ["Cantidad"] = "7",
            ["FechaEntrega"] = null
        };

        var data = TelegramFieldCodec.Encode(Schema, values);

        Assert.Equal("AB   007" + "00000000", data);
    }

    [Fact]
    public void Decode_TrimsAlphaNumericAndNumericAndBlankDate()
    {
        var data = "AB   007" + "00000000";

        var fields = TelegramFieldCodec.Decode(Schema, data);

        Assert.Equal("AB", fields["Nombre"]);
        Assert.Equal("7", fields["Cantidad"]);
        Assert.Equal(string.Empty, fields["FechaEntrega"]);
    }

    [Fact]
    public void Decode_NonZeroDateFieldRoundTrips()
    {
        var values = new Dictionary<string, string?>
        {
            ["Nombre"] = "X",
            ["Cantidad"] = "1",
            ["FechaEntrega"] = "20260713"
        };

        var data = TelegramFieldCodec.Encode(Schema, values);
        var fields = TelegramFieldCodec.Decode(Schema, data);

        Assert.Equal("20260713", fields["FechaEntrega"]);
    }

    [Fact]
    public void Encode_MissingValue_DefaultsToEmptyOrZero()
    {
        var values = new Dictionary<string, string?>();

        var data = TelegramFieldCodec.Encode(Schema, values);

        Assert.Equal("     000" + "00000000", data);
    }

    [Fact]
    public void Encode_AlphaNumericValueTooLong_Throws()
    {
        var values = new Dictionary<string, string?> { ["Nombre"] = "DEMASIADO" };

        Assert.Throws<TelegramFormatException>(() => TelegramFieldCodec.Encode(Schema, values));
    }

    [Fact]
    public void Encode_NumericValueTooLong_Throws()
    {
        var values = new Dictionary<string, string?> { ["Cantidad"] = "12345" };

        Assert.Throws<TelegramFormatException>(() => TelegramFieldCodec.Encode(Schema, values));
    }

    [Fact]
    public void Encode_NumericValueNonDigit_Throws()
    {
        var values = new Dictionary<string, string?> { ["Cantidad"] = "1A" };

        Assert.Throws<TelegramFormatException>(() => TelegramFieldCodec.Encode(Schema, values));
    }

    [Fact]
    public void Decode_DataShorterThanSchema_Throws()
    {
        var shortData = "AB";

        Assert.Throws<TelegramFormatException>(() => TelegramFieldCodec.Decode(Schema, shortData));
    }

    [Fact]
    public void Decode_DataLongerThanSchema_Throws()
    {
        var longData = "AB   007" + "00000000" + "EXTRA";

        Assert.Throws<TelegramFormatException>(() => TelegramFieldCodec.Decode(Schema, longData));
    }
}

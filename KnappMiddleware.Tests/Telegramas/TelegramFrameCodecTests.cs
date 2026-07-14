using KnappMiddleware.Domain.Telegramas;

namespace KnappMiddleware.Tests.Telegramas;

public class TelegramFrameCodecTests
{
    [Fact]
    public void Encode_WrapsDataWithDelimitersAndLength()
    {
        var frame = TelegramFrameCodec.Encode("HELLO");

        Assert.Equal("\n00010HELLO\r", frame);
    }

    [Fact]
    public void Decode_ReturnsOriginalData()
    {
        var frame = TelegramFrameCodec.Encode("12N0001ABC");

        var data = TelegramFrameCodec.Decode(frame);

        Assert.Equal("12N0001ABC", data);
    }

    [Fact]
    public void Encode_ThenDecode_RoundTrips()
    {
        const string original = "140ARTICULO123";

        var roundTripped = TelegramFrameCodec.Decode(TelegramFrameCodec.Encode(original));

        Assert.Equal(original, roundTripped);
    }

    [Fact]
    public void Decode_MissingStartDelimiter_Throws()
    {
        var malformed = "00010HELLO\r";

        Assert.Throws<TelegramFormatException>(() => TelegramFrameCodec.Decode(malformed));
    }

    [Fact]
    public void Decode_MissingEndDelimiter_Throws()
    {
        var malformed = "\n00010HELLO";

        Assert.Throws<TelegramFormatException>(() => TelegramFrameCodec.Decode(malformed));
    }

    [Fact]
    public void Decode_LengthMismatch_Throws()
    {
        var malformed = "\n00099HELLO\r";

        Assert.Throws<TelegramFormatException>(() => TelegramFrameCodec.Decode(malformed));
    }

    [Fact]
    public void Decode_NonNumericLength_Throws()
    {
        var malformed = "\nABCDEHELLO\r";

        Assert.Throws<TelegramFormatException>(() => TelegramFrameCodec.Decode(malformed));
    }

    [Fact]
    public void Encode_DataTooLongForLengthField_Throws()
    {
        var tooLong = new string('A', TelegramFrameCodec.MaxTotalLength);

        Assert.Throws<TelegramFormatException>(() => TelegramFrameCodec.Encode(tooLong));
    }
}

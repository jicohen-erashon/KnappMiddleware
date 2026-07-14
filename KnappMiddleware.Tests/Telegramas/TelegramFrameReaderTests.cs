using KnappMiddleware.Domain.Telegramas;

namespace KnappMiddleware.Tests.Telegramas;

public class TelegramFrameReaderTests
{
    [Fact]
    public void Feed_CompleteFrameInOneChunk_ReturnsOneFrame()
    {
        var reader = new TelegramFrameReader();

        var frames = reader.Feed(TelegramFrameCodec.Encode("12N0001"));

        Assert.Equal(["12N0001"], frames);
    }

    [Fact]
    public void Feed_FrameSplitAcrossChunks_ReassemblesOnceComplete()
    {
        var reader = new TelegramFrameReader();
        var raw = TelegramFrameCodec.Encode("22N0001OK");

        var firstChunk = raw[..4];
        var secondChunk = raw[4..];

        var fromFirst = reader.Feed(firstChunk);
        var fromSecond = reader.Feed(secondChunk);

        Assert.Empty(fromFirst);
        Assert.Equal(["22N0001OK"], fromSecond);
    }

    [Fact]
    public void Feed_MultipleFramesInOneChunk_ReturnsAllInOrder()
    {
        var reader = new TelegramFrameReader();
        var raw = TelegramFrameCodec.Encode("FRAME1") + TelegramFrameCodec.Encode("FRAME2");

        var frames = reader.Feed(raw);

        Assert.Equal(["FRAME1", "FRAME2"], frames);
    }

    [Fact]
    public void Feed_DiscardsNoiseBeforeStartDelimiter()
    {
        var reader = new TelegramFrameReader();
        var raw = "garbage" + TelegramFrameCodec.Encode("CLEAN");

        var frames = reader.Feed(raw);

        Assert.Equal(["CLEAN"], frames);
    }

    [Fact]
    public void Reset_DiscardsPartialBuffer()
    {
        var reader = new TelegramFrameReader();
        var raw = TelegramFrameCodec.Encode("SHOULDNOTAPPEAR");

        reader.Feed(raw[..4]);
        reader.Reset();
        var frames = reader.Feed(TelegramFrameCodec.Encode("AFTERRESET"));

        Assert.Equal(["AFTERRESET"], frames);
    }
}

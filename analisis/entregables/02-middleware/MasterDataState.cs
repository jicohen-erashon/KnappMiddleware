namespace KnappMiddleware.Telegramas.Dispatching;

/// <summary>
/// Estado global de las transmisiones de datos maestros abiertas/cerradas. Necesario
/// porque KiSoft rechaza un 14N si NO se envió antes el 140 o el 141 (estado 93/94).
/// </summary>
public sealed class MasterDataState
{
    private MasterDataSession _article = MasterDataSession.Closed;
    private MasterDataSession _partner = MasterDataSession.Closed;
    private MasterDataSession _route   = MasterDataSession.Closed;

    public MasterDataSession Article { get => _article; set => _article = value; }
    public MasterDataSession Partner { get => _partner; set => _partner = value; }
    public MasterDataSession Route   { get => _route;   set => _route   = value; }

    public bool IsOpen(MasterDataDomain domain) => domain switch
    {
        MasterDataDomain.Article => _article == MasterDataSession.Open,
        MasterDataDomain.Partner => _partner == MasterDataSession.Open,
        MasterDataDomain.Route   => _route   == MasterDataSession.Open,
        _ => false
    };

    /// <summary>
    /// Llamado por el handler cuando llega un 140/141/149 (artículos), 150/151/159 (socios),
    /// 160/161/169 (rutas). Devuelve el identificador 2XX a entregar como acknowledgment.
    /// </summary>
    public MasterDataAck Acknowledge(string idrecord, MasterDataDomain domain)
    {
        var s = idrecord switch
        {
            "140" or "141" => _article = MasterDataSession.Open,
            "149"          => _article = MasterDataSession.Closed,
            "150" or "151" => _partner = MasterDataSession.Open,
            "159"          => _partner = MasterDataSession.Closed,
            "160" or "161" => _route   = MasterDataSession.Open,
            "169"          => _route   = MasterDataSession.Closed,
            _              => MasterDataSession.Closed
        };
        var ackid = idrecord switch
        {
            "140" => "240",
            "141" => "241",
            "149" => "249",
            "150" => "250",
            "151" => "251",
            "159" => "259",
            "160" => "260",
            "161" => "261",
            "169" => "269",
            _      => "000"
        };
        return new MasterDataAck(ackid, s);
    }
}

public enum MasterDataDomain { Article, Partner, Route }
public enum MasterDataSession { Closed, Open }

public sealed record MasterDataAck(string AckIdRecord, MasterDataSession NewState);

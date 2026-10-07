namespace KnappMiddleware.Eventos;

/// <summary>
/// Maneja un tipo específico de evento que KiSoft empuja por el canal 9802 (uno por identificador de
/// registro: 32R, 3IR, etc.). <see cref="DespachadorEventoKiSoft"/> prueba cada manejador registrado
/// y delega en el primero que acepte la trama — así el acuse (HIS §2.6, siempre ≤10s) lo emite
/// exactamente un manejador por trama, nunca más de uno.
/// </summary>
public interface IManejadorEventoKiSoft
{
    bool PuedeManejar(string data);

    Task ManejarAsync(string data, CancellationToken cancellationToken);
}

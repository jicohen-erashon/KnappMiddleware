namespace KnappMiddleware.Domain.Auth;

/// <summary>Rol de la cuenta de acceso. SuperUsuario: acceso a toda la Api (operación/administración). Sap: solo los endpoints SAP-facing.</summary>
public enum UserRole
{
    SuperUsuario,
    Sap
}

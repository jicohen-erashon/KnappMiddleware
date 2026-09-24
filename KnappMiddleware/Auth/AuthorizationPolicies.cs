namespace KnappMiddleware.Auth;

/// <summary>Nombres de policies de autorización. Endpoints SAP-facing (sftp-file, futuros order/article/...) usan SapOrSuperUsuario.</summary>
public static class AuthorizationPolicies
{
    public const string SapOrSuperUsuario = "SapOrSuperUsuario";
}

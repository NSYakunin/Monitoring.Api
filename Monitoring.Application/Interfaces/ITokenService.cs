namespace Monitoring.Application.Interfaces
{
    public interface ITokenService
    {
        string GenerateToken(string userName, int divisionId, IList<string> rolesOrClaims = null);
    }
}
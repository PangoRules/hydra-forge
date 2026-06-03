namespace HydraForge.Application.Auth.Ports;

public interface IAccessTokenIssuer
{
    string IssueToken(HydraForge.Domain.Entities.Auth.User user);
}
using System.Net;

namespace NetTunnel.Application.Interfaces.Sessions
{
    public interface IClientSessionFactory
    {
        IClientSession CreateSession(ITransportClient _replyClient, IPEndPoint replyEndpoint);
    }
}

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using SignalRHubs.Infrastructure;
using SignalRHubs.Models;

namespace SignalRHubs.Hubs;

[GenerateSignalRFrontendType]
[HubRoute("hubs/[Hub]")]
public sealed class ChatHub : Hub
{
    [HubMethodName("SendMessage")]
    public Task SendAsync(string message, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public IAsyncEnumerable<ChatMessage> StreamMessages(string room, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    [NonHubMethod]
    public Task HiddenAsync()
    {
        throw new NotImplementedException();
    }

    public Task OnConnectedAsync()
    {
        throw new NotImplementedException();
    }
}

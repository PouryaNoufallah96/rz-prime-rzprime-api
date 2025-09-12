using Microsoft.AspNetCore.SignalR;

namespace RZPrime.Services._User._Hub
{
    public class NonceNotifyHub : Hub
    {
        public override async Task OnConnectedAsync()
        {

            await base.OnConnectedAsync();
        }

        public async Task RegisterNonce(string nonce) 
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, nonce);
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            await base.OnDisconnectedAsync(exception);
        }

        //public async Task NotifyNonce(string nonce, string message)
        //{
        //    await Clients.Group(nonce).SendAsync("notifyNonceMessage", message);
        //}
    }
}

using Microsoft.AspNetCore.SignalR;

public class PaidOrderHub : Hub
{
    public override async Task OnConnectedAsync()
    {

        await base.OnConnectedAsync();
    }

    public async Task RegisterWallet(string walletAddress)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, walletAddress);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }

    //public async Task SendPaidOrderNotification(string walletAddress)
    //{
    //    await Clients.All.SendAsync("NotifyPaidOrder", walletAddress);
    //}
}
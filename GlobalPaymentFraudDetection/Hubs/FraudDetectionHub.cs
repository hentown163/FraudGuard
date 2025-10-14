using Microsoft.AspNetCore.SignalR;
using GlobalPaymentFraudDetection.Models;

namespace GlobalPaymentFraudDetection.Hubs;

public class FraudDetectionHub : Hub
{
    public async Task SendTransactionUpdate(Transaction transaction)
    {
        await Clients.All.SendAsync("ReceiveTransactionUpdate", transaction);
    }

    public async Task SendFraudAlert(FraudAlert alert)
    {
        await Clients.All.SendAsync("ReceiveFraudAlert", alert);
    }

    public async Task SendScoreUpdate(FraudScoreResponse score)
    {
        await Clients.All.SendAsync("ReceiveScoreUpdate", score);
    }
}

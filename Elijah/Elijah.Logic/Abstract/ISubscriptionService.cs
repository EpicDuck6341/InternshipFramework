namespace Elijah.Logic.Abstract;

public interface ISubscriptionService
{
    Task SubscribeExistingAsync();
    Task SubscribeAsync(string address);
}
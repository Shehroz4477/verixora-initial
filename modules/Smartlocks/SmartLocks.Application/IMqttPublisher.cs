namespace SmartLocks.Application;

public interface IMqttPublisher
{
    Task PublishAsync(string topic, string payload);
}

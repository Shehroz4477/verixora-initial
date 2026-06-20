using MQTTnet;
using MQTTnet.Client;
using SmartLocks.Application;

namespace SmartLocks.Infrastructure;

public class MqttPublisher : IMqttPublisher, IDisposable
{
    private readonly IMqttClient _mqttClient;
    private readonly MqttClientOptions _options;

    public MqttPublisher(string brokerHost, int brokerPort)
    {
        var factory = new MqttFactory();
        _mqttClient = factory.CreateMqttClient();

        _options = new MqttClientOptionsBuilder()
            .WithTcpServer(brokerHost, brokerPort)
            .WithCleanSession()
            .Build();

        // Connect in background – real implementation would handle reconnect, etc.
        Task.Run(async () => await ConnectAsync());
    }

    private async Task ConnectAsync()
    {
        try
        {
            await _mqttClient.ConnectAsync(_options);
            Console.WriteLine("MQTT Publisher connected.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"MQTT connection failed: {ex.Message}");
        }
    }

    public async Task PublishAsync(string topic, string payload)
    {
        if (!_mqttClient.IsConnected)
            await ConnectAsync();

        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();

        await _mqttClient.PublishAsync(message);
        Console.WriteLine($"MQTT PUBLISH: {topic} -> {payload}");
    }

    public void Dispose() => _mqttClient?.Dispose();
}

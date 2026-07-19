using MQTTnet;
using MQTTnet.Client;
using SmartLocks.Application;

namespace SmartLocks.Infrastructure;

public sealed class MqttPublisher : IMqttPublisher, IDisposable
{
    private readonly IMqttClient _mqttClient;
    private readonly MqttClientOptions _options;
    private readonly SemaphoreSlim _connectionGate = new(1, 1);

    public MqttPublisher(string brokerHost, int brokerPort)
    {
        if (string.IsNullOrWhiteSpace(brokerHost))
            throw new ArgumentException("MQTT broker host is required.", nameof(brokerHost));

        _mqttClient = new MqttFactory().CreateMqttClient();
        _options = new MqttClientOptionsBuilder()
            .WithTcpServer(brokerHost, brokerPort)
            .WithCleanSession()
            .Build();
    }

    public async Task PublishAsync(string topic, string payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);

        await EnsureConnectedAsync();
        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();

        await _mqttClient.PublishAsync(message);
    }

    private async Task EnsureConnectedAsync()
    {
        if (_mqttClient.IsConnected)
            return;

        await _connectionGate.WaitAsync();
        try
        {
            if (!_mqttClient.IsConnected)
                await _mqttClient.ConnectAsync(_options);
        }
        finally
        {
            _connectionGate.Release();
        }
    }

    public void Dispose()
    {
        _connectionGate.Dispose();
        _mqttClient.Dispose();
    }
}

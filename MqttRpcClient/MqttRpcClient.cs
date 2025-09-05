using MQTTnet;
using MQTTnet.Extensions.Rpc;
using MQTTnet.Protocol;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using MqttRpcCommon.MqttRpc;

namespace MqttRpcClient
{
    public class MqttRpcClient
    {
        public CancellationToken _cancellationToken;
        private readonly string _host;
        private readonly int _port;
        private readonly string _userName;
        private readonly string _password;
        private readonly string _clientId;
        private readonly double _reConnectSeconds;
        private readonly double _reConnectTimeout;
        public MqttClientFactory _mqttFactory = new MqttClientFactory();
        public IMqttRpcClient _mqttRpcClient;
        public IMqttClient MqttClient;
        public MqttClientOptions? MqttClientOptions;
        public MqttRpcClientOptions _mqttRpcClientOptions = new MqttRpcClientOptions()
        {
            TopicGenerationStrategy = new MqttSharedRpcClientTopicGenerationStrategy(),
        };
        public MqttRpcClient(string host, string username = "", string passWord = "", int port = 1883, string clientId = "", double reConnectSeconds = 1, double reConnectTimeout = 30, CancellationToken cancellationToken = default)
        {
            _host = host;
            _port = port;
            _userName = username;
            _password = passWord;
            _clientId = clientId == "" ? $"{Process.GetCurrentProcess().ProcessName}-{Guid.NewGuid().ToString()}" : clientId;
            _cancellationToken = cancellationToken;
            _reConnectSeconds = reConnectSeconds;
            _reConnectTimeout = reConnectTimeout;
            MqttClient = _mqttFactory.CreateMqttClient();
        }

        public void StartConnect()
        {
            if (MqttClientOptions == null)
            {
                var builder = new MqttClientOptionsBuilder()
                    .WithTcpServer(_host, _port);
                if (_userName != "")
                {
                    builder.WithCredentials(_userName, _password);
                }

                builder.WithTimeout(TimeSpan.FromSeconds(_reConnectTimeout));
                MqttClientOptions = builder.WithClientId(_clientId)
                    .WithCleanSession()
                    .Build();
            }
            _mqttRpcClient = _mqttFactory.CreateMqttRpcClient(MqttClient, _mqttRpcClientOptions);
            _ = Task.Run(async () =>
            {
                while (!this._cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        // This code will also do the very first connect! So no call to _ConnectAsync_ is required in the first place.
                        if (!await MqttClient.TryPingAsync())
                        {
                            await MqttClient.ConnectAsync(MqttClientOptions, this._cancellationToken);

                        }
                    }
                    catch
                    {
                        // Handle the exception properly (logging etc.).
                    }
                    finally
                    {
                        // Check the connection state every 5 seconds and perform a reconnect if required.
                        await Task.Delay(TimeSpan.FromSeconds(_reConnectSeconds));
                    }
                }
            });
        }

        public async Task WaitConnectedAsync(CancellationToken cancellationToken = default)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (await MqttClient.TryPingAsync(cancellationToken))
                {
                    return;
                }

                await Task.Delay(100, cancellationToken);
            }
        }

        public async Task<T?> CallAsync<T>(string funcName,
            params object[] objects)
        {
            var cancellationToken = CancellationToken.None;
            if (objects.Length > 0)
            {
                if (objects[^1] is CancellationToken token)
                {
                    cancellationToken = token;
                }
            }
            var result = await _mqttRpcClient.ExecuteAsync(funcName,
                Encoding.UTF8.GetBytes(JsonSerializer.Serialize(objects)), MqttQualityOfServiceLevel.ExactlyOnce, null,
                cancellationToken);
            var strResult = Encoding.UTF8.GetString(result);
            var resultObject = JsonSerializer.Deserialize<JsonElement>(strResult)!;
            var error = resultObject.GetProperty("Error");
            if (error.ValueKind != JsonValueKind.Null)
            {
                throw new Exception(error.GetString());
            }
            return resultObject.GetProperty("Data").Deserialize<T>();
        }
    }
}

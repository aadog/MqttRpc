using MQTTnet;
using MQTTnet.Protocol;
using MqttRpcCommon;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace MqttRpcServer
{
    public class MqttRpcServer
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
        public IMqttClient MqttClient;
        public MqttClientOptions? MqttClientOptions;
        public IMqttRpcFuncServer _mqttRpcFuncServer;
        public MqttRpcServer(IMqttRpcFuncServer mqttRpcFuncServer, string host, string username = "", string passWord = "", int port = 1883, string clientId = "", double reConnectSeconds = 1, double reConnectTimeout = 30, CancellationToken cancellationToken = default)
        {
            _host = host;
            _port = port;
            _userName = username;
            _password = passWord;
            _clientId = clientId == "" ? $"{Process.GetCurrentProcess().ProcessName}-{Guid.NewGuid().ToString()}" : clientId;
            _cancellationToken = cancellationToken;
            _reConnectSeconds = reConnectSeconds;
            _reConnectTimeout = reConnectTimeout;
            _mqttRpcFuncServer=mqttRpcFuncServer;
            MqttClient = _mqttFactory.CreateMqttClient();

            MqttClient.ConnectedAsync += async (e) =>
            {
                await this.InitFunctions();
            };
            
            MqttClient.ApplicationMessageReceivedAsync += async (e) =>
            {
                var topic = e.ApplicationMessage.Topic;
                var topicResponse = e.ApplicationMessage.ResponseTopic;
                var func = topic.Split("/")[^1];
                var deviceId = topic.Split("/")[^2];
                var response = new RpcResponse();
                try
                {
                    // Console.WriteLine(Encoding.UTF8.GetString(e.ApplicationMessage.Payload));
                    var strRequest = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
                    var requestParams = JsonSerializer.Deserialize<JsonElement[]>(strRequest)!;
                    var fn = _mqttRpcFuncServer.GetFunctions()[func];
                    var result = fn.DynamicInvoke(ConvertCallParamsObjects(requestParams,fn));
                    if (result is Task<object?> task)
                    {
                        response.Data = await task;
                    }
                    else
                    {
                        response.Data = result;
                    }
                }
                catch (Exception exception)
                {
                    response.Error = exception.Message;
                }
                finally
                {
                    await MqttClient.PublishStringAsync(topicResponse, JsonSerializer.Serialize(response),
                        MqttQualityOfServiceLevel.ExactlyOnce);
                }
            };
        }

        public object[] ConvertCallParamsObjects(JsonElement[] requestParams,Delegate fn)
        {
            if (requestParams.Length == 0)
            {
                return [];
            }

            // 获取方法的参数类型
            var methodParams = fn.Method.GetParameters();
            // 动态构造参数
            object[] parameters = new object[requestParams.Length];
            for (int i = 0; i < requestParams.Length; i++)
            {
                var param = requestParams[i];

                // 获取目标类型
                var paramType = methodParams[i].ParameterType;

                // 反序列化为目标类型
                parameters[i] = param.Deserialize(paramType);
            }

            return parameters;
        }

        public async Task InitFunctions()
        {
            foreach (var fn in _mqttRpcFuncServer.GetFunctions())
            {
                var attributes = fn.Value.Method.GetCustomAttributes(typeof(MqttRpcShareFuncAttribute), false);
                if (attributes.Any())
                {
                    await MqttClient.SubscribeAsync($"$share/MQTTnet.Share.RPC/+/{fn.Key}", cancellationToken: _cancellationToken);
                }
                else
                {
                    await MqttClient.SubscribeAsync($"+/{fn.Key}", cancellationToken: _cancellationToken);
                }
            }
        }

        public async Task RunAsync(CancellationToken cancellationToken = default)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(100, cancellationToken);
            }
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
            _ = Task.Run(async () =>
            {
                while (!this._cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        // This code will also do the very first connect! So no call to _ConnectAsync_ is required in the first place.
                        if (!await MqttClient.TryPingAsync(cancellationToken: _cancellationToken))
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
                        await Task.Delay(TimeSpan.FromSeconds(_reConnectSeconds), _cancellationToken);
                    }
                }
            }, _cancellationToken);
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

    }
}

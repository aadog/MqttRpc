using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MqttRpcCommon
{
    public record RpcResponse
    {
        public string? Error { get; set; }
        public object? Data { get; set; }
    }
}

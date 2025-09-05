namespace MqttRpcServer
{
 
    // 定义自定义特性
    [AttributeUsage(AttributeTargets.Method|AttributeTargets.Interface)]
    public class MqttRpcShareFuncAttribute() : Attribute
    {
    }
    public interface IMqttRpcFuncServer
    {
        public Dictionary<string, Delegate> GetFunctions();
    }
}

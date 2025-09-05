namespace MqttRpcCommon
{
    using MQTTnet.Extensions.Rpc;
    namespace MqttRpc
    {
        public class MqttSharedRpcClientTopicGenerationStrategy : IMqttRpcClientTopicGenerationStrategy
        {
            public MqttRpcTopicPair CreateRpcTopics(TopicGenerationContext context)
            {
                ArgumentNullException.ThrowIfNull(context);

                if (context.MethodName.Contains("/") || context.MethodName.Contains("+") || context.MethodName.Contains("#"))
                {
                    throw new ArgumentException("The method name cannot contain /, + or #.");
                }

                var requestTopic = $"{Guid.NewGuid():N}/{context.MethodName}";
                var responseTopic = requestTopic + "/response";

                return new MqttRpcTopicPair
                {
                    RequestTopic = requestTopic,
                    ResponseTopic = responseTopic
                };
            }
        }
    }
}

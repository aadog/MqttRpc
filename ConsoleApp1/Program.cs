using MqttRpcServer;
using MqttRpcClient;

namespace ConsoleApp1
{
    public class V
    {
        public string z { get; set; }
        public string x { get; set; }
    }

    public class Z : IMqttRpcFuncServer
    {
        public Dictionary<string, Delegate> GetFunctions()
        {
            return new Dictionary<string, Delegate>()
            {
                {"z1",Z1}
            };
        }
        [MqttRpcShareFunc]
        public async Task<object> Z1(string a1,int a2,V v)
        {
            Console.WriteLine(a1);
            Console.WriteLine(a2);
            Console.WriteLine(v.z);
            return v;
        }
    }

    internal class Program
    {
        static async Task Main(string[] args)
        {
            _ = Task.Run(async () =>
            {
                var s = new MqttRpcServer.MqttRpcServer(new Z(), "mobile.ptask.fun", "test", "test");
                s.StartConnect();
                await s.RunAsync();
            });
            var c = new MqttRpcClient("mobile.ptask.fun", "test", "test");
            c.StartConnect();
            await c.WaitConnectedAsync();
            try
            {
                var s = await c.CallAsync<V>("z1", "string", 2,new V(){z="6",x="8888v"});
                Console.WriteLine(s.z);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }


        }
    }
}

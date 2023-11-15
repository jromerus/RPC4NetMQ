using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RPC4NetMq;
using Serilog;
using Serilog.Core;
using Serilog.Debugging;
using SimpleDemo;
using System.IO;
using System.Reflection;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace test
{
	class Program
	{
		public static void Main(string[] args)
		{
			Console.WriteLine("Hello World!");
			Log.Logger = new LoggerConfiguration()
			.MinimumLevel.Debug()
			.WriteTo.Console()			
			.CreateLogger();

            var loggerFactory = new LoggerFactory()
			.AddSerilog(Log.Logger);

            ILogger logger = loggerFactory.CreateLogger("RPCClient");           
			
            TestCalculator(logger);
            TestSendFile(logger);
        }
		
		private static void TestCalculator(ILogger logger)
		{
            // Serialize Test
            string json = JsonConvert.SerializeObject(User.Super());
            object? o = JsonConvert.DeserializeObject(json, typeof(List<User>));
            Console.WriteLine("Deserialized type: {0}", o.GetType());

            var server = RpcFactory.CreateServer<ICalculator>(new Calculator(), "tcp://127.0.0.1:13777", logger);
            server.Start();

            var calculator = RpcFactory.CreateClient<ICalculator>(">tcp://127.0.0.1:13777", logger, 5);
            var result = calculator.Add(1, 2);
            Console.WriteLine("Result is: {0}", result);

            var programmers = calculator.Programmers();
            Console.WriteLine("Programmer Count: {0}", programmers.Count);

            var goodProgrammers = calculator.GoodProgrammers(programmers);
            Console.WriteLine("Good Programmer Count: {0}", goodProgrammers.Count);

            calculator.SetProgrammers(programmers);

            Console.Write("Press any key to continue . . . ");
            Console.ReadKey(true);
            //server.Stop();
            Console.WriteLine("Shutting Down..");
        }

		private static void TestSendFile(ILogger logger)
		{
            var assembly = Assembly.GetExecutingAssembly();            
            var stream = assembly.GetManifestResourceStream("SimpleDemo.smiley.png");
            if (stream != null)
            {
                var memoryStream = new MemoryStream();
                stream.CopyTo(memoryStream);
                stream.Close();

                byte[] byteArray = memoryStream.ToArray();
                memoryStream.Close();

                var server = RpcFactory.CreateServer<IFileManager>(new FileManager(), "tcp://127.0.0.1:13777", logger);
                server.Start();

                var client = RpcFactory.CreateClient<IFileManager>(">tcp://127.0.0.1:13777", logger);
                client.SetFileToPath(@"c:\logs\smiley.png", byteArray);
                server.Stop();                
            }
        }
	}
}
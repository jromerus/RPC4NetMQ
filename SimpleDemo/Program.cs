using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RPC4NetMq;
using RPC4NetMq.Client;
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
			            
            //TestCalculator(logger);
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

            List<Task> tasks = new List<Task>();
            int numThreads = 200;

            for (int i = 0; i < numThreads; i++)
            {
                Console.WriteLine("Starting test {0}", i);
                try
                {
                    /// Create a new thread to test the RPC client                    
                    tasks.Add(Task.Run(() => {
                        DoCalculations(logger, i);

                        //var programmers = calculator.Programmers();
                        //Console.WriteLine("Thread {0} Programmer Count: {1}", i, programmers.Count);

                        //var goodProgrammers = calculator.GoodProgrammers(programmers);
                        //Console.WriteLine("Good Programmer Count: {0}", goodProgrammers.Count);

                        //calculator.SetProgrammers(programmers);
                    }));
                    //Thread.Sleep(100); // Simulate some delay before starting the next task
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error in test {0}: {1}", i, ex.Message);
                }
            }

            Task.WaitAll(tasks.ToArray(), -1);
            Console.Write("Press any key to continue . . . ");
            Console.ReadKey(true);
            server.Stop();
            server.Dispose();
            Console.WriteLine("Shutting Down..");
        }

        private static void DoCalculations(ILogger logger, int i)
        {
            var calculator = RpcFactory.CreateClient<ICalculator>(">tcp://127.0.0.1:13777", logger, 0);
            var result = calculator.Add(1, 2);
            Console.WriteLine("Thread {0} Result is: {1}", i, result);
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
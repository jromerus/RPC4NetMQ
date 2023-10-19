﻿using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RPC4NetMq;
using Serilog;
using Serilog.Debugging;

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

            Microsoft.Extensions.Logging.ILogger logger = loggerFactory.CreateLogger("RPCClient");

            // Serialize Test
            string json = JsonConvert.SerializeObject(User.Super());
			object o  =  JsonConvert.DeserializeObject(json,typeof(List<User>));
			Console.WriteLine ("Deserialized type: {0}", o.GetType() );
			
			bool logMessages = true;
			var server = RpcFactory.CreateServer<ICalculator>(new Calculator (), "tcp://127.0.0.1:13777", logger);
			server.Start();			

			var calculator = RpcFactory.CreateClient<ICalculator>(">tcp://127.0.0.1:13777", logger);
			var result = calculator.Add (1,2);
			Console.WriteLine ("Result is: {0}", result); 			
			
			var programmers = calculator.Programmers ();
			Console.WriteLine ("Programmer Count: {0}", programmers.Count); 			
			
			var goodProgrammers = calculator.GoodProgrammers (programmers);
			Console.WriteLine ("Good Programmer Count: {0}", goodProgrammers.Count); 						
			
			calculator.SetProgrammers (programmers);
			
			Console.Write("Press any key to continue . . . ");
			Console.ReadKey(true);
			server.Stop();
			Console.WriteLine ("Shutting Down..");
		}
		
		static void logClientMessage (Direction dir, string json) {
			Console.WriteLine("{0}: {1}", dir, json);
		}
	}
}
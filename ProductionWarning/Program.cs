using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProductionWarning
{
	class Program
	{
		private static readonly ILog Logger = LogManager.GetLogger(typeof(Program));


		static void Main(string[] args)
		{
			Logger.Info("Main");

			ProductionWarningInstance instance = ProductionWarningInstance.FromConfig();
			instance.Start();

			bool shouldRun = true;
			while(shouldRun)
			{
				ConsoleKeyInfo info = Console.ReadKey();
				if (info.KeyChar == 'q')
				{
					shouldRun = false;
				}
			}

			instance.Stop();

			Console.WriteLine("Done.");
		}
	}
}

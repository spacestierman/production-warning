using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using ProductionWarning;
using log4net;

namespace ProductionWarning.Service
{
	public partial class ProductionWarningService : ServiceBase
	{
		private static readonly ILog Logger = LogManager.GetLogger(typeof(ProductionWarningService));

		private ProductionWarningInstance _warning;

		public ProductionWarningService()
		{
			InitializeComponent();

			_warning = ProductionWarningInstance.FromConfig();
		}

		protected override void OnStart(string[] args)
		{
			Logger.Info("OnStart");
			_warning.Start();
		}

		protected override void OnStop()
		{
			Logger.Info("OnStop");
			_warning.Stop();
		}
	}
}

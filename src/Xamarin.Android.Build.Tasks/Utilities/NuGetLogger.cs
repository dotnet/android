using System;
using NuGet.Common;
using Microsoft.Build.Utilities;
using TPL = System.Threading.Tasks;

namespace Xamarin.Android.Tasks {

	class NuGetLogger : LoggerBase {
		Action<string> log;

		public NuGetLogger (Action<string> log)
		{
			this.log = log;
		}

		public override void Log (ILogMessage message) {
			log (message.Message);
		}

		public override void Log (LogLevel level, string data) {
			log (data);
		}

		public override TPL.Task LogAsync (ILogMessage message) {
			Log (message);
			return TPL.Task.FromResult(0);
		}

		public override TPL.Task LogAsync (LogLevel level, string data) {
			Log (level, data);
			return TPL.Task.FromResult(0);
		}
	}
}

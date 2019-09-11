using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks
{
	public class AndroidCreateDebugKey : KeyTool
	{
		public override string TaskPrefix => "ACD";

		public int Validity { get; set; }

		public string KeyAlgorithm { get; set; }

		string dname = "CN=Android Debug,O=Android,C=US";

		public AndroidCreateDebugKey ()
		{
			KeyAlgorithm = "RSA";
			Validity = 30 * 365; // 30 years
		}

		protected override CommandLineBuilder CreateCommandLine ()
		{
			var cmd = base.CreateCommandLine ();
			cmd.AppendSwitchIfNotNull ("-dname ", dname);
			cmd.AppendSwitchIfNotNull ("-keyalg ", KeyAlgorithm);
			cmd.AppendSwitchIfNotNull ("-validity ", Validity.ToString()); 
			return cmd;
		}
	}
}


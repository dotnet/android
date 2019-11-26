using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks
{
	public class AndroidCreateDebugKey : KeyTool
	{
		public override string TaskPrefix => "ACD";

		public int Validity { get; set; } = 30 * 365;

		public string KeyAlgorithm { get; set; } = "RSA";

		public string StoreType { get; set; } = "pkcs12";

		string dname = "CN=Android Debug,O=Android,C=US";

		protected override CommandLineBuilder CreateCommandLine ()
		{
			var cmd = base.CreateCommandLine ();
			cmd.AppendSwitchIfNotNull ("-dname ", dname);
			cmd.AppendSwitchIfNotNull ("-keyalg ", KeyAlgorithm);
			cmd.AppendSwitchIfNotNull ("-validity ", Validity.ToString()); 
			cmd.AppendSwitchIfNotNull ("-deststoretype ", StoreType);
			return cmd;
		}
	}
}


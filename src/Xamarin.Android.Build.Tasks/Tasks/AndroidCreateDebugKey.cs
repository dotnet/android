using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks
{
	public class AndroidCreateDebugKey : KeyTool
	{
		public int Validity { get; set; }

		public string KeyAlgorithm { get; set; }

		string dname = "CN=Android Debug,O=Android,C=US";

		public AndroidCreateDebugKey ()
		{
			KeyAlgorithm = "RSA";
			Validity = 30 * 365; // 30 years
		}

		public override bool Execute ()
		{
			Log.LogDebugMessage ("AndroidCreateDebugKey : {0}", Command);
			Log.LogDebugMessage ("          {0}",Validity);
			Log.LogDebugMessage ("          {0}",KeyAlgorithm);
			return base.Execute ();
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


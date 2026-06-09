//
// RunActivity.cs
//
// Author:
//       Jonathan Pryor <jonp@xamarin.com>
//
// Copyright (c) 2013 Xamarin Inc. (http://www.xamarin.com)
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System.IO;
using System.Xml;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mono.AndroidTools;
using Microsoft.Android.Build.Tasks;
using Xamarin.AndroidTools;
using Xamarin.AndroidTools.Debugging;
using Xamarin.Android.Build.Debugging.Tasks.Properties;

namespace Xamarin.Android.Tasks
{
	public class RunActivity : AsyncTask
	{
		public override string TaskPrefix => "RUNA";

		[Required]
		public string PackageName { get; set; }

		[Required]
		public string ActivityName { get; set; }

		public string AdbTarget { get; set; }

		public bool AttachDebugger { get; set; }

		public bool Server { get; set; }

		public string Port { get; set; }

		public int UserID { get; set; } = 0;

		public bool ForceStop { get; set; } = true;

		/// <summary>
		/// Gets or sets a value indicating whether Java debugging is allowed. Defaults to true, but will be available to be toggled off via -p:_AndroidAllowJavaDebugging=false.
		/// </summary>
		public bool AllowJavaDebugging { get; set; } = true;

		AndroidDevice Device;

		public RunActivity ()
		{
			Port = "10000";
		}

		public override bool Execute ()
		{
			Device = AndroidHelper.ParseTarget (AdbTarget, LogMessage, LogCodedError, logErrors: true, engine4: BuildEngine4);
			if (Device == null) {
				return false;
			}
			LogMessage ($"Found device: {Device.ID}");
			return base.Execute ();
		}

		public async override System.Threading.Tasks.Task RunTaskAsync ()
		{
			LogDebugMessage ($"  ActivityName: {ActivityName}");

			var amStartCommand = new AmStartCommand (PackageName, ActivityName);
			amStartCommand.ForceStop = ForceStop;
			amStartCommand.EnableDebugging = false;
			amStartCommand.User = UserID.ToString ();

			amStartCommand.Action = amStartCommand.Action ?? "android.intent.action.MAIN";
			amStartCommand.Categories = amStartCommand.Categories ?? new[] { "android.intent.category.LAUNCHER" };

			var startConfiguration = new ExecutionConfiguration (PackageName, amStartCommand);
			startConfiguration.AllowJavaDebugging = AllowJavaDebugging;
			startConfiguration.LogWiter = (s) => LogDebugMessage (s);

			if (AttachDebugger) {
				var port = int.Parse (Port);
				var ipAddress = Server ? System.Net.IPAddress.Loopback : System.Net.IPAddress.Parse("10.0.2.2");
				startConfiguration.Debugger.Address = ipAddress;
				startConfiguration.Debugger.SdbPort = port;
				startConfiguration.Debugger.StdoutPort = -1;
				startConfiguration.Debugger.Server = Server;
				LogMessage (string.Format (Resources.StartDebugger_ipAddress_port, ipAddress, port), MessageImportance.High);
				await Device.StartWithDebuggingAsync (startConfiguration, CancellationToken);
			} else {
				await Device.StartWithoutDebuggingAsync (startConfiguration, CancellationToken);
			}
		}
	}
}

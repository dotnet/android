using System;
using System.IO;

using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	public class ProfileNativeCode : AndroidAsyncTask
	{
		public override string TaskPrefix => "PNC";

		public string DeviceSdkVersion      { get; set; }
		public bool DeviceIsEmulator        { get; set; }
		public string[] DeviceSupportedAbis { get; set; }
		public string DevicePrimaryABI      { get; set; }
		public string SimplePerfDirectory   { get; set; }
		public string NdkPythonDirectory    { get; set; }

		public async override System.Threading.Tasks.Task RunTaskAsync ()
		{
		}
	}
}

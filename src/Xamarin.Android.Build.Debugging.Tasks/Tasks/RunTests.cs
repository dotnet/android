using System;
using System.IO;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;

using Xamarin.AndroidTools;

namespace Xamarin.Android.Tasks
{
	public class RunTests : ToolTask
	{
		public string AdbTarget { get; set; }

		public string AdbOptions { get; set; }
		[Required]
		public string AndroidPackage { get; set; }

		[Required]
		public string TargetTestActivity { get; set; }

		protected override string ToolName {
			get { return OS.IsWindows ? "adb.exe" : "adb"; }
		}

		protected override string GenerateFullPathToTool ()
		{
			return Path.Combine (ToolPath, ToolExe);
		}

		protected override string GenerateCommandLineCommands ()
		{
			// If we supported full MSBuild 4.0 expression syntax for [Class] and property access, we could write this...
			//<Exec Command="&quot;$(_AndroidPlatformToolsDirectory)adb&quot; $(AdbTarget) $(AdbOptions) shell am start -a android.intent.action.MAIN -c android.intent.category.LAUNCHER --ez automated true -n $(_AndroidPackage)/$(RootNamespace.ToLowerInvariant()).MainActivity" />
			return string.Format ("{0} {1} shell am start -a android.intent.action.MAIN -c android.intent.category.LAUNCHER --ez automated true -n {2}/{3}",
			                      AdbTarget, AdbOptions, AndroidPackage, TargetTestActivity);
		}
	}
}

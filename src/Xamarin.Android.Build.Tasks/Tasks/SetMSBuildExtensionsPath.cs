using System;
using System.IO;
using System.Linq;

using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;

namespace Xamarin.Android.Tasks
{
	public sealed class SetMSBuildExtensionsPath : Task
	{
		const   string      MSBuildExtensionsPath           = "MSBuildExtensionsPath";
		const   string      XBUILD_FRAMEWORK_FOLDERS_PATH   = "XBUILD_FRAMEWORK_FOLDERS_PATH";
		public override bool Execute ()
		{
			var frameworksPath  = Path.GetDirectoryName (typeof (SetMSBuildExtensionsPath).Assembly.Location);
			if (Path.DirectorySeparatorChar == '\\') {
				// TODO: Default Windows search location
			} else {
				// e == $prefix/lib/xbuild/Xamarin/Android
				// Want: $prefix/lib/xbuild-frameworks
				if (!frameworksPath.EndsWith ("xbuild/Xamarin/Android", StringComparison.OrdinalIgnoreCase)) {
					throw new NotSupportedException ("Cannot determine path to xbuild-frameworks!");
				}
				frameworksPath  = Path.GetDirectoryName (Path.GetDirectoryName (Path.GetDirectoryName (frameworksPath)));
				frameworksPath  = Path.Combine (frameworksPath, "xbuild-frameworks");
			}

			UpdateEnvironmentVariable (MSBuildExtensionsPath,           frameworksPath);
			UpdateEnvironmentVariable (XBUILD_FRAMEWORK_FOLDERS_PATH,   frameworksPath);

			return !Log.HasLoggedErrors;
		}

		void UpdateEnvironmentVariable (string environmentVariable, string newPath)
		{
			var p   = (Environment.GetEnvironmentVariable (environmentVariable) ?? "")
				.Split (new [] { Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries);
			if (p.Any (x => string.Equals (x, newPath, StringComparison.OrdinalIgnoreCase))) {
				return;
			}

			var newValue    = string.Join (Path.PathSeparator.ToString (), new [] { newPath }.Concat (p));
			Log.LogMessage (MessageImportance.Low, $"  Setting environment variable `{environmentVariable}`='{newValue}'.");
			Environment.SetEnvironmentVariable (environmentVariable, newValue);
		}
	}
}


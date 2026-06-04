using System;
using System.IO;
using System.Runtime.CompilerServices;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mono.AndroidTools;
using AT = Xamarin.AndroidTools;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// This task sets static variables on Xamarin.AndroidTools.AndroidSdk and MonoDroidSdk
	/// </summary>
	public class ResolveXamarinAndroidTools : Task
	{
		public string AndroidNdkPath { get; set; }

		public string AndroidSdkPath { get; set; }

		public string JavaSdkPath { get; set; }

		[Required]
		public string MonoAndroidToolsPath { get; set; }

		[Required]
		public string MonoAndroidBinDirectory { get; set; }

		[Required]
		public string [] ReferenceAssemblyPaths { get; set; }

		const string AndroidSdkKey   = nameof (ResolveXamarinAndroidTools) + ".Xamarin.AndroidTools." + nameof (AT.AndroidSdk);
		const string MonoDroidSdkKey = nameof (ResolveXamarinAndroidTools) + ".Xamarin.AndroidTools." + nameof (AT.MonoDroidSdk);
		const RegisteredTaskObjectLifetime lifetime = RegisteredTaskObjectLifetime.AppDomain;

		static ResolveXamarinAndroidTools () => AppContext.SetSwitch (AT.AndroidSdk.AutoRefreshSwitch, false);

		public override bool Execute ()
		{
			AndroidLogger.Error += ErrorHandler;
			AndroidLogger.Warning += WarningHandler;
			AndroidLogger.Info += InfoHandler;
			AndroidLogger.Debug += DebugHandler;
			try {
				return RunTask ();
			} finally {
				AndroidLogger.Error -= ErrorHandler;
				AndroidLogger.Warning -= WarningHandler;
				AndroidLogger.Info -= InfoHandler;
				AndroidLogger.Debug -= DebugHandler;
			}
		}

		bool RunTask ()
		{
			var engine = BuildEngine4;
			var androidPaths = engine.GetRegisteredTaskObjectAssemblyLocal<AndroidSdkPaths> (AndroidSdkKey, lifetime);

			if (androidPaths == null || androidPaths.HasChanged (this)) {
				AT.AndroidSdk.Refresh (AndroidSdkPath, AndroidNdkPath, JavaSdkPath);
				if (!Log.HasLoggedErrors) {
					engine.RegisterTaskObjectAssemblyLocal (AndroidSdkKey, new AndroidSdkPaths (this), lifetime, allowEarlyCollection: false);
				}
			} else {
				Log.LogDebugMessage ("  Using cached AndroidSdk values");
			}
			Log.LogDebugMessage ("  Found AndroidSdk at {0}", AT.AndroidSdk.AndroidSdkPath);
			Log.LogDebugMessage ("  Found AndroidNdk at {0}", AT.AndroidSdk.AndroidNdkPath);
			Log.LogDebugMessage ("  Found AndroidTools at {0}", string.Join (",", AT.AndroidSdk.GetCommandLineToolsPaths ()));

			var frameworkDirectory = ReferenceAssemblyPaths [0].TrimEnd (Path.DirectorySeparatorChar);
			var monodroidPaths = engine.GetRegisteredTaskObjectAssemblyLocal<MonoDroidPaths> (MonoDroidSdkKey, lifetime);
			if (monodroidPaths == null || monodroidPaths.HasChanged (this, frameworkDirectory)) {
				AT.MonoDroidSdk.Refresh (MonoAndroidToolsPath, MonoAndroidBinDirectory, frameworkDirectory);
				if (!Log.HasLoggedErrors) {
					engine.RegisterTaskObjectAssemblyLocal (MonoDroidSdkKey, new MonoDroidPaths (this, frameworkDirectory), lifetime, allowEarlyCollection: false);
				}
			} else {
				Log.LogDebugMessage ("  Using cached MonoDroidSdk values");
			}
			Log.LogDebugMessage ("  Found RuntimePath at {0}", AT.MonoDroidSdk.RuntimePath);
			Log.LogDebugMessage ("  Found FrameworkPath at {0}", AT.MonoDroidSdk.FrameworkPath);

			return !Log.HasLoggedErrors;
		}

		class AndroidSdkPaths
		{
			public AndroidSdkPaths (ResolveXamarinAndroidTools task)
			{
				AndroidSdkPath = task.AndroidSdkPath;
				AndroidNdkPath = task.AndroidNdkPath;
				JavaSdkPath = task.JavaSdkPath;
			}

			public bool HasChanged (ResolveXamarinAndroidTools task)
			{
				return AndroidSdkPath != task.AndroidSdkPath || AndroidNdkPath != task.AndroidNdkPath || JavaSdkPath != task.JavaSdkPath;
			}

			public string AndroidSdkPath { get; private set; }
			public string AndroidNdkPath { get; private set; }
			public string JavaSdkPath { get; private set; }
		}

		class MonoDroidPaths
		{
			public MonoDroidPaths (ResolveXamarinAndroidTools task, string frameworkDirectory)
			{
				MonoAndroidToolsPath = task.MonoAndroidToolsPath;
				FrameworkDirectory = frameworkDirectory;
			}

			public bool HasChanged (ResolveXamarinAndroidTools task, string frameworkDirectory)
			{
				return MonoAndroidToolsPath != task.MonoAndroidToolsPath || FrameworkDirectory != frameworkDirectory;
			}

			public string MonoAndroidToolsPath { get; private set; }
			public string FrameworkDirectory { get; private set; }
		}

		void ErrorHandler (string task, string message)
		{
			Log.LogCodedError ("XA5300", $"{task} {message}");
		}

		void WarningHandler (string task, string message)
		{
			Log.LogCodedWarning ("XA5300", $"{task} {message}");
		}

		void DebugHandler (string task, string message)
		{
			Log.LogDebugMessage ($"DEBUG {task} {message}");
		}

		void InfoHandler (string task, string message)
		{
			Log.LogMessage ($"{task} {message}");
		}
	}
}

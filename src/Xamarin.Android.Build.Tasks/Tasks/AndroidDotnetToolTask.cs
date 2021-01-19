using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// A base class that can detect when we are running on .NET 5+
	/// to determine if we should run `foo.exe` or `dotnet foo.dll`
	/// </summary>
	public abstract class AndroidDotnetToolTask : AndroidToolTask
	{
		/// <summary>
		/// If `true`, this task should run `dotnet foo.dll` and `foo.exe` otherwise.
		/// </summary>
		protected bool UsingDotnet { get; private set; }

		/// <summary>
		/// If `true`, this task should run `mono foo.exe`.
		/// NOTE: this would only occur when building "legacy" Xamarin.Android projects under `dotnet build`.
		/// </summary>
		protected bool UsingMono { get; private set; }

		/// <summary>
		/// The path to the assembly `foo.dll`. Will be `null` when UsingDotnet and UsingMono are `false`.
		/// </summary>
		protected string AssemblyPath { get; private set; }

		public override bool Execute ()
		{
			if (string.IsNullOrEmpty (ToolExe)) {
				ToolExe = $"{BaseToolName}.exe";
			}

			var assemblyPath = Path.Combine (ToolPath, $"{BaseToolName}.dll");
			if (File.Exists (assemblyPath)) {
				UsingDotnet = true;
				AssemblyPath = assemblyPath;
				ToolPath = null;

				Log.LogDebugMessage ($"Using: dotnet {AssemblyPath}");
			} else {
				if (!RuntimeInformation.IsOSPlatform (OSPlatform.Windows) &&
						!RuntimeInformation.FrameworkDescription.StartsWith ("Mono", StringComparison.OrdinalIgnoreCase)) {
					UsingMono = true;
					AssemblyPath = Path.ChangeExtension (assemblyPath, ".exe");
					ToolPath = null;

					Log.LogDebugMessage ($"Using: mono {AssemblyPath}");
				} else {
					Log.LogDebugMessage ($"Using: {GenerateFullPathToTool ()}");
				}
			}

			return base.Execute ();
		}

		/// <summary>
		/// The base tool name, such as "generator" for `generator.exe` and `dotnet generator.dll`
		/// </summary>
		protected abstract string BaseToolName {
			get;
		}

		protected override string ToolName =>
			UsingDotnet ? "dotnet" : UsingMono ? "mono" : $"{BaseToolName}.exe";

		protected override string GenerateFullPathToTool () =>
			UsingDotnet ? "dotnet" : UsingMono ? FindMono () : Path.Combine (ToolPath, ToolExe);


		const RegisteredTaskObjectLifetime Lifetime = RegisteredTaskObjectLifetime.Build;
		const string MonoKey = nameof (AndroidDotnetToolTask) + "_Mono";

		string FindMono ()
		{
			string mono = BuildEngine4.GetRegisteredTaskObjectAssemblyLocal<string> (MonoKey, Lifetime);
			if (!string.IsNullOrEmpty (mono))
				return mono;

			var env = Environment.GetEnvironmentVariable ("PATH");
			if (string.IsNullOrEmpty (env)) {
				foreach (var path in env.Split (Path.PathSeparator)) {
					mono = Path.Combine (path, "mono");
					if (File.Exists (mono)) {
						BuildEngine4.RegisterTaskObjectAssemblyLocal (MonoKey, mono, Lifetime);
						return mono;
					}
				}
			}

			mono = "/Library/Frameworks/Mono.framework/Versions/Current/Commands/mono";
			BuildEngine4.RegisterTaskObjectAssemblyLocal (MonoKey, mono, Lifetime);
			return mono;
		}

		protected virtual CommandLineBuilder GetCommandLineBuilder ()
		{
			var cmd = new CommandLineBuilder ();
			if (!string.IsNullOrEmpty (AssemblyPath)) {
				cmd.AppendFileNameIfNotNull (AssemblyPath);
			}
			return cmd;
		}
	}
}

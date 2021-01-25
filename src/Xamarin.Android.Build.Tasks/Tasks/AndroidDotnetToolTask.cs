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
		protected bool NeedsDotnet { get; private set; }

		/// <summary>
		/// If `true`, this task should run `mono foo.exe`.
		/// NOTE: this would only occur when building "legacy" Xamarin.Android projects under `dotnet build`.
		/// </summary>
		protected bool NeedsMono { get; private set; }

		/// <summary>
		/// The path to the assembly `foo.dll`. Will be `null` when NeedsDotnet and NeedsMono are `false`.
		/// </summary>
		protected string AssemblyPath { get; private set; }

		public override bool Execute ()
		{
			if (string.IsNullOrEmpty (ToolExe)) {
				ToolExe = $"{BaseToolName}.exe";
			}

			var assemblyPath = Path.Combine (ToolPath, $"{BaseToolName}.dll");
			if (File.Exists (assemblyPath)) {
				NeedsDotnet = true;
				AssemblyPath = assemblyPath;
				ToolPath = null;

				Log.LogDebugMessage ($"Using: dotnet {AssemblyPath}");
			} else {
				if (!RuntimeInformation.IsOSPlatform (OSPlatform.Windows) &&
						!RuntimeInformation.FrameworkDescription.StartsWith ("Mono", StringComparison.OrdinalIgnoreCase)) {
					// If not Windows and not running under Mono
					NeedsMono = true;
					AssemblyPath = Path.Combine (ToolPath, $"{BaseToolName}.exe");
					ToolPath = null;

					Log.LogDebugMessage ($"Using: mono {AssemblyPath}");
				} else {
					// Otherwise running the .exe directly should work
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

		protected override string ToolName {
			get {
				if (NeedsDotnet)
					return "dotnet";
				if (NeedsMono)
					return "mono";
				return $"{BaseToolName}.exe";
			}
		}

		protected override string GenerateFullPathToTool ()
		{
			if (NeedsDotnet)
				return "dotnet";
			if (NeedsMono)
				return FindMono ();
			return Path.Combine (ToolPath, ToolExe);
		}


		const RegisteredTaskObjectLifetime Lifetime = RegisteredTaskObjectLifetime.Build;
		const string MonoKey = nameof (AndroidDotnetToolTask) + "_Mono";
		static readonly string [] KnownMonoPaths = new [] {
			"/Library/Frameworks/Mono.framework/Versions/Current/Commands/mono",
			"/usr/bin/mono",
		};

		string FindMono ()
		{
			string mono = BuildEngine4.GetRegisteredTaskObjectAssemblyLocal<string> (MonoKey, Lifetime);
			if (!string.IsNullOrEmpty (mono)) {
				Log.LogDebugMessage ($"Found cached mono via {nameof (BuildEngine4.RegisterTaskObject)}");
				return mono;
			}

			var env = Environment.GetEnvironmentVariable ("PATH");
			if (string.IsNullOrEmpty (env)) {
				foreach (var path in env.Split (Path.PathSeparator)) {
					if (File.Exists (mono = Path.Combine (path, "mono"))) {
						Log.LogDebugMessage ("Found mono in $PATH");
						BuildEngine4.RegisterTaskObjectAssemblyLocal (MonoKey, mono, Lifetime);
						return mono;
					}
				}
			}

			foreach (var path in KnownMonoPaths) {
				if (File.Exists (mono = path)) {
					Log.LogDebugMessage ($"Found mono in {nameof (KnownMonoPaths)}");
					BuildEngine4.RegisterTaskObjectAssemblyLocal (MonoKey, mono, Lifetime);
					return mono;
				}
			}

			// Last resort
			BuildEngine4.RegisterTaskObjectAssemblyLocal (MonoKey, mono = "mono", Lifetime);
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

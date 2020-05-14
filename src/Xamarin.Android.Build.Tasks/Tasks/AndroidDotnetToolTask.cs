using System.IO;
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
		/// The path to the assembly `foo.dll`. Will be `null` when not running on .NET 5+.
		/// </summary>
		protected string AssemblyPath { get; private set; }

		public override bool Execute ()
		{
			if (string.IsNullOrEmpty (ToolExe)) {
				ToolExe = $"{BaseToolName}.exe";
			}

			var assemblyPath = Path.Combine (ToolPath, $"{BaseToolName}.dll");
			if (File.Exists (assemblyPath)) {
				Log.LogDebugMessage ($"Using: dotnet {assemblyPath}");
				UsingDotnet = true;
				AssemblyPath = assemblyPath;
				ToolPath = null;
			} else {
				Log.LogDebugMessage ($"Using: {GenerateFullPathToTool ()}");
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
			UsingDotnet ? "dotnet" : $"{BaseToolName}.exe";

		protected override string GenerateFullPathToTool () =>
			UsingDotnet ? "dotnet" : Path.Combine (ToolPath, ToolExe);

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

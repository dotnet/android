using System;
using System.IO;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.BuildTools.PrepTasks
{
	public abstract class PathToolTask : ToolTask
	{
		static readonly string[] FileExtensions;

		static PathToolTask ()
		{
			var pathExt = Environment.GetEnvironmentVariable ("PATHEXT");
			var pathExts = pathExt?.Split (new char [] { Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries);
			int length = pathExts?.Length ?? 0;
			FileExtensions = new string [length * 2 + 1];
			for (int i = 0, j = 0; i < length; i++, j += 2) {
				FileExtensions [j] = pathExts [i].ToLowerInvariant ();
				FileExtensions [j + 1] = pathExts [i];
			}
			FileExtensions [FileExtensions.Length - 1] = null;
		}

		protected   abstract    string          ToolBaseName { get; }

		public      override    string          ToolExe {
			get { return base.ToolExe; }
			set { if (value != ToolBaseName) base.ToolExe = value; }
		}

		protected   override    string          ToolName {
			get {
				var dirs = string.IsNullOrEmpty (ToolPath)
					? null
					: new [] { ToolPath };
				string filename;
				GetProgramLocation (ToolBaseName, out filename, dirs);
				return filename;
			}
		}

		protected override string GenerateFullPathToTool ()
		{
			var dirs = string.IsNullOrEmpty (ToolPath)
				? null
				: new [] { ToolPath };
			string filename;
			var path    = GetProgramLocation (ToolBaseName, out filename, dirs);
			return path;
		}

		public static string GetProgramLocation (string programBasename, out string filename, string[] directories = null)
		{
			directories = directories ?? Environment.GetEnvironmentVariable ("PATH").Split (Path.PathSeparator);
			foreach (var directory in directories) {
				foreach (var extension in FileExtensions) {
					filename = Path.ChangeExtension (programBasename, extension);
					var path = Path.Combine (directory, filename);
					if (File.Exists (path))
						return path;
				}
			}
			filename = programBasename;
			return null;
		}

		protected void AddEnvironmentVariables (string[] variables)
		{
			if (EnvironmentVariables == null) {
				EnvironmentVariables = variables;
				return;
			}
			if (variables == null)
				return;
			var newVariables = new string [checked(EnvironmentVariables.Length + variables.Length)];
			Array.Copy (EnvironmentVariables, newVariables, EnvironmentVariables.Length);
			Array.Copy (variables, 0, newVariables, EnvironmentVariables.Length, variables.Length);
			EnvironmentVariables = newVariables;
		}
	}
}


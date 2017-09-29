using System;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.BuildTools.PrepTasks
{
	public abstract class PathToolTask : ToolTask
	{
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
				Which.GetProgramLocation (ToolBaseName, out filename, dirs);
				return filename;
			}
		}

		protected override string GenerateFullPathToTool ()
		{
			var dirs = string.IsNullOrEmpty (ToolPath)
				? null
				: new [] { ToolPath };
			string filename;
			var path    = Which.GetProgramLocation (ToolBaseName, out filename, dirs);
			return path;
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


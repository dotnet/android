using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Diagnostics;
using Microsoft.Build.Framework;
using System.Xml.Linq;
using Xamarin.Android.Tools;

namespace Xamarin.ProjectTools
{
	/// <summary>
	/// Provides functionality for building Xamarin test projects using MSBuild.
	/// This class manages the project directory, handles project file generation,
	/// and orchestrates the build process for testing scenarios.
	/// </summary>
	/// <remarks>
	/// ProjectBuilder extends <see cref="Builder"/> to provide project-specific build capabilities.
	/// It handles project file creation, NuGet package restoration, and incremental builds.
	/// </remarks>
	/// <seealso cref="Builder"/>
	/// <seealso cref="XamarinProject"/>
	/// <seealso cref="BuildOutput"/>
	public class ProjectBuilder : Builder
	{
		/// <summary>
		/// Initializes a new instance of the ProjectBuilder class for the specified project directory.
		/// </summary>
		/// <param name="projectDirectory">The directory where the project will be created and built.</param>
		public ProjectBuilder (string projectDirectory)
		{
			ProjectDirectory = projectDirectory;
			Target = "Build";
			ThrowOnBuildFailure = true;
			BuildLogFile = "build.log";
		}

		/// <summary>
		/// Gets or sets a value indicating whether the project directory should be cleaned up when the builder is disposed.
		/// </summary>
		public bool CleanupOnDispose { get; set; }
		
		/// <summary>
		/// Gets or sets a value indicating whether the project directory should be cleaned up after a successful build.
		/// </summary>
		public bool CleanupAfterSuccessfulBuild { get; set; }
		
		/// <summary>
		/// Gets or sets the directory where the project files are located.
		/// </summary>
		public string ProjectDirectory { get; set; }
		
		/// <summary>
		/// Gets or sets the MSBuild target to execute (default: "Build").
		/// </summary>
		public string Target { get; set; }

		/// <summary>
		/// Gets or sets the logger for build operations.
		/// </summary>
		/// <summary>
		/// Gets or sets the logger for build operations.
		/// </summary>
		public ILogger Logger { get; set; }

		/// <summary>
		/// Disposes of the ProjectBuilder and optionally cleans up the project directory.
		/// </summary>
		/// <param name="disposing">True if disposing managed resources.</param>
		protected override void Dispose (bool disposing)
		{
			if (disposing)
				if (CleanupOnDispose)
					Cleanup ();
		}

		bool built_before;
		bool last_build_result;

		/// <summary>
		/// Gets the build output from the last build operation.
		/// </summary>
		/// <seealso cref="BuildOutput"/>
		public BuildOutput Output { get; private set; }

		/// <summary>
		/// Saves the project files to the project directory.
		/// On the first call, populates a new directory; on subsequent calls, updates existing files.
		/// </summary>
		/// <param name="project">The project to save.</param>
		/// <param name="doNotCleanupOnUpdate">If true, existing files not in the project will not be deleted during updates.</param>
		/// <param name="saveProject">If true, the project file itself will be saved.</param>
		/// <seealso cref="XamarinProject.Save(bool)"/>
		/// <seealso cref="XamarinProject.Populate(string, IEnumerable{ProjectResource})"/>
		/// <seealso cref="XamarinProject.UpdateProjectFiles(string, IEnumerable{ProjectResource}, bool)"/>
		public void Save (XamarinProject project, bool doNotCleanupOnUpdate = false, bool saveProject = true)
		{
			var files = project.Save (saveProject);

			if (!built_before) {
				if (project.ShouldPopulate) {
					if (Directory.Exists (ProjectDirectory)) {
						FileSystemUtils.SetDirectoryWriteable (ProjectDirectory);
						Directory.Delete (ProjectDirectory, true);
					}
					project.Populate (ProjectDirectory, files);
				}

				project.CopyNuGetConfig (ProjectDirectory);
			}
			else
				project.UpdateProjectFiles (ProjectDirectory, files, doNotCleanupOnUpdate);
		}

		/// <summary>
		/// Builds the specified project using MSBuild.
		/// </summary>
		/// <param name="project">The project to build.</param>
		/// <param name="doNotCleanupOnUpdate">If true, existing files not in the project will not be deleted during updates.</param>
		/// <param name="parameters">Optional MSBuild parameters to pass to the build.</param>
		/// <param name="saveProject">If true, the project file itself will be saved before building.</param>
		/// <param name="environmentVariables">Optional environment variables to set during the build.</param>
		/// <returns>True if the build succeeded; otherwise, false.</returns>
		/// <seealso cref="Save(XamarinProject, bool, bool)"/>
		/// <seealso cref="Output"/>
		public bool Build (XamarinProject project, bool doNotCleanupOnUpdate = false, string [] parameters = null, bool saveProject = true, Dictionary<string, string> environmentVariables = null)
		{
			Save (project, doNotCleanupOnUpdate, saveProject);

			Output = project.CreateBuildOutput (this);

			bool result = BuildInternal (Path.Combine (ProjectDirectory, project.ProjectFilePath), Target, parameters, environmentVariables, restore: project.ShouldRestorePackageReferences, binlogName: Path.GetFileNameWithoutExtension (BuildLogFile));
			built_before = true;

			if (CleanupAfterSuccessfulBuild)
				Cleanup ();
			last_build_result = result;
			return result;
		}

		public bool Install (XamarinProject project, bool doNotCleanupOnUpdate = false, string [] parameters = null, bool saveProject = true)
		{
			//NOTE: since $(BuildingInsideVisualStudio) is set, Build will not happen by default
			return RunTarget (project, "Build,Install", doNotCleanupOnUpdate, parameters, saveProject: saveProject);
		}

		public bool Uninstall (XamarinProject project, bool doNotCleanupOnUpdate = false, bool saveProject = true)
		{
			return RunTarget (project, "Uninstall", doNotCleanupOnUpdate);
		}

		public bool Restore (XamarinProject project, bool doNotCleanupOnUpdate = false, string [] parameters = null)
		{
			return RunTarget (project, "Restore", doNotCleanupOnUpdate, parameters);
		}

		public bool Clean (XamarinProject project, bool doNotCleanupOnUpdate = false)
		{
			return RunTarget (project, "Clean", doNotCleanupOnUpdate);
		}

		public bool UpdateAndroidResources (XamarinProject project, bool doNotCleanupOnUpdate = false, string [] parameters = null, Dictionary<string, string> environmentVariables = null)
		{
			return RunTarget (project, "UpdateAndroidResources", doNotCleanupOnUpdate, parameters, environmentVariables);
		}

		public bool DesignTimeBuild (XamarinProject project, string target = "Compile", bool doNotCleanupOnUpdate = false, string [] parameters = null)
		{
			if (parameters == null) {
				return RunTarget (project, target, doNotCleanupOnUpdate, parameters: new string [] { "DesignTimeBuild=True", "SkipCompilerExecution=true" });
			} else {
				var designTimeParameters = new string [parameters.Length + 2];
				parameters.CopyTo (designTimeParameters, 0);
				designTimeParameters [designTimeParameters.Length - 2] = "DesignTimeBuild=True";
				designTimeParameters [designTimeParameters.Length - 1] = "SkipCompilerExecution=true";
				return RunTarget (project, target, doNotCleanupOnUpdate, parameters: designTimeParameters);
			}
		}

		public bool RunTarget (XamarinProject project, string target, bool doNotCleanupOnUpdate = false, string [] parameters = null, Dictionary<string, string> environmentVariables = null, bool saveProject = true)
		{
			var oldTarget = Target;
			Target = target;
			try {
				return Build (project, doNotCleanupOnUpdate: doNotCleanupOnUpdate, parameters: parameters, saveProject: saveProject, environmentVariables: environmentVariables);
			} finally {
				Target = oldTarget;
			}
		}

		public void Cleanup ()
		{
			// don't clean up if we failed so we can get the
			//logs
			if (!last_build_result)
				return;
			built_before = false;

			var projectDirectory = Path.Combine (XABuildPaths.TestOutputDirectory, ProjectDirectory);
			if (Directory.Exists (projectDirectory)) {
				FileSystemUtils.SetDirectoryWriteable (projectDirectory);
				Directory.Delete (projectDirectory, true);
			}
		}

		public struct RuntimeInfo
		{
			public string Name;
			public string Runtime;
			public string Abi;
			public int Size;
		}

		public RuntimeInfo [] GetSupportedRuntimes ()
		{
			var runtimeInfo = new List<RuntimeInfo> ();
			var runtimeDirs = new HashSet<string> ();
			var rootRuntimeDirs = Directory.GetDirectories (TestEnvironment.DotNetPreviewPacksDirectory, $"Microsoft.Android.Runtime.{XABuildConfig.AndroidDefaultTargetDotnetApiLevel}.*");
			foreach (var dir in rootRuntimeDirs) {
				runtimeDirs.Add (Directory.GetDirectories (dir).LastOrDefault ());
			}

			foreach (var runtimeDir in runtimeDirs) {
				foreach (var file in Directory.EnumerateFiles (runtimeDir, "libmono-android.*.so", SearchOption.AllDirectories)) {
					string fullFilePath = Path.GetFullPath (file);
					DirectoryInfo parentDir = Directory.GetParent (fullFilePath);
					if (parentDir == null)
						continue;
					string[] items = Path.GetFileName (fullFilePath).Split ('.' );
					if (items.Length != 3)
						continue;
					var fi = new FileInfo (fullFilePath);
					runtimeInfo.Add (new RuntimeInfo () {
						Name = "libmonodroid.so",
						Runtime = items [1], // release|debug
						Abi = parentDir.Name, // armaebi|x86|arm64-v8a
						Size = (int)fi.Length, // int
					});
				}
			}
			return runtimeInfo.ToArray ();
		}
	}
}

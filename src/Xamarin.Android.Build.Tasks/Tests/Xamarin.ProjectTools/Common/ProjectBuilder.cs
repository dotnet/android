﻿﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using Microsoft.Build.Framework;

using XABuildPaths = Xamarin.Android.Build.Paths;

namespace Xamarin.ProjectTools
{
	public class ProjectBuilder : Builder
	{
		public ProjectBuilder (string projectDirectory)
		{
			ProjectDirectory = projectDirectory;
			Verbosity = LoggerVerbosity.Normal;
			Target = "Build";
			ThrowOnBuildFailure = true;
			BuildLogFile = "build.log";
		}

		public bool CleanupOnDispose { get; set; }
		public bool CleanupAfterSuccessfulBuild { get; set; }
		public string PackagesDirectory { get; set; }
		public string ProjectDirectory { get; set; }
		public string Target { get; set; }

		public ILogger Logger { get; set; }

		protected override void Dispose (bool disposing)
		{
			if (disposing)
				if (CleanupOnDispose)
					Cleanup ();
		}

		bool built_before;
		bool last_build_result;

		public BuildOutput Output { get; private set; }

		public void Save (XamarinProject project, bool doNotCleanupOnUpdate = false, bool saveProject = true)
		{
			var files = project.Save (saveProject);

			if (!built_before) {
				if (Directory.Exists (ProjectDirectory)) {
					FileSystemUtils.SetDirectoryWriteable (ProjectDirectory);
					Directory.Delete (ProjectDirectory, true);
				}
				if (Directory.Exists (PackagesDirectory))
					Directory.Delete (PackagesDirectory, true);
				project.Populate (ProjectDirectory, files);
			}
			else
				project.UpdateProjectFiles (ProjectDirectory, files, doNotCleanupOnUpdate);
		}

		public bool Build (XamarinProject project, bool doNotCleanupOnUpdate = false, string [] parameters = null, bool saveProject = true, Dictionary<string, string> environmentVariables = null)
		{
			Save (project, doNotCleanupOnUpdate, saveProject);

			Output = project.CreateBuildOutput (this);

			project.NuGetRestore (Path.Combine (XABuildPaths.TestOutputDirectory, ProjectDirectory), PackagesDirectory);

			bool result = BuildInternal (Path.Combine (ProjectDirectory, project.ProjectFilePath), Target, parameters, environmentVariables);
			built_before = true;

			if (CleanupAfterSuccessfulBuild)
				Cleanup ();
			last_build_result = result;
			return result;
		}

		public bool Clean (XamarinProject project, bool doNotCleanupOnUpdate = false)
		{
			var oldTarget = Target;
			Target = "Clean";
			try {
				return Build (project, doNotCleanupOnUpdate);
			}
			finally {
				Target = oldTarget;
			}
		}

		public bool UpdateAndroidResources (XamarinProject project, bool doNotCleanupOnUpdate = false, string [] parameters = null, Dictionary<string, string> environmentVariables = null)
		{
			var oldTarget = Target;
			Target = "UpdateAndroidResources";
			try {
				return Build (project, doNotCleanupOnUpdate: doNotCleanupOnUpdate, parameters: parameters, environmentVariables: environmentVariables);
			}
			finally {
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
			var outdir = FrameworkLibDirectory;
			var path = Path.Combine (outdir, "xbuild", "Xamarin", "Android", "lib");
			if (!Directory.Exists (path)) {
				path = outdir;
			}
			foreach (var file in Directory.EnumerateFiles (path, "libmono-android.*.so", SearchOption.AllDirectories)) {
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
			return runtimeInfo.ToArray ();
		}
	}
}

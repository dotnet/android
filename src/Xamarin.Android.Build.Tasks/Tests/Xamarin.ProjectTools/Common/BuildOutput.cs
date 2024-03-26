using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xamarin.Tools.Zip;

namespace Xamarin.ProjectTools
{
	public class BuildOutput
	{
		internal BuildOutput (XamarinProject project)
		{
			Project = project;
		}

		public ProjectBuilder Builder { get; set; }

		public XamarinProject Project { get; private set; }

		public string GetPropertyInApplicableConfiguration (string name)
		{
			return Project.GetProperty (Project.IsRelease ? Project.ReleaseProperties : Project.DebugProperties, name) ?? Project.GetProperty (name);
		}

		public string OutputPath {
			get { return GetPropertyInApplicableConfiguration (KnownProperties.OutputPath); }
		}

		public string IntermediateOutputPath {
			get { return GetPropertyInApplicableConfiguration (KnownProperties.IntermediateOutputPath) ?? "obj" + OutputPath.Substring (3); } // obj/{Config}
		}

		public string GetIntermediaryPath (string file)
		{
			return Path.Combine (Project.Root, Builder.ProjectDirectory, IntermediateOutputPath, file.Replace ('/', Path.DirectorySeparatorChar));
		}

		public string GetIntermediaryAsText (string root, string file)
		{
			return File.ReadAllText (GetIntermediaryPath (file));
		}

		public string GetIntermediaryAsText (string file)
		{
			return File.ReadAllText (GetIntermediaryPath (file));
		}

		public List<string> GetAssemblyMapCache ()
		{
			var path = GetIntermediaryPath (Path.Combine ("lp", "map.cache"));
			return File.ReadLines (path).ToList ();
		}

		public bool IsTargetSkipped (string target, bool defaultIfNotUsed = false) => IsTargetSkipped (Builder.LastBuildOutput, target, defaultIfNotUsed);

		public static bool IsTargetSkipped (IEnumerable<string> output, string target, bool defaultIfNotUsed = false)
		{
			bool found = false;
			foreach (var line in output) {
					if (line.Contains ($"Building target \"{target}\" completely."))
						return false;
					found = line.Contains ($"Target {target} skipped due to ")
					            || line.Contains ($"Skipping target \"{target}\" because it has no ") //NOTE: message can say `inputs` or `outputs`
					            || line.Contains ($"Target \"{target}\" skipped, due to")
					            || line.Contains ($"Skipping target \"{target}\" because its outputs are up-to-date")
					            || line.Contains ($"target {target}, skipping")
					            || line.Contains ($"Skipping target \"{target}\" because all output files are up-to-date");
					if (found)
						return true;
			}
			return defaultIfNotUsed;
		}

		/// <summary>
		/// Looks for: Building target "Foo" partially, because some output files are out of date with respect to their input files.
		/// </summary>
		public bool IsTargetPartiallyBuilt (string target)
		{
			foreach (var line in Builder.LastBuildOutput) {
				if (line.Contains ($"Building target \"{target}\" partially")) {
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Extracts the timing (in milliseconds) from the Performance Summary.
		/// </summary>
		/// <param name="targetOrTask">The Target or Task to get the timing for.</param>
		/// <returns>The time it took as a TimeSpan. Or Zero if the data could not be found.</returns>
		public TimeSpan GetTargetOrTaskTime (string targetOrTask)
		{
			Regex regex = new Regex (@"\s+(?<time>\d+)\s+(ms)\s+(" + targetOrTask + @")\s+(?<calls>\d+)\scalls",
				RegexOptions.Compiled | RegexOptions.ExplicitCapture);
			foreach (var line in Builder.LastBuildOutput) {
				var match = regex.Match (line);
				if (match.Success) {
					if (TimeSpan.TryParse ($"0:0:0.{match.Groups ["time"].Value}", out TimeSpan result))
						return result;
				}
			}
			return TimeSpan.Zero;
		}

		public bool IsApkInstalled {
			get {
				foreach (var line in Builder.LastBuildOutput) {
					if (line.Contains ("Installed Package") || line.Contains (" pm install "))
						return true;
				}
				return false;
			}
		}

		public bool AreTargetsAllSkipped (params string [] targets)
		{
			return targets.All (t => IsTargetSkipped (t));
		}

		public bool AreTargetsAllBuilt (params string [] targets)
		{
			return targets.All (t => !IsTargetSkipped (t));
		}
	}

	public class AndroidApplicationBuildOutput : BuildOutput
	{
		internal AndroidApplicationBuildOutput (XamarinProject project)
			: base (project)
		{
		}

		public new XamarinAndroidApplicationProject Project {
			get { return (XamarinAndroidApplicationProject) base.Project; }
		}

		public string ApkFile {
			// If we could know package name, this can be simpler and much less hackier...
			get { return Directory.GetFiles (Path.Combine (GetIntermediaryPath ("android"), "bin"), "*.apk").First (); }
		}

		public OutputApk OpenApk ()
		{
			return new OutputApk (ZipHelper.OpenZip (ApkFile));
		}
	}

	public class OutputApk : IDisposable
	{
		ZipArchive apk;

		internal OutputApk (ZipArchive apk)
		{
			this.apk = apk;
		}

		public void Dispose ()
		{
			apk.Dispose ();
		}

		ZipEntry GetEntry (string file)
		{
			return apk.First (e => e.FullName == file);
		}

		public bool Exists (string file)
		{
			return apk.Any (e => e.FullName == file);
		}

		public string GetText (string file)
		{
			using (var ms = new MemoryStream ()) {
				GetEntry (file).Extract (ms);
				ms.Position = 0;
				using (var sr = new StreamReader (ms))
					return sr.ReadToEnd ();
			}
		}

		public byte [] GetRaw (string file)
		{
			var e = GetEntry (file);
			using (var ms = new MemoryStream ()) {
				e.Extract (ms);
				return ms.ToArray ();
			}
		}
	}
}

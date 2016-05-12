using System;
using System.IO;
using System.Linq;
using Ionic.Zip;

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
			return Path.Combine (Builder.ProjectDirectory, IntermediateOutputPath, file.Replace ('/', Path.DirectorySeparatorChar));
		}
		
		public string GetIntermediaryAsText (string file)
		{
			return File.ReadAllText (GetIntermediaryPath (file));
		}

		public bool IsTargetSkipped (string target)
		{
			if (!Builder.LastBuildOutput.Contains (target))
				throw new ArgumentException (string.Format ("Target '{0}' is not even in the build output.", target));
			return Builder.LastBuildOutput.Contains (string.Format ("Target {0} skipped due to ", target))
				      || Builder.LastBuildOutput.Contains (string.Format ("Skipping target \"{0}\" because its outputs are up-to-date", target));
		}

		public bool IsApkInstalled {
			get { return Builder.LastBuildOutput.Contains (" pm install "); }
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
		ZipFile apk;
		
		internal OutputApk (ZipFile apk)
		{
			this.apk = apk;
		}
		
		public void Dispose ()
		{
			apk.Dispose ();
		}
		
		ZipEntry GetEntry (string file)
		{
			return apk.Entries.First (e => e.FileName == file);
		}
		
		public bool Exists (string file)
		{
			return apk.Entries.Any (e => e.FileName == file);
		}
		
		public string GetText (string file)
		{
			using (var sr = new StreamReader (GetEntry (file).OpenReader ()))
				return sr.ReadToEnd ();
		}
		
		public byte [] GetRaw (string file)
		{
			var e = GetEntry (file);
			byte [] ret = new byte [e.UncompressedSize];
			using (var r = e.OpenReader ())
				r.Read (ret, 0, ret.Length);
			return ret;
		}
	}
}

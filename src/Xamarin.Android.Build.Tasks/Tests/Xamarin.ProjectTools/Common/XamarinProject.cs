using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.Build.Construction;
using System.Diagnostics;
using System.Text;
using System.Xml.Linq;

namespace Xamarin.ProjectTools
{
	public abstract class XamarinProject 
	{
		string debugConfigurationName;
		string releaseConfigurationName;

		public virtual ProjectLanguage Language { get; set; }

		public string ProjectName { get; set; }
		public string ProjectGuid { get; set; }
		public string AssemblyName { get; set; }
		public abstract string ProjectTypeGuid { get; }

		public IList<Property> DebugProperties { get; private set; }
		public IList<Property> ReleaseProperties { get; private set; }
		public IList<Property> CommonProperties { get; private set; }
		public IList<IList<BuildItem>> ItemGroupList { get; private set; }
		public IList<PropertyGroup> PropertyGroups { get; private set; }
		public IList<Package> Packages { get; private set; }
		public IList<BuildItem> References { get; private set; }
		public IList<Package> PackageReferences { get; private set; }
		public string GlobalPackagesFolder { get; set; } = Path.Combine (XABuildPaths.TopDirectory, "packages");
		public IList<string> ExtraNuGetConfigSources { get; set; }

		public virtual bool ShouldRestorePackageReferences => PackageReferences?.Count > 0;
		/// <summary>
		/// If true, the ProjectDirectory will be deleted and populated on the first build
		/// </summary>
		public virtual bool ShouldPopulate => true;
		public IList<Import> Imports { get; private set; }
		PropertyGroup common, debug, release;

		public bool IsRelease {
			get { return GetProperty (KnownProperties.Configuration) == releaseConfigurationName; }
			set { SetProperty ("Configuration", value ? releaseConfigurationName : debugConfigurationName); }
		}

		public XamarinProject (string debugConfigurationName = "Debug", string releaseConfigurationName = "Release")
		{
			this.debugConfigurationName = debugConfigurationName;
			this.releaseConfigurationName = releaseConfigurationName;

			References = new List<BuildItem> ();
			PackageReferences = new List<Package> ();
			ItemGroupList = new List<IList<BuildItem>> ();
			PropertyGroups = new List<PropertyGroup> ();
			CommonProperties = new List<Property> ();
			common = new PropertyGroup (null, CommonProperties);
			DebugProperties = new List<Property> ();
			debug = new PropertyGroup ($"'$(Configuration)|$(Platform)' == '{debugConfigurationName}|AnyCPU'", DebugProperties);
			ReleaseProperties = new List<Property> ();
			release = new PropertyGroup ($"'$(Configuration)|$(Platform)' == '{releaseConfigurationName}|AnyCPU'", ReleaseProperties);

			PropertyGroups.Add (common);
			PropertyGroups.Add (debug);
			PropertyGroups.Add (release);

			Packages = new List<Package> ();
			Imports = new List<Import> ();

		}

		public string GetProperty (string name)
		{
			return GetProperty (CommonProperties, name);
		}

		public string GetProperty (IList<Property> group, string name)
		{
			var prop = group.FirstOrDefault (p => p.Name.Equals (name, StringComparison.OrdinalIgnoreCase));
			return prop != null ? prop.Value () : null;
		}

		public void SetProperty (string name, string value, string condition = null)
		{
			SetProperty (name, () => value, condition);
		}

		public void SetProperty (IList<Property> group, string name, bool value, string condition = null)
		{
			SetProperty (group, name, () => value.ToString (), condition);
		}

		public void SetProperty (IList<Property> group, string name, string value, string condition = null)
		{
			SetProperty (group, name, () => value, condition);
		}

		public bool RemoveProperty (string name)
		{
			return RemoveProperty (CommonProperties, name);
		}

		public bool RemoveProperty (IList<Property> group, string name)
		{
			var prop = group.FirstOrDefault (p => p.Name.Equals (name, StringComparison.OrdinalIgnoreCase));
			if (prop == null)
				return false;
			group.Remove (prop);
			return true;
		}

		public void SetProperty (string name, Func<string> value, string condition = null)
		{
			SetProperty (CommonProperties, name, value);
		}

		public void SetProperty (IList<Property> group, string name, Func<string> value, string condition = null)
		{
			var prop = group.FirstOrDefault (p => p.Name.Equals (name, StringComparison.OrdinalIgnoreCase));
			if (prop == null)
				group.Add (new Property (condition, name, value));
			else {
				prop.Condition = condition;
				prop.Value = value;
			}
		}

		public BuildItem GetItem (string include)
		{
			return ItemGroupList.SelectMany (g => g).FirstOrDefault (i => i.Include ().Equals (include, StringComparison.OrdinalIgnoreCase));
		}

		public Import GetImport (string include)
		{
			return Imports.FirstOrDefault (i => i.Project ().Equals (include, StringComparison.OrdinalIgnoreCase));
		}

		public void Touch (params string [] itemPaths)
		{
			foreach (var item in itemPaths) {
				var buildItem = GetItem (item);
				if (buildItem != null) {
					buildItem.Timestamp = DateTime.UtcNow;
					continue;
				}
				var import = GetImport (item);
				if (import != null) {
					import.Timestamp = DateTime.UtcNow;
					continue;
				}
				throw new InvalidOperationException ($"Path `{item}` not found in project!");
			}
		}

		string project_file_path;
		public string ProjectFilePath {
			get { return project_file_path ?? ProjectName + Language.DefaultProjectExtension; }
			set { project_file_path = value; }
		}

		public string AssemblyInfo { get; set; }
		public string RootNamespace { get; set; }

		public virtual string SaveProject ()
		{
			return string.Empty;
		}

		public virtual BuildOutput CreateBuildOutput (ProjectBuilder builder)
		{
			return new BuildOutput (this) { Builder = builder };
		}

		ProjectResource project;
		string packages_config_contents;

		public virtual List<ProjectResource> Save (bool saveProject = true)
		{
			var list = new List<ProjectResource> ();
			if (saveProject) {
				if (project == null) {
					project = new ProjectResource {
						Path = ProjectFilePath,
						Encoding = System.Text.Encoding.UTF8,
					};
				}
				// Clear the Timestamp if the project changed
				var contents = SaveProject ();
				if (contents != project.Content) {
					project.Timestamp = null;
					project.Content = contents;
				}
				list.Add (project);
			}

			if (Packages.Any ()) {
				var contents = "<packages>\n" + string.Concat (Packages.Select (p => string.Format ("  <package id='{0}' version='{1}' targetFramework='{2}' />\n",
					p.Id, p.Version, p.TargetFramework))) + "</packages>";
				var timestamp = contents != packages_config_contents ? default (DateTimeOffset?) : DateTimeOffset.MinValue;
				list.Add (new ProjectResource () {
					Timestamp = timestamp,
					Path = "packages.config",
					Content = packages_config_contents = contents,
				});
			} else {
				packages_config_contents = null;
			}

			foreach (var ig in ItemGroupList)
				list.AddRange (ig.Select (s => new ProjectResource () {
					Timestamp = s.Timestamp,
					Path = s.Include?.Invoke (),
					Content = s.TextContent == null ? null : s.TextContent (),
					BinaryContent = s.BinaryContent == null ? null : s.BinaryContent (),
					Encoding = s.Encoding,
					Deleted = s.Deleted,
					Attributes = s.Attributes,
				}));

			foreach (var import in Imports)
				list.Add (new ProjectResource () {
					Timestamp = import.Timestamp,
					Path = import.Project (),
					Content = import.TextContent == null ? null : import.TextContent (),
					Encoding = System.Text.Encoding.UTF8,
				});

			return list;
		}

		public void Populate (string directory)
		{
			Populate (directory, Save ());
		}

		public string Root {
			get {
				return XABuildPaths.TestOutputDirectory;
			}
		}

		public void Populate (string directory, IEnumerable<ProjectResource> projectFiles)
		{
			directory = directory.Replace ('\\', '/').Replace ('/', Path.DirectorySeparatorChar);

			if (File.Exists (directory) || Directory.Exists (directory))
				throw new InvalidOperationException ("Path '" + directory + "' already exists. Cannot create a project at this state.");

			Directory.CreateDirectory (Path.Combine (Root, directory));

			UpdateProjectFiles (directory, projectFiles);
		}

		public virtual void UpdateProjectFiles (string directory, IEnumerable<ProjectResource> projectFiles, bool doNotCleanup = false)
		{
			directory = Path.Combine (Root, directory.Replace ('\\', '/').Replace ('/', Path.DirectorySeparatorChar));

			if (!Directory.Exists (directory))
				throw new InvalidOperationException ("Path '" + directory + "' does not exist.");

			foreach (var p in projectFiles) {
				// Skip empty paths or wildcards
				if (string.IsNullOrEmpty (p.Path) || p.Path.Contains ("*"))
					continue;
				var path = Path.Combine (directory, p.Path.Replace ('\\', '/').Replace ('/', Path.DirectorySeparatorChar));

				if (p.Deleted) {
					if (File.Exists (path))
						File.Delete (path);
					continue;
				}
				string filedir = directory;
				if (path.Contains (Path.DirectorySeparatorChar)) {
					filedir = Path.GetDirectoryName (path);
					if (!Directory.Exists (filedir) && (p.Content != null || p.BinaryContent != null))
						Directory.CreateDirectory (filedir);
				}

				// LastWriteTime is inaccurate without milliseconds, but neither mono FileSystemInfo nor UnixFileSystemInfo (in Mono.Posix)
				// handles this precisely. And that causes comparison problem.
				// To avoid this issue, we compare time after removing milliseconds field here.
				//
				// Mono.Posix after mono 367d417 will handle file time precisely.
				// 
				/*
				// The code below debug prints time comparison. Current code could still result in unwanted comparison,
				// so in case of doubtful results, use this to get comparison results.
				if (File.Exists (path) && p.Timestamp != null) {
					var ft = new DateTimeOffset (new FileInfo (path).LastWriteTime);
					string cmp = string.Format ("{0} {1} {2}", p.Timestamp.Value.TimeOfDay.TotalMilliseconds, ft.TimeOfDay.TotalMilliseconds, p.Timestamp.Value > ft);
					Console.Error.WriteLine (cmp);
				}
				*/
				var needsUpdate = (!File.Exists (path) || p.Timestamp == null || p.Timestamp.Value > new DateTimeOffset (new FileInfo (path).LastWriteTimeUtc));
				if (p.Content != null && needsUpdate) {
					if (File.Exists (path)) File.SetAttributes (path, FileAttributes.Normal);
					File.WriteAllText (path, p.Content, p.Encoding ?? Encoding.UTF8);
					File.SetLastWriteTimeUtc (path, p.Timestamp != null ? p.Timestamp.Value.UtcDateTime : DateTime.UtcNow);
					File.SetAttributes (path, p.Attributes);
					p.Timestamp = new DateTimeOffset (new FileInfo (path).LastWriteTimeUtc);
				} else if (p.BinaryContent != null && needsUpdate) {
					using (var f = File.Create (path))
						f.Write (p.BinaryContent, 0, p.BinaryContent.Length);
					File.SetLastWriteTimeUtc (path, p.Timestamp != null ? p.Timestamp.Value.UtcDateTime : DateTime.UtcNow);
					File.SetAttributes (path, p.Attributes);
					p.Timestamp = new DateTimeOffset (new FileInfo (path).LastWriteTimeUtc);
				}
			}

			// finally clean up files that do not exist in project anymore.
			if (!doNotCleanup) {
				var dirFullPath = Path.GetFullPath (directory) + '/';
				foreach (var fi in new DirectoryInfo (directory).GetFiles ("*", SearchOption.AllDirectories)) {
					var subname = fi.FullName.Substring (dirFullPath.Length).Replace ('\\', '/');
					if (subname.StartsWith ("bin", StringComparison.OrdinalIgnoreCase) || subname.StartsWith ("obj", StringComparison.OrdinalIgnoreCase))
						continue;
					if (!projectFiles.Any (p => p.Path.Replace ('\\', '/').Equals (subname))) {
						fi.Delete ();
					}
				}
			}

		}

		public virtual void NuGetRestore (string directory, string packagesDirectory = null)
		{
			if (!Packages.Any ())
				return;

			var isWindows = Environment.OSVersion.Platform == PlatformID.Win32NT;
			var nuget = Path.Combine (Root, "nuget", "NuGet.exe");
			var psi = new ProcessStartInfo (isWindows ? nuget : "mono") {
				Arguments = $"{(isWindows ? "" : "\"" + nuget + "\"")} restore -Verbosity Detailed -PackagesDirectory \"{Path.Combine (Root, directory, "..", "packages")}\" \"{Path.Combine (Root, directory, "packages.config")}\"",
				CreateNoWindow = true,
				UseShellExecute = false,
				WindowStyle = ProcessWindowStyle.Hidden,
				RedirectStandardError = true,
				RedirectStandardOutput = true,
			};
			//TODO: possibly remove this later?
			psi.EnvironmentVariables.Add ("MONO_LOG_LEVEL", "debug");
			Console.WriteLine ($"{psi.FileName} {psi.Arguments}");
			using (var process = new Process {
				StartInfo = psi,
			}) {
				process.OutputDataReceived += (sender, e) => Console.WriteLine (e.Data);
				process.ErrorDataReceived += (sender, e) => Console.Error.WriteLine (e.Data);
				process.Start ();
				process.BeginOutputReadLine ();
				process.BeginErrorReadLine ();
				process.WaitForExit ();
			}
		}

		public virtual string ProcessSourceTemplate (string source)
		{
			return source.Replace ("${ROOT_NAMESPACE}", RootNamespace ?? ProjectName).Replace ("${PROJECT_NAME}", ProjectName);
		}

		public void CopyNuGetConfig (string relativeDirectory)
		{
			// Copy our solution's NuGet.config
			var repoNuGetConfig = Path.Combine (XABuildPaths.TopDirectory, "NuGet.config");
			var projNugetConfig = Path.Combine (Root, relativeDirectory, "NuGet.config");
			if (File.Exists (repoNuGetConfig) && !File.Exists (projNugetConfig)) {
				File.Copy (repoNuGetConfig, projNugetConfig, overwrite: true);
				// Write additional sources to NuGet.config if needed
				if (ExtraNuGetConfigSources != null) {
					int sourceIndex = 0;
					var doc = XDocument.Load (projNugetConfig);
					XElement pkgSourcesElement = doc.Descendants ().FirstOrDefault (d => d.Name.LocalName.ToLowerInvariant () == "packagesources");
					foreach (var source in ExtraNuGetConfigSources) {
						var sourceElement = new XElement ("add");
						sourceElement.SetAttributeValue ("key", $"testsource{++sourceIndex}");
						sourceElement.SetAttributeValue ("value", source);
						pkgSourcesElement.Add (sourceElement);
					}
					doc.Save (projNugetConfig);
				}
				// Set a local PackageReference installation folder if specified
				if (!string.IsNullOrEmpty (GlobalPackagesFolder)) {
					var doc = XDocument.Load (projNugetConfig);
					XElement gpfElement = doc.Descendants ().FirstOrDefault (c => c.Name.LocalName.ToLowerInvariant () == "add"
						&& c.Attributes ().Any (a => a.Name.LocalName.ToLowerInvariant () == "key" && a.Value.ToLowerInvariant () == "globalpackagesfolder"));
					if (gpfElement != default (XElement)) {
						gpfElement.SetAttributeValue ("value", GlobalPackagesFolder);
					} else {
						var configElement = new XElement ("add");
						configElement.SetAttributeValue ("key", "globalPackagesFolder");
						configElement.SetAttributeValue ("value", GlobalPackagesFolder);
						XElement configParentElement = doc.Descendants ().FirstOrDefault (c => c.Name.LocalName.ToLowerInvariant () == "config");
						if (configParentElement != default (XElement)) {
							configParentElement.Add (configElement);
						} else {
							configParentElement = new XElement ("config");
							configParentElement.Add (configElement);
							doc.Root.Add (configParentElement);
						}
					}
					doc.Save (projNugetConfig);
				}
			}
		}
	}
}

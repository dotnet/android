using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.Build.Construction;
using System.Diagnostics;
using System.Text;

namespace Xamarin.ProjectTools
{

	// no need to be Xamarin specific, but I want to not name Project to avoid local name conflict :/
	public abstract class XamarinProject
	{
		public string ProjectName { get; set; }
		public string ProjectGuid { get; set; }
		public string AssemblyName { get; set; }

		public virtual ProjectLanguage Language { get; set; }

		string debugConfigurationName;
		string releaseConfigurationName;

		protected XamarinProject (string debugConfigurationName = "Debug", string releaseConfigurationName = "Release")
		{
			ProjectName = "UnnamedProject";

			this.debugConfigurationName = debugConfigurationName;
			this.releaseConfigurationName = releaseConfigurationName;

			Sources = new List<BuildItem> ();
			References = new List<BuildItem> ();
			OtherBuildItems = new List<BuildItem> ();

			ItemGroupList = new List<IList<BuildItem>> ();
			ItemGroupList.Add (References);
			ItemGroupList.Add (OtherBuildItems);
			ItemGroupList.Add (Sources);

			AddReferences ("System"); // default

			CommonProperties = new List<Property> ();
			common = new PropertyGroup (null, CommonProperties);
			DebugProperties = new List<Property> ();
			debug = new PropertyGroup ($"'$(Configuration)|$(Platform)' == '{debugConfigurationName}|AnyCPU'", DebugProperties);
			ReleaseProperties = new List<Property> ();
			release = new PropertyGroup ($"'$(Configuration)|$(Platform)' == '{releaseConfigurationName}|AnyCPU'", ReleaseProperties);

			PropertyGroups = new List<PropertyGroup> ();
			PropertyGroups.Add (common);
			PropertyGroups.Add (debug);
			PropertyGroups.Add (release);

			Packages = new List<Package> ();

			SetProperty (KnownProperties.Configuration, debugConfigurationName, "'$(Configuration)' == ''");
			SetProperty ("Platform", "AnyCPU", "'$(Platform)' == ''");
			SetProperty ("ErrorReport", "prompt");
			SetProperty ("WarningLevel", "4");
			SetProperty ("ConsolePause", "false");
			SetProperty ("RootNamespace", () => RootNamespace ?? ProjectName);
			SetProperty ("AssemblyName", () => AssemblyName ?? ProjectName);
			SetProperty ("BaseIntermediateOutputPath", "obj\\", " '$(BaseIntermediateOutputPath)' == '' ");

			SetProperty (DebugProperties, "DebugSymbols", "true");
			SetProperty (DebugProperties, "DebugType", "full");
			SetProperty (DebugProperties, "Optimize", "false");
			SetProperty (DebugProperties, KnownProperties.OutputPath, Path.Combine ("bin", debugConfigurationName));
			SetProperty (DebugProperties, "DefineConstants", "DEBUG;");
			SetProperty (DebugProperties, KnownProperties.IntermediateOutputPath, Path.Combine ("obj", debugConfigurationName));

			SetProperty (ReleaseProperties, "Optimize", "true");
			SetProperty (ReleaseProperties, "ErrorReport", "prompt");
			SetProperty (ReleaseProperties, "WarningLevel", "4");
			SetProperty (ReleaseProperties, "ConsolePause", "false");
			SetProperty (ReleaseProperties, KnownProperties.OutputPath, Path.Combine ("bin", releaseConfigurationName));
			SetProperty (ReleaseProperties, KnownProperties.IntermediateOutputPath, Path.Combine ("obj", releaseConfigurationName));

			Sources.Add (new BuildItem.Source (() => "Properties\\AssemblyInfo" + Language.DefaultExtension) { TextContent = () => ProcessSourceTemplate (AssemblyInfo ?? Language.DefaultAssemblyInfo) });

			Imports = new List<Import> ();
		}

		public virtual BuildOutput CreateBuildOutput (ProjectBuilder builder)
		{
			return new BuildOutput (this) { Builder = builder };
		}

		void AddProperties (IList<Property> list, string condition, KeyValuePair<string, string> [] props)
		{
			foreach (var p in props)
				list.Add (new Property (condition, p.Key, p.Value));
		}

		PropertyGroup common, debug, release;
		public IList<IList<BuildItem>> ItemGroupList { get; private set; }
		public IList<PropertyGroup> PropertyGroups { get; private set; }
		public IList<Property> CommonProperties { get; private set; }
		public IList<Property> DebugProperties { get; private set; }
		public IList<Property> ReleaseProperties { get; private set; }
		public IList<BuildItem> OtherBuildItems { get; private set; }
		public IList<BuildItem> Sources { get; private set; }
		public IList<BuildItem> References { get; private set; }
		public IList<Package> Packages { get; private set; }
		public IList<Import> Imports { get; private set; }

		public abstract string ProjectTypeGuid { get; }

		public bool IsRelease {
			get { return GetProperty (KnownProperties.Configuration) == releaseConfigurationName; }
			set { SetProperty ("Configuration", value ? releaseConfigurationName : debugConfigurationName); }
		}

		public IList<Property> ActiveConfigurationProperties {
			get { return IsRelease ? ReleaseProperties : DebugProperties; }
		}

		public string OutputPath {
			get { return GetProperty (ActiveConfigurationProperties, KnownProperties.OutputPath); }
			set { SetProperty (ActiveConfigurationProperties, KnownProperties.OutputPath, value); }
		}

		public string IntermediateOutputPath {
			get { return GetProperty (ActiveConfigurationProperties, KnownProperties.IntermediateOutputPath); }
			set { SetProperty (ActiveConfigurationProperties, KnownProperties.IntermediateOutputPath, value); }
		}

		public BuildItem GetItem (string include)
		{
			return ItemGroupList.SelectMany (g => g).First (i => i.Include ().Equals (include, StringComparison.OrdinalIgnoreCase));
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

		public void AddReferences (params string [] references)
		{
			foreach (var s in references)
				References.Add (new BuildItem.Reference (s));
		}

		public void AddSources (params string [] sources)
		{
			foreach (var s in sources)
				Sources.Add (new BuildItem.Source (s));
		}

		public void Touch (params string [] itemPaths)
		{
			foreach (var item in itemPaths)
				GetItem (item).Timestamp = DateTimeOffset.Now;
		}

		public virtual ProjectRootElement Construct ()
		{
			var root = ProjectRootElement.Create ();
			if (Packages.Any ())
				root.AddItemGroup ().AddItem (BuildActions.None, "packages.config");
			foreach (var pkg in Packages.Where (p => p.AutoAddReferences))
				foreach (var reference in pkg.References)
					if (!References.Any (r => r.Include == reference.Include))
						References.Add (reference);
			foreach (var pg in PropertyGroups)
				pg.AddElement (root);

			foreach (var ig in ItemGroupList) {
				var ige = root.AddItemGroup ();
				foreach (var i in ig) {
					if (i.Deleted)
						continue;
					ige.AddItem (i.BuildAction, i.Include (), i.Metadata);
				}
			}

			root.FullPath = ProjectName + Language.DefaultProjectExtension;

			return root;
		}

		string project_file_path;
		public string ProjectFilePath {
			get { return project_file_path ?? ProjectName + Language.DefaultProjectExtension; }
			set { project_file_path = value; }
		}

		public string AssemblyInfo { get; set; }
		public string RootNamespace { get; set; }

		public string SaveProject ()
		{
			var root = Construct ();
			var sw = new StringWriter ();
			root.Save (sw);
			return sw.ToString ();
		}

		public virtual List<ProjectResource> Save (bool saveProject = true)
		{
			var list = new List<ProjectResource> ();
			if (saveProject) {
				list.Add (new ProjectResource () {
					Timestamp = ItemGroupList.SelectMany (ig => ig).Where (i => i.Timestamp != null).Select (i => (DateTimeOffset)i.Timestamp).Max (),
					Path = ProjectFilePath,
					Content = SaveProject (),
					Encoding = System.Text.Encoding.UTF8,
				});
			}

			if (Packages.Any ()) {
				list.Add (new ProjectResource () {
					Path = "packages.config",
					Content = "<packages>\n" + string.Concat (Packages.Select (p => string.Format ("  <package id='{0}' version='{1}' targetFramework='{2}' />\n",
						p.Id, p.Version, p.TargetFramework))) + "</packages>"
				});
			}

			foreach (var ig in ItemGroupList)
				list.AddRange (ig.Select (s => new ProjectResource () {
					Timestamp = s.Timestamp,
					Path = s.Include (),
					Content = s.TextContent == null ? null : s.TextContent (),
					BinaryContent = s.BinaryContent == null ? null : s.BinaryContent (),
					Encoding = s.Encoding,
					Deleted = s.Deleted,
					Attributes = s.Attributes,
				}));

			foreach (var import in Imports)
				list.Add (new ProjectResource () {
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
				return Path.GetDirectoryName (new Uri (typeof (XamarinProject).Assembly.CodeBase).LocalPath);
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

		public void UpdateProjectFiles (string directory, IEnumerable<ProjectResource> projectFiles, bool doNotCleanup = false)
		{
			directory = Path.Combine (Root, directory.Replace ('\\', '/').Replace ('/', Path.DirectorySeparatorChar));

			if (!Directory.Exists (directory))
				throw new InvalidOperationException ("Path '" + directory + "' does not exist.");

			foreach (var p in projectFiles) {
				var path = Path.Combine (directory, p.Path.Replace ('\\', '/').Replace ('/', Path.DirectorySeparatorChar));

				if (p.Deleted) {
					if (File.Exists (path))
						File.Delete (path);
					continue;
				}
				string filedir = directory;
				if (path.Contains (Path.DirectorySeparatorChar)) {
					filedir = Path.GetDirectoryName (path);
					if (!Directory.Exists (filedir))
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
				}
				else if (p.BinaryContent != null && needsUpdate) {
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

		public void NuGetRestore (string directory, string packagesDirectory = null)
		{
			if (!Packages.Any ())
				return;

			var isWindows = Environment.OSVersion.Platform == PlatformID.Win32NT;
			var nuget = Path.Combine (Root, "NuGet.exe");
			var psi = new ProcessStartInfo (isWindows ? nuget : "mono") {
				Arguments = $"{(isWindows ? "" : "\"" + nuget + "\"")} restore -PackagesDirectory \"{Path.Combine (Root, directory, "..", "packages")}\" \"{Path.Combine (Root, directory, "packages.config")}\"",
				CreateNoWindow = true,
				WindowStyle = ProcessWindowStyle.Hidden,
			};
			var process = Process.Start (psi);
			process.WaitForExit ();
		}

		public string ProcessSourceTemplate (string source)
		{
			return source.Replace ("${ROOT_NAMESPACE}", RootNamespace ?? ProjectName).Replace ("${PROJECT_NAME}", ProjectName);
		}

	}
}

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
	/// <summary>
	/// Base class for creating and managing test project files used in Xamarin.Android.Build.Tasks tests.
	/// This class provides a framework for generating MSBuild project files, managing build items,
	/// properties, and references for testing build scenarios.
	/// </summary>
	/// <remarks>
	/// Derived classes like <see cref="XamarinAndroidProject"/> provide specific implementations
	/// for different project types. This class handles project file generation, property management,
	/// and file system operations for test projects.
	/// </remarks>
	public abstract class XamarinProject 
	{
		string debugConfigurationName;
		string releaseConfigurationName;

		/// <summary>
		/// Gets or sets the programming language for the project (C#, F#, etc.).
		/// </summary>
		/// <seealso cref="ProjectLanguage"/>
		public virtual ProjectLanguage Language { get; set; }

		/// <summary>
		/// Gets or sets the name of the project, which is used as the assembly name by default.
		/// </summary>
		public string ProjectName { get; set; }
		
		/// <summary>
		/// Gets or sets the unique GUID identifier for the project.
		/// </summary>
		public string ProjectGuid { get; set; }
		
		/// <summary>
		/// Gets or sets the assembly name for the compiled output.
		/// </summary>
		public string AssemblyName { get; set; }
		
		/// <summary>
		/// Gets the MSBuild project type GUID that identifies the project type.
		/// Must be implemented by derived classes to specify the appropriate project type.
		/// </summary>
		public abstract string ProjectTypeGuid { get; }

		/// <summary>
		/// Gets the list of properties that apply only to Debug builds.
		/// </summary>
		/// <seealso cref="ReleaseProperties"/>
		/// <seealso cref="CommonProperties"/>
		public IList<Property> DebugProperties { get; private set; }
		
		/// <summary>
		/// Gets the list of properties that apply only to Release builds.
		/// </summary>
		/// <seealso cref="DebugProperties"/>
		/// <seealso cref="CommonProperties"/>
		public IList<Property> ReleaseProperties { get; private set; }
		
		/// <summary>
		/// Gets the list of properties that apply to all build configurations.
		/// </summary>
		/// <seealso cref="DebugProperties"/>
		/// <seealso cref="ReleaseProperties"/>
		public IList<Property> CommonProperties { get; private set; }
		
		/// <summary>
		/// Gets the collection of item groups containing build items like source files, resources, etc.
		/// </summary>
		/// <seealso cref="BuildItem"/>
		public IList<IList<BuildItem>> ItemGroupList { get; private set; }
		
		/// <summary>
		/// Gets the collection of property groups that define MSBuild properties with conditions.
		/// </summary>
		/// <seealso cref="PropertyGroup"/>
		public IList<PropertyGroup> PropertyGroups { get; private set; }
		
		/// <summary>
		/// Gets the collection of assembly references for the project.
		/// </summary>
		/// <seealso cref="PackageReferences"/>
		public IList<BuildItem> References { get; private set; }
		
		/// <summary>
		/// Gets the collection of NuGet package references for the project.
		/// </summary>
		/// <seealso cref="References"/>
		/// <seealso cref="Package"/>
		public IList<Package> PackageReferences { get; private set; }
		
		/// <summary>
		/// Gets or sets the global packages folder path for NuGet packages.
		/// Defaults to the system's NuGet global packages folder.
		/// </summary>
		public string GlobalPackagesFolder { get; set; } = FileSystemUtils.FindNugetGlobalPackageFolder ();
		
		/// <summary>
		/// Gets or sets additional NuGet package source URLs to include in NuGet.config.
		/// </summary>
		public IList<string> ExtraNuGetConfigSources { get; set; } = new List<string> ();

		/// <summary>
		/// Gets a value indicating whether NuGet package restore should be performed.
		/// Returns true if the project has any package references.
		/// </summary>
		public virtual bool ShouldRestorePackageReferences => PackageReferences?.Count > 0;
		/// <summary>
		/// If true, the ProjectDirectory will be deleted and populated on the first build
		/// </summary>
		public virtual bool ShouldPopulate { get; set; } = true;
		
		/// <summary>
		/// Gets the collection of MSBuild import statements for the project.
		/// </summary>
		/// <seealso cref="Import"/>
		public IList<Import> Imports { get; private set; }
		PropertyGroup common, debug, release;
		bool isRelease;

		/// <summary>
		/// Configures projects with Configuration=Debug or Release
		/// * Directly in the `.csproj` file for legacy projects
		/// * Uses `Directory.Build.props` for .NET 5+ projects
		/// </summary>
		public bool IsRelease {
			get => isRelease;
			set {
				isRelease = value;
				Touch ("Directory.Build.props");
			}
		}

		/// <summary>
		/// Gets the current build configuration name (Debug or Release).
		/// </summary>
		public string Configuration => IsRelease ? releaseConfigurationName : debugConfigurationName;

		/// <summary>
		/// Initializes a new instance of the XamarinProject class with the specified configuration names.
		/// </summary>
		/// <param name="debugConfigurationName">The name for the debug configuration (default: "Debug").</param>
		/// <param name="releaseConfigurationName">The name for the release configuration (default: "Release").</param>
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
			ReleaseProperties = new List<Property> ();
			debug = new PropertyGroup ($"'$(Configuration)' == '{debugConfigurationName}'", DebugProperties);
			release = new PropertyGroup ($"'$(Configuration)' == '{releaseConfigurationName}'", ReleaseProperties);
			PropertyGroups.Add (common);
			PropertyGroups.Add (debug);
			PropertyGroups.Add (release);
			Imports = new List<Import> ();

			//NOTE: for SDK-style projects, we need $(Configuration) set before Microsoft.NET.Sdk.targets
			Imports.Add (new Import ("Directory.Build.props") {
				TextContent = () =>
$@"<Project>
	<PropertyGroup>
		<Configuration>{Configuration}</Configuration>
		<DisableTransitiveFrameworkReferenceDownloads>true</DisableTransitiveFrameworkReferenceDownloads>
	</PropertyGroup>
</Project>"
			});
		}

		/// <summary>
		/// Adds a reference to another project. The optional include path uses a relative path and ProjectName if omitted.
		/// </summary>
		/// <param name="other">The project to reference.</param>
		/// <param name="include">Optional relative path to the project file. If null, defaults to "../{ProjectName}/{ProjectName}.csproj".</param>
		/// <seealso cref="References"/>
		public void AddReference (XamarinProject other, string include = null)
		{
			if (string.IsNullOrEmpty (include)) {
				include = $"..\\{other.ProjectName}\\{other.ProjectName}.csproj";
			}
			References.Add (new BuildItem.ProjectReference (include, other.ProjectName, other.ProjectGuid));
		}

		/// <summary>
		/// Gets or sets the target framework for the project (e.g., "net9.0-android").
		/// </summary>
		/// <seealso cref="TargetFrameworks"/>
		public string TargetFramework {
			get { return GetProperty ("TargetFramework"); }
			set { SetProperty ("TargetFramework", value); }
		}

		/// <summary>
		/// Gets or sets multiple target frameworks for the project, separated by semicolons.
		/// </summary>
		/// <seealso cref="TargetFramework"/>
		public string TargetFrameworks {
			get { return GetProperty ("TargetFrameworks"); }
			set { SetProperty ("TargetFrameworks", value); }
		}

		/// <summary>
		/// Gets the value of a property from the common properties collection.
		/// </summary>
		/// <param name="name">The name of the property to retrieve.</param>
		/// <returns>The property value, or null if not found.</returns>
		/// <seealso cref="SetProperty(string, string, string)"/>
		public string GetProperty (string name)
		{
			return GetProperty (CommonProperties, name);
		}

		/// <summary>
		/// Gets the value of a property from the specified property group.
		/// </summary>
		/// <param name="group">The property group to search in.</param>
		/// <param name="name">The name of the property to retrieve.</param>
		/// <returns>The property value, or null if not found.</returns>
		/// <seealso cref="CommonProperties"/>
		/// <seealso cref="DebugProperties"/>
		/// <seealso cref="ReleaseProperties"/>
		public string GetProperty (IList<Property> group, string name)
		{
			var prop = group.FirstOrDefault (p => p.Name.Equals (name, StringComparison.OrdinalIgnoreCase));
			return prop != null ? prop.Value () : null;
		}

		/// <summary>
		/// Sets a property value in the common properties collection.
		/// </summary>
		/// <param name="name">The name of the property to set.</param>
		/// <param name="value">The value to assign to the property.</param>
		/// <param name="condition">Optional MSBuild condition for the property.</param>
		/// <seealso cref="GetProperty(string)"/>
		/// <seealso cref="RemoveProperty(string)"/>
		public void SetProperty (string name, string value, string condition = null)
		{
			SetProperty (name, () => value, condition);
		}

		/// <summary>
		/// Sets a boolean property value in the specified property group.
		/// </summary>
		/// <param name="group">The property group to add the property to.</param>
		/// <param name="name">The name of the property to set.</param>
		/// <param name="value">The boolean value to assign to the property.</param>
		/// <param name="condition">Optional MSBuild condition for the property.</param>
		public void SetProperty (IList<Property> group, string name, bool value, string condition = null)
		{
			SetProperty (group, name, () => value.ToString (), condition);
		}

		/// <summary>
		/// Sets a property value in the specified property group.
		/// </summary>
		/// <param name="group">The property group to add the property to.</param>
		/// <param name="name">The name of the property to set.</param>
		/// <param name="value">The value to assign to the property.</param>
		/// <param name="condition">Optional MSBuild condition for the property.</param>
		public void SetProperty (IList<Property> group, string name, string value, string condition = null)
		{
			SetProperty (group, name, () => value, condition);
		}

		/// <summary>
		/// Removes a property from the common properties collection.
		/// </summary>
		/// <param name="name">The name of the property to remove.</param>
		/// <returns>True if the property was found and removed; otherwise, false.</returns>
		/// <seealso cref="SetProperty(string, string, string)"/>
		public bool RemoveProperty (string name)
		{
			return RemoveProperty (CommonProperties, name);
		}

		/// <summary>
		/// Removes a property from the specified property group.
		/// </summary>
		/// <param name="group">The property group to remove the property from.</param>
		/// <param name="name">The name of the property to remove.</param>
		/// <returns>True if the property was found and removed; otherwise, false.</returns>
		public bool RemoveProperty (IList<Property> group, string name)
		{
			var prop = group.FirstOrDefault (p => p.Name.Equals (name, StringComparison.OrdinalIgnoreCase));
			if (prop == null)
				return false;
			group.Remove (prop);
			return true;
		}

		/// <summary>
		/// Sets a property value with a function that provides the value in the common properties collection.
		/// </summary>
		/// <param name="name">The name of the property to set.</param>
		/// <param name="value">A function that returns the property value.</param>
		/// <param name="condition">Optional MSBuild condition for the property.</param>
		public void SetProperty (string name, Func<string> value, string condition = null)
		{
			SetProperty (CommonProperties, name, value);
		}

		/// <summary>
		/// Sets a property value with a function that provides the value in the specified property group.
		/// </summary>
		/// <param name="group">The property group to add the property to.</param>
		/// <param name="name">The name of the property to set.</param>
		/// <param name="value">A function that returns the property value.</param>
		/// <param name="condition">Optional MSBuild condition for the property.</param>
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

		/// <summary>
		/// Gets a build item with the specified include path.
		/// </summary>
		/// <param name="include">The include path of the build item to find.</param>
		/// <returns>The build item if found; otherwise, null.</returns>
		/// <seealso cref="BuildItem"/>
		/// <seealso cref="ItemGroupList"/>
		public BuildItem GetItem (string include)
		{
			return ItemGroupList.SelectMany (g => g).FirstOrDefault (i => i.Include ().Equals (include, StringComparison.OrdinalIgnoreCase));
		}

		/// <summary>
		/// Gets an import with the specified project path.
		/// </summary>
		/// <param name="include">The project path of the import to find.</param>
		/// <returns>The import if found; otherwise, null.</returns>
		/// <seealso cref="Import"/>
		/// <seealso cref="Imports"/>
		public Import GetImport (string include)
		{
			return Imports.FirstOrDefault (i => i.Project ().Equals (include, StringComparison.OrdinalIgnoreCase));
		}

		/// <summary>
		/// Updates the timestamp of the specified build items or imports to trigger rebuild.
		/// </summary>
		/// <param name="itemPaths">The paths of items or imports to touch.</param>
		/// <exception cref="InvalidOperationException">Thrown if any path is not found in the project.</exception>
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
		
		/// <summary>
		/// Gets or sets the file path for the project file.
		/// If not set, defaults to ProjectName + Language.DefaultProjectExtension.
		/// </summary>
		/// <seealso cref="ProjectName"/>
		/// <seealso cref="Language"/>
		public string ProjectFilePath {
			get { return project_file_path ?? ProjectName + Language.DefaultProjectExtension; }
			set { project_file_path = value; }
		}

		/// <summary>
		/// Gets or sets the AssemblyInfo.cs content for the project.
		/// </summary>
		public string AssemblyInfo { get; set; }
		
		/// <summary>
		/// Gets or sets the root namespace for the project.
		/// </summary>
		public string RootNamespace { get; set; }

		/// <summary>
		/// Generates the MSBuild project file content.
		/// Must be implemented by derived classes to provide the appropriate project file format.
		/// </summary>
		/// <returns>The project file content as a string.</returns>
		public virtual string SaveProject ()
		{
			return string.Empty;
		}

		/// <summary>
		/// Creates a build output object for this project with the specified builder.
		/// </summary>
		/// <param name="builder">The project builder that built this project.</param>
		/// <returns>A new <see cref="BuildOutput"/> instance.</returns>
		/// <seealso cref="BuildOutput"/>
		/// <seealso cref="ProjectBuilder"/>
		public virtual BuildOutput CreateBuildOutput (ProjectBuilder builder)
		{
			return new BuildOutput (this) { Builder = builder };
		}

		ProjectResource project;

		/// <summary>
		/// Saves the project and all its associated files to a list of project resources.
		/// </summary>
		/// <param name="saveProject">Whether to include the project file itself in the results.</param>
		/// <returns>A list of <see cref="ProjectResource"/> objects representing all project files.</returns>
		/// <seealso cref="ProjectResource"/>
		/// <seealso cref="Populate(string)"/>
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

			foreach (var ig in ItemGroupList)
				list.AddRange (ig.Select (s => new ProjectResource () {
					Timestamp = s.Timestamp,
					Path = s.Include?.Invoke () ?? s.Update?.Invoke (),
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

		/// <summary>
		/// Populates the specified directory with the project files.
		/// </summary>
		/// <param name="directory">The target directory to populate.</param>
		/// <seealso cref="Save(bool)"/>
		/// <seealso cref="UpdateProjectFiles(string, IEnumerable{ProjectResource}, bool)"/>
		public void Populate (string directory)
		{
			Populate (directory, Save ());
		}

		/// <summary>
		/// Gets the root directory for test projects.
		/// </summary>
		/// <seealso cref="XABuildPaths"/>
		public string Root {
			get {
				return XABuildPaths.TestOutputDirectory;
			}
		}

		/// <summary>
		/// Populates the specified directory with the provided project files.
		/// </summary>
		/// <param name="directory">The target directory to populate.</param>
		/// <param name="projectFiles">The project files to write to the directory.</param>
		/// <exception cref="InvalidOperationException">Thrown if the target path already exists.</exception>
		/// <seealso cref="UpdateProjectFiles(string, IEnumerable{ProjectResource}, bool)"/>
		public void Populate (string directory, IEnumerable<ProjectResource> projectFiles)
		{
			directory = directory.Replace ('\\', '/').Replace ('/', Path.DirectorySeparatorChar);

			if (File.Exists (directory) || Directory.Exists (directory))
				throw new InvalidOperationException ("Path '" + directory + "' already exists. Cannot create a project at this state.");

			Directory.CreateDirectory (Path.Combine (Root, directory));

			UpdateProjectFiles (directory, projectFiles);
		}

		/// <summary>
		/// Updates project files in an existing directory, creating, updating, or deleting files as needed.
		/// </summary>
		/// <param name="directory">The target directory containing the project.</param>
		/// <param name="projectFiles">The project files to synchronize.</param>
		/// <param name="doNotCleanup">If true, existing files not in projectFiles will not be deleted.</param>
		/// <exception cref="InvalidOperationException">Thrown if the target directory does not exist.</exception>
		/// <seealso cref="Populate(string, IEnumerable{ProjectResource})"/>
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
				if (path.Contains ($"{Path.DirectorySeparatorChar}")) {
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
					if (subname.Equals ("NuGet.config", StringComparison.OrdinalIgnoreCase))
						continue;
					if (subname.EndsWith (".log", StringComparison.OrdinalIgnoreCase) || subname.EndsWith (".binlog", StringComparison.OrdinalIgnoreCase))
						continue;
					if (!projectFiles.Any (p => p.Path != null && p.Path.Replace ('\\', '/').Equals (subname))) {
						fi.Delete ();
					}
				}
			}

		}

		/// <summary>
		/// Processes source template content by replacing placeholders with project-specific values.
		/// </summary>
		/// <param name="source">The source template content.</param>
		/// <returns>The processed content with placeholders replaced.</returns>
		/// <remarks>
		/// Replaces ${ROOT_NAMESPACE} with <see cref="RootNamespace"/> or <see cref="ProjectName"/>,
		/// and ${PROJECT_NAME} with <see cref="ProjectName"/>.
		/// </remarks>
		public virtual string ProcessSourceTemplate (string source)
		{
			return source.Replace ("${ROOT_NAMESPACE}", RootNamespace ?? ProjectName).Replace ("${PROJECT_NAME}", ProjectName);
		}

		/// <summary>
		/// Copies the repository's NuGet.config file to the project directory and configures it.
		/// </summary>
		/// <param name="relativeDirectory">The relative directory path for the project.</param>
		/// <seealso cref="GlobalPackagesFolder"/>
		/// <seealso cref="ExtraNuGetConfigSources"/>
		public void CopyNuGetConfig (string relativeDirectory)
		{
			// Copy our solution's NuGet.config
			var repoNuGetConfig = Path.Combine (XABuildPaths.TopDirectory, "NuGet.config");
			var projNugetConfig = Path.Combine (Root, relativeDirectory, "NuGet.config");
			if (File.Exists (repoNuGetConfig) && !File.Exists (projNugetConfig)) {
				Directory.CreateDirectory (Path.GetDirectoryName (projNugetConfig));
				File.Copy (repoNuGetConfig, projNugetConfig, overwrite: true);

				AddNuGetConfigSources (projNugetConfig);

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

		/// <summary>
		/// Updates a NuGet.config based on sources in ExtraNuGetConfigSources
		/// </summary>
		/// <param name="nugetConfigPath">The path to the NuGet.config file to update.</param>
		/// <seealso cref="ExtraNuGetConfigSources"/>
		/// <seealso cref="CopyNuGetConfig(string)"/>
		protected void AddNuGetConfigSources (string nugetConfigPath)
		{
			XDocument doc;
			if (File.Exists (nugetConfigPath))
				doc = XDocument.Load (nugetConfigPath);
			else
				doc = new XDocument (new XElement ("configuration"));

			const string elementName = "packageSources";
			XElement pkgSourcesElement = doc.Root?.Elements ().FirstOrDefault (d => string.Equals (d.Name.LocalName, elementName, StringComparison.OrdinalIgnoreCase));
			if (pkgSourcesElement == null) {
				doc.Root.Add (pkgSourcesElement = new XElement (elementName));
			}

			if (ExtraNuGetConfigSources == null) {
				ExtraNuGetConfigSources = new List<string> ();
			}

			int sourceIndex = 0;
			foreach (var source in ExtraNuGetConfigSources.Distinct ()) {
				var sourceElement = new XElement ("add");
				sourceElement.SetAttributeValue ("key", $"testsource{++sourceIndex}");
				sourceElement.SetAttributeValue ("value", source);
				pkgSourcesElement.Add (sourceElement);
			}

			doc.Save (nugetConfigPath);
		}
	}
}

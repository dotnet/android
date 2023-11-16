using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	/// <summary>
	///   Base class for all supported operating systems.
	/// </summary>
	abstract class OS : AppObject
	{
		/// <summary>
		///   Type of the operating system (<c>Linux</c>, <c>Darwin</c> or <c>Windows</c> currently)
		/// </summary>
		public abstract string Type { get; }

		/// <summary>
		///   List of programs Xamarin.Android depends on when running on the particular OS
		/// </summary>
		public abstract List<Program> Dependencies { get; }

		/// <summary>
		///   A convenience shortcut for <see cref="Context.Instance" />
		/// </summary>
		public Context Context { get; }

		/// <summary>
		///   <c>true</c> if the OS is 64-bit
		/// </summary>
		public bool Is64Bit => Environment.Is64BitOperatingSystem;

		/// <summary>
		///   Number of processors found in the host machine
		/// </summary>
		public uint CPUCount => (uint)Environment.ProcessorCount;

		public string DiskInformation { get; protected set; } = string.Empty;

		/// <summary>
		///   A dictionary of variables to export in environment of all the executed programs, scripts etc.
		/// </summary>
		public Dictionary<string, string> EnvironmentVariables { get; }

		/// <summary>
		///   Path to <c>javac</c> (full or relative)
		/// </summary>
		public string JavaCPath      { get; set; } = String.Empty;

		/// <summary>
		///   Path to <c>jar</c> (full or relative)
		/// </summary>
		public string JarPath        { get; set; } = String.Empty;

		/// <summary>
		///   Path to <c>java</c> (full or relative)
		/// </summary>
		public string JavaPath       { get; set; } = String.Empty;

		/// <summary>
		///   Full path to Java home, set via the <c>$(JavaSdkDirectory)</c> MSBuild property or defaults to
		///   <c>$(AndroidToolchainDirectory)/jdk</c>.
		/// </summary>
		public string JavaHome       { get; set; } = String.Empty;

		/// <summary>
		///   Name of the operating system (e.g. Ubuntu, Debian, Arch etc for Linux-based operating systems, Mac OS X, Windows)
		/// </summary>
		public string Name           { get; protected set; } = String.Empty;

		/// <summary>
		///   OS "flavor" name (i.e. a variation of the OS named <see cref="Name"/>), may be equal to <see cref="Name"/>
		/// </summary>
		public string Flavor         { get; protected set; } = String.Empty;

		/// <summary>
		///   Operating system release version/name
		/// </summary>
		public string Release        { get; protected set; } = String.Empty;

		/// <summary>
		///   Host system architecture (e.g. x86, x86_64)
		/// </summary>
		public string Architecture   { get; protected set; } = String.Empty;

		/// <summary>
		///   Prefix where Homebrew is installed (relevant only on macOS, but present in all operating system for
		///   compatibility reasons)
		/// </summary>
		public string HomebrewPrefix { get; set; } = String.Empty;

		public virtual bool ProcessIsTranslated => false;

		/// <summary>
		///   File extension used for ZIP archives
		/// </summary>
		public string ZipExtension   { get; protected set; } = "zip";

		/// <summary>
		///   Return full path to the user's home directory
		/// </summary>
		public abstract string HomeDirectory { get; }

		/// <summary>
		///   Extensions of executable files as supported by the host OS or <c>null</c> if executable extensions aren't
		///   used/needed
		/// </summary>
		protected abstract List<string>? ExecutableExtensions     { get; }

		/// <summary>
		///   Default string comparison for path comparison
		/// </summary>
		public abstract StringComparison DefaultStringComparison { get; }

		/// <summary>
		///   Default string comparer for path comparison
		/// </summary>
		public abstract StringComparer DefaultStringComparer     { get; }

		/// <summary>
		///   <c>true</c> if we are running on Windows
		/// </summary>
		public abstract bool IsWindows                           { get; }

		/// <summary>
		///   <c>true</c> if we are running on Unix
		/// </summary>
		public abstract bool IsUnix                              { get; }

		/// <summary>
		///   Returns path to the managed program "runner" (most commonly a .NET runtime) used by the host OS or null if
		///   no such runner is needed.
		/// </summary>
		public abstract string GetManagedProgramRunner (string programPath);

		/// <summary>
		///   Initialize base OS support properties, variables etc. Implementation should perform as little detection of
		///   programs etc as possible
		/// </summary>
		protected virtual bool InitOS ()
		{
			JavaHome = Context.Instance.Properties.GetValue (KnownProperties.JavaSdkDirectory)?.Trim () ?? String.Empty;
			if (String.IsNullOrEmpty (JavaHome)) {
				var androidToolchainDirectory = Context.Instance.Properties.GetValue (KnownProperties.AndroidToolchainDirectory)?.Trim () ?? String.Empty;
				JavaHome = Path.Combine (androidToolchainDirectory, Configurables.Defaults.JdkFolder);
			}

			string extension = IsWindows ? ".exe" : string.Empty;
			JavaCPath = Path.Combine (JavaHome, "bin", $"javac{extension}");
			JavaPath = Path.Combine (JavaHome, "bin", $"java{extension}");
			JarPath = Path.Combine (JavaHome, "bin", $"jar{extension}");

			return true;
		}

		/// <summary>
		///   Initialize <see cref="Dependencies"/> for the host OS
		/// </summary>
		protected abstract void InitializeDependencies ();

		/// <summary>
		///   Assert that the program passed in <paramref name="fullPath"/> is in fact executable and throw an exception if
		///   it is not.
		/// </summary>
		protected abstract string AssertIsExecutable (string fullPath);

		protected OS (Context context)
		{
			Context = context;
			EnvironmentVariables = new Dictionary<string, string> (StringComparer.Ordinal);
		}

		/// <summary>
		///   Override to print notices/information/etc at the very end of bootstrapper run.
		/// </summary>
		public virtual void ShowFinalNotices ()
		{}

		/// <summary>
		/// <para>
		///   Initialize OS support. Initializes basic OS properties (by calling <see cref="InitOS"/>), dependencies (by
		///   calling <see cref="InitializeDependencies"/>) as well as makes sure that all the dependencies are
		///   installed and initializes the environment.
		/// </para>
		/// <para>
		///   Missing dependencies are installed only if <see cref="KnownConditions.AllowProgramInstallation"/>
		///   condition is set and and <see cref="Context.AutoProvision"/> is <c>true</c>. If the two conditions aren't
		///   met and missing programs are found, the initialization fails unless the <see
		///   cref="KnownConditions.IgnoreMissingPrograms"/> is set to <c>true</c> in which case only a warning is
		///   printed regarding the missing dependencies.
		/// </para>
		/// </summary>
		public async Task<bool> Init ()
		{
			if (!InitOS ())
				throw new InvalidOperationException ("Failed to initialize operating system support");

			InitializeDependencies ();

			Context.Banner ("Ensuring all required programs are installed");
			if (!await EnsureDependencies ())
				return false;

			Context.Banner ("Configuring environment");
			ConfigureEnvironment ();
			return true;
		}

		async Task<bool> EnsureDependencies ()
		{
			if (Dependencies == null)
				throw new InvalidOperationException ("Dependencies not set");

			Log.Todo ("Implement 'package refresh' mode where we reinstall packages/programs forcibly");

			int maxNameLength = GetMaxNameLength (Dependencies);
			var missing = new List <Program> ();
			foreach (Program p in Dependencies) {
				if (p == null)
					continue;

				Log.Status ($"Checking ", $"{p.Name}".PadRight (maxNameLength), tailColor: ConsoleColor.White);
				bool installed = await p.IsInstalled ();
				if (installed) {
					if (!p.InstalledButWrongVersion && !p.MustReinstall) {
						Log.StatusLine ($" [FOUND {p.CurrentVersion}]", Context.SuccessColor);
					} else {
						if (p.MustReinstall)
							Log.StatusLine ($" [MUST REINSTALL {p.CurrentVersion}]", Context.WarningColor);
						else
							Log.StatusLine ($" [WRONG VERSION {p.CurrentVersion}]", Context.WarningColor);
						missing.Add (p);
					}
				} else {
					Log.StatusLine (" [MISSING]", Context.FailureColor);
					missing.Add (p);
				}
			}

			if (missing.Count == 0)
				return true;

			bool ignoreMissing = Context.Instance.CheckCondition (KnownConditions.IgnoreMissingPrograms);
			if (!Context.Instance.AutoProvision) {
				string message = "Some programs are missing or have invalid versions, but automatic provisioning is disabled";
				if (ignoreMissing) {
					Log.WarningLine ($"{message}. Ignoring missing programs.");
					return true;
				}

				Log.ErrorLine (message);
				return false;
			}

			maxNameLength = GetMaxNameLength (missing);
			Context.Banner ("Installing programs");
			if (missing.Any (p => p.NeedsSudoToInstall))
				Log.StatusLine ("You might be prompted for your sudo password");

			bool someFailed = false;
			foreach (Program p in missing) {
				if (p.NeedsSudoToInstall && !Context.AutoProvisionUsesSudo) {
					Log.ErrorLine ($"Program '{p.Name}' requires sudo to install but sudo is disabled");
					someFailed = true;
					continue;
				}

				if (!p.CanInstall ()) {
					if (!ignoreMissing)
						someFailed = true;
					Log.Status ("Installation disabled for ");
					Log.StatusLine (p.Name.PadRight (maxNameLength), ConsoleColor.Cyan);
					continue;
				}

				Log.Status ("Installing ");
				Log.StatusLine (p.Name.PadRight (maxNameLength), ConsoleColor.White);
				bool success = await p.Install ();
				Log.StatusLine ();
				if (success)
					continue;

				someFailed = true;
				Log.ErrorLine ($"Installation of {p.Name} failed");
			}

			if (someFailed)
				throw new InvalidOperationException ("Failed to install some required programs.");

			return true;

			int GetMaxNameLength (List<Program> list)
			{
				int ret = 0;
				list.ForEach (p => {
						int len = p.Name.Length;
						if (len < ret)
							return;
						ret = len;
					}
				);
				ret++;

				return ret;
			}
		}

		void ConfigureEnvironment ()
		{
			PopulateEnvironmentVariables ();

			foreach (var kvp in EnvironmentVariables) {
				string name = kvp.Key.Trim ();
				if (String.IsNullOrEmpty (name))
					continue;

				string value = kvp.Value ?? String.Empty;
				Log.DebugLine ($"Setting environment variable: {name} = {value}");
				Environment.SetEnvironmentVariable (name, value);
			}
		}

		/// <summary>
		///   Populate <see cref="EnvironmentVariables"/> with variables to be exported in the environment of all the
		///   programs executed by the bootstrapper.
		/// </summary>
		protected virtual void PopulateEnvironmentVariables ()
		{
			EnvironmentVariables ["OS_NAME"] = Name;
			EnvironmentVariables ["OS_ARCH"] = Architecture;
		}

		/// <summary>
		///   Locate the program indicated in <paramref name="programPath"/> which can be just the base program name
		///   (without the executable extension for cross-OS compatibility) or a full path to a program (however also
		///   without the executable extension). In both cases the method tries to find the program at the indicated
		///   location (if a path is used in <paramref name="programPath"/>) or in the directories found in the
		///   <c>PATH</c> environment variable, trying all executable extensions (<see cref="ExecutableExtensions"/>)
		///   until the program file is found. If the file is indeed found, a check is made whether it is executable (by
		///   calling <see cref="AssertIsExecutable"/>) and the path is returned. If, however, the program is not found
		///   and <paramref name="required"/> is <c>true</c> then an exception is throw. If <paramref name="required"/>
		///   is <c>false</c>, however, <c>null</c> is returned.
		/// </summary>
		public virtual string Which (string programPath, bool required = true)
		{
			if (String.IsNullOrEmpty (programPath)) {
				goto doneAndOut;
			}

			string match;
			// If it's any form of path, just return it as-is, possibly with executable extension added
			if (programPath.IndexOf (Path.DirectorySeparatorChar) >= 0) {
				match = GetExecutableWithExtension (programPath, (string ext) => {
						string fp = $"{programPath}{ext}";
						if (Utilities.FileExists (fp))
							return fp;
						return String.Empty;
					}
				);

				if (match.Length == 0 && Utilities.FileExists (programPath))
					match = programPath;

				if (match.Length > 0)
					return match;
				else if (required) {
					goto doneAndOut;
				}

				return programPath;
			}

			List<string> directories = GetPathDirectories ();
			match = GetExecutableWithExtension (programPath, (string ext) => FindProgram ($"{programPath}{ext}", directories));
			if (match.Length > 0)
				return AssertIsExecutable (match);

			match = FindProgram ($"{programPath}", directories);
			if (match.Length > 0)
				return AssertIsExecutable (match);

		  doneAndOut:
			if (required)
				throw new InvalidOperationException ($"Required program '{programPath}' could not be found");

			return String.Empty;
		}

		string GetExecutableWithExtension (string programPath, Func<string, string> finder)
		{
			List<string>? extensions = ExecutableExtensions;
			if (extensions == null || extensions.Count == 0)
				return String.Empty;

			foreach (string extension in extensions) {
				string match = finder (extension);
				if (match.Length > 0)
					return match;
			}

			return String.Empty;
		}

		public virtual string AppendExecutableExtension (string programName)
		{
			return programName;
		}

		protected static string FindProgram (string programName, List<string> directories)
		{
			foreach (string dir in directories) {
				string path = Path.Combine (dir, programName);
				if (Utilities.FileExists (path))
					return path;
			}

			return String.Empty;
		}

		protected static List <string> GetPathDirectories ()
		{
			var ret = new List <string> ();
			string path = Environment.GetEnvironmentVariable ("PATH")?.Trim () ?? String.Empty;
			if (String.IsNullOrEmpty (path))
				return ret;

			ret.AddRange (path.Split (new []{ Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries));
			return ret;
		}
	};
}

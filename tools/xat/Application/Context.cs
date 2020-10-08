using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Xamarin.Android.Tests;

namespace Xamarin.Android.Prepare
{
	// Minimum implementation to satisfy needs of the classes we use from xaprepare
	partial class Context
	{
		readonly object testCollectionLock = new object ();
		public static readonly char[] NewlineSplit = new [] { '\n' };
		public static readonly char[] WhitespaceSplit = new [] { ' ', '\t' };

		TestCollection? testCollection;
		Characters? characters;

		public static Context Instance           { get; } = new Context ();

		public OS OS                             { get; } = new OS ();
		public bool DullMode                     => false;
		public bool NoEmoji                      => false;
		public bool UseColor                     { get; set; } = true;
		public bool CanConsoleUseUnicode         { get; }
		public VersionFetchers VersionFetchers   { get; } = new VersionFetchers ();
		public LoggingVerbosity LoggingVerbosity { get; set; } = LoggingVerbosity.Normal;
		public string Configuration              { get; set; } = Configurables.Defaults.DefaultConfiguration;
		public string BuildTimeStamp             { get; }
		public uint MakeConcurrency              { get; set; } = Configurables.Defaults.MakeConcurrency;
		public Properties Properties             { get; } = new Properties ();
		public List<string> FailedTests          { get; } = new List<string> ();
		public string MainLogFilePath            { get; }
		public string LogDirectory               { get; }
		public Characters Characters             => characters ?? throw new InvalidOperationException ("Context not initialized properly (was .Init called?)");

		/// <summary>
		///   A shortcut to access a small set of essential tools used by the bootstrapper. See <see cref="EssentialTools" />
		/// </summary>
		public EssentialTools Tools                    { get; private set; } = new EssentialTools ();

		public bool RequireNewEmulator           { get; set; }
		public string AdbTarget                  { get; set; } = String.Empty;
		public string AdbPath                    { get; set; } = String.Empty;
		public string AdbOptions                 { get; set; } = String.Empty;
		public string EmulatorPath               { get; set; } = String.Empty;
		public string AvdManagerPath             { get; set; } = String.Empty;
		public string BundleToolJarPath          { get; set; } = String.Empty;
		public string JavaPath                   { get; set; } = String.Empty;
		public string NUnitPath                  { get; set; } = String.Empty;
		public string NUnitOptions               { get; set; } = String.Empty;
		public string MSBuildBinary              { get; set; } = String.Empty;

		public TestCollection Tests {
			get {
				lock (testCollectionLock) {
					if (testCollection == null) {
						testCollection = new TestCollection ();
					}
				}

				return testCollection;
			}
		}

		Context ()
		{
			var now = DateTime.Now;
			BuildTimeStamp = $"{now.Year}{now.Month:00}{now.Day:00}T{now.Hour:00}{now.Minute:00}{now.Second:00}";
			LogDirectory = Path.Combine (BuildPaths.XamarinAndroidSourceRoot, "bin", $"Test{Configuration}");
			Directory.CreateDirectory (LogDirectory);

			MainLogFilePath = GetLogFilePath (null, true);

			CanConsoleUseUnicode =
				Console.OutputEncoding is UTF7Encoding ||
				Console.OutputEncoding is UTF8Encoding ||
				Console.OutputEncoding is UTF32Encoding ||
				Console.OutputEncoding is UnicodeEncoding;
		}

		public void Init ()
		{
			characters = Characters.Create (this);
			AddExtraProperties ();
			Configuration = Properties.GetRequiredValue (KnownProperties.Configuration);

			AdbPath = MakeToolPath (KnownProperties.AdbToolExe, KnownProperties.AdbToolPath);
			EmulatorPath = MakeToolPath (KnownProperties.EmulatorToolExe, KnownProperties.EmulatorToolPath);
			AvdManagerPath = MakeToolPath (KnownProperties.AvdManagerToolExe, KnownProperties.CommandLineToolsBinPath);
			NUnitPath = Properties.GetRequiredValue (KnownProperties.NUnit);

			// Support for running AAB tests against a system installation of XA.
			string bundleToolPath = Properties.GetRequiredValue (KnownProperties.BundleToolJarPath);
			if (!Utilities.FileExists (bundleToolPath)) {
				bundleToolPath = Configurables.Paths.DefaultBundleToolJarPath;
			}

			if (!Utilities.FileExists (bundleToolPath)) {
				bundleToolPath = Properties.GetValue (KnownProperties.AndroidBundleToolJarPath) ?? String.Empty;
			}

			if (String.IsNullOrEmpty (bundleToolPath)) {
				throw new InvalidOperationException ("Android BundleTool path must be specified");
			}

			BundleToolJarPath = bundleToolPath!;

			string? javaPath = Properties.GetValue (KnownProperties.JavaPath);
			if (String.IsNullOrWhiteSpace (javaPath)) {
				javaPath = Configurables.Paths.DefaultJavaPath;
			}

			JavaPath = javaPath!;

			if (MSBuildBinary.Length == 0) {
				if (Utilities.FileExists (Configurables.Paths.XABuildReleasePath)) {
					MSBuildBinary = Configurables.Paths.XABuildReleasePath;
				} else if (Utilities.FileExists (Configurables.Paths.XABuildConfigurationPath)) {
					MSBuildBinary = Configurables.Paths.XABuildConfigurationPath;
				} else {
					MSBuildBinary = "msbuild";
				}
			}

			Tools.Init (this);
		}

		string MakeToolPath (string toolExeProperty, string toolPathProperty)
		{
			string toolExe = Properties.GetRequiredValue (toolExeProperty);
			string toolPath = Properties.GetValue (toolPathProperty) ?? String.Empty;
			string ret;

			if (toolPath.Length > 0) {
				ret = Path.Combine (toolPath, toolExe);
			} else {
				ret = toolExe;
			}

			return ret;
		}

		partial void AddExtraProperties ();

		public string GetLogFilePath (string tags)
		{
			return GetLogFilePath (tags, false);
		}

		string GetLogFilePath (string? tags, bool mainLogFile)
		{
			string logFileName;
			if (String.IsNullOrEmpty (tags)) {
				if (!mainLogFile)
					throw new ArgumentException ("must not be null or empty", nameof (tags));
				logFileName = $"{Configurables.Defaults.LogFilePrefix}-{BuildTimeStamp}.log";
			} else {
				logFileName = $"{Configurables.Defaults.LogFilePrefix}-{BuildTimeStamp}.{tags}.log";
			}

			return Path.Combine (LogDirectory, logFileName);
		}
	}
}

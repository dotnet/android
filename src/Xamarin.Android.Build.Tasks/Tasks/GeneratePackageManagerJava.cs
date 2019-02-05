// Copyright (C) 2011 Xamarin, Inc. All rights reserved.

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	public class GeneratePackageManagerJava : Task
	{
		const string EnvironmentFileName = "XamarinAndroidEnvironmentVariables.java";

		Guid buildId = Guid.NewGuid ();

		[Required]
		public ITaskItem[] ResolvedAssemblies { get; set; }

		[Required]
		public ITaskItem[] ResolvedUserAssemblies { get; set; }

		[Required]
		public string OutputDirectory { get; set; }

		[Required]
		public string EnvironmentOutputDirectory { get; set; }

		[Required]
		public string UseSharedRuntime { get; set; }

		[Required]
		public string MainAssembly { get; set; }

		[Required]
		public string TargetFrameworkVersion { get; set; }

		[Required]
		public string Manifest { get; set; }

		public string Debug { get; set; }
		public ITaskItem[] Environments { get; set; }
		public string AndroidAotMode { get; set; }
		public bool EnableLLVM { get; set; }
		public string HttpClientHandlerType { get; set; }
		public string TlsProvider { get; set; }
		public string AndroidSequencePointsMode { get; set; }
		public bool EnableSGenConcurrent { get; set; }

		[Output]
		public string BuildId { get; set; }

		bool _Debug {
			get {
				return string.Equals (Debug, "true", StringComparison.OrdinalIgnoreCase);
			}
		}

		public override bool Execute ()
		{
			BuildId = buildId.ToString ();
			Log.LogDebugMessage ("  [Output] BuildId: {0}", BuildId);

			var shared_runtime = string.Compare (UseSharedRuntime, "true", true) == 0;
			var doc = AndroidAppManifest.Load (Manifest, MonoAndroidHelper.SupportedVersions);
			int minApiVersion = doc.MinSdkVersion == null ? 4 : (int) doc.MinSdkVersion;
			// We need to include any special assemblies in the Assemblies list
			var assemblies = ResolvedUserAssemblies.Select (p => p.ItemSpec)
				.Concat (MonoAndroidHelper.GetFrameworkAssembliesToTreatAsUserAssemblies (ResolvedAssemblies))						
				.ToList ();
			var mainFileName = Path.GetFileName (MainAssembly);
			Func<string,string,bool> fileNameEq = (a,b) => a.Equals (b, StringComparison.OrdinalIgnoreCase);
			assemblies = assemblies.Where (a => fileNameEq (a, mainFileName)).Concat (assemblies.Where (a => !fileNameEq (a, mainFileName))).ToList ();

			using (var stream = new MemoryStream ())
			using (var pkgmgr = new StreamWriter (stream)) {
				// Write the boilerplate from the MonoPackageManager.java resource
				using (var template = new StreamReader (Assembly.GetExecutingAssembly ().GetManifestResourceStream ("MonoPackageManager.java"))) {
					string line;
					while ((line = template.ReadLine ()) != null) {
						pkgmgr.WriteLine (line);
					}
				}

				// Write all the user assemblies
				pkgmgr.WriteLine ("class MonoPackageManager_Resources {");
				pkgmgr.WriteLine ("\tpublic static final String[] Assemblies = new String[]{");

				pkgmgr.WriteLine ("\t\t/* We need to ensure that \"{0}\" comes first in this list. */", mainFileName);
				foreach (var assembly in assemblies) {
					pkgmgr.WriteLine ("\t\t\"" + Path.GetFileName (assembly) + "\",");
				}

				// Write the assembly dependencies
				pkgmgr.WriteLine ("\t};");
				pkgmgr.WriteLine ("\tpublic static final String[] Dependencies = new String[]{");

				//foreach (var assembly in assemblies.Except (args.Assemblies)) {
				//        if (args.SharedRuntime && !Toolbox.IsInSharedRuntime (assembly))
				//                pkgmgr.WriteLine ("\t\t\"" + Path.GetFileName (assembly) + "\",");
				//}

				pkgmgr.WriteLine ("\t};");

				// Write the platform api apk we need
				pkgmgr.WriteLine ("\tpublic static final String ApiPackageName = {0};", shared_runtime
						? string.Format ("\"Mono.Android.Platform.ApiLevel_{0}\"",
							MonoAndroidHelper.SupportedVersions.GetApiLevelFromFrameworkVersion (TargetFrameworkVersion))
						: "null");
				pkgmgr.WriteLine ("}");
				pkgmgr.Flush ();

				// Only copy to the real location if the contents actually changed
				var dest = Path.GetFullPath (Path.Combine (OutputDirectory, "MonoPackageManager.java"));

				MonoAndroidHelper.CopyIfStreamChanged (stream, dest);
			}

			AddEnvironment ();

			return !Log.HasLoggedErrors;
		}

		static readonly string[] defaultLogLevel = {"MONO_LOG_LEVEL", "info"};
		static readonly string[] defaultMonoDebug = {"MONO_DEBUG", "gen-compact-seq-points"};
		static readonly string[] defaultHttpMessageHandler = {"XA_HTTP_CLIENT_HANDLER_TYPE", "System.Net.Http.HttpClientHandler, System.Net.Http"};
		static readonly string[] defaultTlsProvider = {"XA_TLS_PROVIDER", "btls"};

		void AddEnvironment ()
		{
			var environment = new StringWriter () {
				NewLine = "\n",
			};

			if (EnableLLVM) {
				WriteEnvironment ("mono.llvm", "true");
			}

			AotMode aotMode;
			if (AndroidAotMode != null && Aot.GetAndroidAotMode (AndroidAotMode, out aotMode)) {
				WriteEnvironment ("mono.aot", aotMode.ToString ().ToLowerInvariant());
			}

			bool haveLogLevel = false;
			bool haveMonoDebug = false;
			bool havebuildId = false;
			bool haveHttpMessageHandler = false;
			bool haveTlsProvider = false;
			bool haveMonoGCParams = false;

			SequencePointsMode sequencePointsMode;
			if (!Aot.TryGetSequencePointsMode (AndroidSequencePointsMode, out sequencePointsMode))
				sequencePointsMode = SequencePointsMode.None;

			foreach (ITaskItem env in Environments ?? new TaskItem[0]) {
				environment.WriteLine ("\t\t// Source File: {0}", env.ItemSpec);
				foreach (string line in File.ReadLines (env.ItemSpec)) {
					var lineToWrite = line;
					if (lineToWrite.StartsWith ("MONO_LOG_LEVEL=", StringComparison.Ordinal))
						haveLogLevel = true;
					if (lineToWrite.StartsWith ("MONO_GC_PARAMS=", StringComparison.Ordinal))
						haveMonoGCParams = true;
					if (lineToWrite.StartsWith ("XAMARIN_BUILD_ID=", StringComparison.Ordinal))
						havebuildId = true;
					if (lineToWrite.StartsWith ("MONO_DEBUG=", StringComparison.Ordinal)) {
						haveMonoDebug = true;
						if (sequencePointsMode != SequencePointsMode.None && !lineToWrite.Contains ("gen-compact-seq-points"))
							lineToWrite = line  + ",gen-compact-seq-points";
					}
					if (lineToWrite.StartsWith ("XA_HTTP_CLIENT_HANDLER_TYPE=", StringComparison.Ordinal))
						haveHttpMessageHandler = true;
					if (lineToWrite.StartsWith ("XA_TLS_PROVIDER=", StringComparison.Ordinal))
						haveTlsProvider = true;
					WriteEnvironmentLine (lineToWrite);
				}
			}

			if (_Debug && !haveLogLevel) {
				WriteEnvironment (defaultLogLevel[0], defaultLogLevel[1]);
			}

			if (sequencePointsMode != SequencePointsMode.None && !haveMonoDebug) {
				WriteEnvironment (defaultMonoDebug[0], defaultMonoDebug[1]);
			}

			if (!havebuildId)
				WriteEnvironment ("XAMARIN_BUILD_ID", BuildId);

			if (!haveHttpMessageHandler) {
				if (HttpClientHandlerType == null)
					WriteEnvironment (defaultHttpMessageHandler[0], defaultHttpMessageHandler[1]);
				else
					WriteEnvironment ("XA_HTTP_CLIENT_HANDLER_TYPE", HttpClientHandlerType.Trim ());
			}

			if (!haveTlsProvider) {
				if (TlsProvider == null)
					WriteEnvironment (defaultTlsProvider[0], defaultTlsProvider[1]);
				else
					WriteEnvironment ("XA_TLS_PROVIDER", TlsProvider.Trim ());
			}

			if (!haveMonoGCParams) {
				if (EnableSGenConcurrent)
					WriteEnvironment ("MONO_GC_PARAMS", "major=marksweep-conc");
				else
					WriteEnvironment ("MONO_GC_PARAMS", "major=marksweep");
			}

			string environmentTemplate;
			using (var sr = new StreamReader (typeof (BuildApk).Assembly.GetManifestResourceStream (EnvironmentFileName))) {
				environmentTemplate = sr.ReadToEnd ();
			}

			using (var ms = new MemoryStream ()) {
				using (var sw = new StreamWriter (ms)) {
					sw.Write (environmentTemplate.Replace ("//@ENVVARS@", environment.ToString ()));
					sw.Flush ();

					string dest = Path.GetFullPath (Path.Combine (EnvironmentOutputDirectory, EnvironmentFileName));
					MonoAndroidHelper.CopyIfStreamChanged (ms, dest);
				}
			}

			void WriteEnvironment (string name, string value)
			{
				environment.WriteLine ($"\t\t\"{ValidJavaString (name)}\", \"{ValidJavaString (value)}\",");
			}

			void WriteEnvironmentLine (string line)
			{
				if (String.IsNullOrEmpty (line))
					return;

				string[] nv = line.Split (new char[]{'='}, 2);
				WriteEnvironment (nv[0].Trim (), nv.Length < 2 ? String.Empty : nv[1].Trim ());
			}

			string ValidJavaString (string s)
			{
				return s.Replace ("\"", "\\\"");
			}
		}
	}
}

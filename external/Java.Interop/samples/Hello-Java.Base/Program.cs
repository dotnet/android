using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Threading;

using Mono.Options;

using Java.Interop;

namespace Hello
{
	class App
	{
		const int N = 1000000;

		public static void Main (string[] args)
		{
			var     logger              = (Action<TraceLevel, string>?) null;
			string? jvmPath             = null;
			bool    createMultipleVMs   = false;
			bool    reportTiming        = false;
			bool    showHelp            = false;
			int     verbosity           = 0;
			var options = new OptionSet () {
				"Using the JVM from C#!",
				"",
				"Options:",
				{ "jvm=",
				  $"{{PATH}} to JVM to use.  Default is:\n{Java.InteropTests.TestJVM.GetJvmLibraryPath (logger)}",
				  v => jvmPath = v },
				{ "m",
				  "Create multiple Java VMs.  This will likely crash.",
				  v => createMultipleVMs = v != null },
				{ "t",
				  $"Timing; invoke Object.hashCode() {N} times, print average.",
				  v => reportTiming = v != null },
				{ "v|verbosity:",
				  $"Set console log verbosity to {{LEVEL}}.  Default is 0.",
				  (int? v) => verbosity = v.HasValue ? v.Value : verbosity + 1 },
				{ "h|help",
				  "Show this message and exit.",
				  v => showHelp = v != null },
			};
			options.Parse (args);
			if (showHelp) {
				options.WriteOptionDescriptions (Console.Out);
				return;
			}
			if (verbosity > 0) {
				logger = CreateConsoleLogger ();
			}
			var builder = new JreRuntimeOptions () {
				JniAddNativeMethodRegistrationAttributePresent  = true,
				JvmLibraryPath                                  = jvmPath ?? global::Java.InteropTests.TestJVM.GetJvmLibraryPath (logger),
				ClassPath   = {
					Path.Combine (Path.GetDirectoryName (typeof (App).Assembly.Location)!, "Hello-Java.Base.jar"),
				},
				TypeMappings = {
					[MyJLO.JniTypeName]     = typeof (MyJLO),
				},
			};
			builder.AddOption ("-Xcheck:jni");

			var jvm = builder.CreateJreVM ();

			if (reportTiming) {
				ReportTiming ();
				return;
			}

			if (createMultipleVMs) {
				CreateAnotherJVM ();
				return;
			}

			CreateJLO ();

			GC.Collect ();
			GC.Collect ();
			GC.WaitForPendingFinalizers ();
			GC.WaitForPendingFinalizers ();
		}

		static Action<TraceLevel, string> CreateConsoleLogger ()
		{
			return (level, message) => {
				Console.WriteLine ($"# {level}: {message}");
			};
		}

		static void CreateJLO ()
		{
			var jlo = new MyJLO ();
			Console.WriteLine ($"binding? {jlo.ToString ()}");
		}

		static void ReportTiming ()
		{
			var jlo = new Java.Lang.Object ();
			var t = Stopwatch.StartNew ();
			for (int i = 0; i < N; ++i) {
				jlo.GetHashCode ();
			}
			t.Stop ();
			Console.WriteLine ($"Object.hashCode: {N} invocations. Total={t.Elapsed}; Average={t.Elapsed.TotalMilliseconds / (double) N}ms");
		}

		static unsafe void CreateAnotherJVM ()
		{
			Console.WriteLine ("Part 2!");
			using (var vm = new JreRuntimeOptions ().CreateJreVM ()) {
				Console.WriteLine ("# JniEnvironment.EnvironmentPointer={0}", JniEnvironment.EnvironmentPointer);
				Console.WriteLine ("vm.SafeHandle={0}", vm.InvocationPointer);
				var t = new JniType ("java/lang/Object");
				var c = t.GetConstructor ("()V");
				var o = t.NewObject (c, null);
				var m = t.GetInstanceMethod ("hashCode", "()I");
				int i = JniEnvironment.InstanceMethods.CallIntMethod (o, m);
				Console.WriteLine ("java.lang.Object={0}", o);
				Console.WriteLine ("hashcode={0}", i);
				JniObjectReference.Dispose (ref o);
				t.Dispose ();
				// var o = JniTypes.FindClass ("java/lang/Object");
				/*
				var waitForCreation = new CountdownEvent (1);
				var exitThread = new CountdownEvent (1);
				var t = new Thread (() => {
					var vm2 = new JavaVMBuilder ().CreateJavaVM ();
					waitForCreation.Signal ();
					exitThread.Wait ();
				});
				t.Start ();
				waitForCreation.Wait ();
				*/
				foreach (var h in vm.GetAvailableInvocationPointers ()) {
					Console.WriteLine ("WITHIN: GetCreatedJavaVMs: {0}", h);
				}
				// exitThread.Signal ();
			}
			foreach (var h in JniRuntime.GetRegisteredRuntimes ()) {
				Console.WriteLine ("POST: GetCreatedJavaVMs: {0}", h);
			}
		}
	}

	[JniTypeSignature (JniTypeName)]
	class MyJLO : Java.Lang.Object {
		internal    const   string      JniTypeName = "net/dot/jni/sample/MyJLO";
	}
}

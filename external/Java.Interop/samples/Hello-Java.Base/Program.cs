using System;
using System.Diagnostics;
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
			string? jvmPath             = global::Java.InteropTests.TestJVM.GetJvmLibraryPath ();
			bool    createMultipleVMs   = false;
			bool    reportTiming        = false;
			bool    showHelp            = false;
			var options = new OptionSet () {
				"Using the JVM from C#!",
				"",
				"Options:",
				{ "jvm=",
				  $"{{PATH}} to JVM to use.  Default is:\n  {jvmPath}",
				  v => jvmPath = v },
				{ "m",
				  "Create multiple Java VMs.  This will likely creash.",
				  v => createMultipleVMs = v != null },
				{ "t",
				  $"Timing; invoke Object.hashCode() {N} times, print average.",
				  v => reportTiming = v != null },
				{ "h|help",
				  "Show this message and exit.",
				  v => showHelp = v != null },
			};
			options.Parse (args);
			if (showHelp) {
				options.WriteOptionDescriptions (Console.Out);
				return;
			}
			var builder = new JreRuntimeOptions () {
				JniAddNativeMethodRegistrationAttributePresent  = true,
				JvmLibraryPath                                  = jvmPath,
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
		}

		static void CreateJLO ()
		{
			var jlo = new Java.Lang.Object ();
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
				foreach (var h in JniRuntime.GetAvailableInvocationPointers ()) {
					Console.WriteLine ("WITHIN: GetCreatedJavaVMs: {0}", h);
				}
				// exitThread.Signal ();
			}
			foreach (var h in JniRuntime.GetAvailableInvocationPointers ()) {
				Console.WriteLine ("POST: GetCreatedJavaVMs: {0}", h);
			}
		}
	}
}

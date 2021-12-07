using System;
using System.Threading;

using Mono.Options;

using Java.Interop;

namespace Hello
{
	class App
	{
		public static void Main (string[] args)
		{
			string? jvmPath             = global::Java.InteropTests.TestJVM.GetJvmLibraryPath ();
			bool    createMultipleVMs   = false;
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
				{ "h|help",
				  "Show this message and exit.",
				  v => showHelp = v != null },
			};
			options.Parse (args);
			if (showHelp) {
				options.WriteOptionDescriptions (Console.Out);
				return;
			}
			Console.WriteLine ("Hello World!");
			var builder = new JreRuntimeOptions () {
				JniAddNativeMethodRegistrationAttributePresent  = true,
				JvmLibraryPath                                  = jvmPath,
			};
			builder.AddOption ("-Xcheck:jni");
			var jvm = builder.CreateJreVM ();
			Console.WriteLine ($"JniRuntime.CurrentRuntime == jvm? {ReferenceEquals (JniRuntime.CurrentRuntime, jvm)}");
			foreach (var h in JniRuntime.GetAvailableInvocationPointers ()) {
				Console.WriteLine ("PRE: GetCreatedJavaVMHandles: {0}", h);
			}

			CreateJLO ();

			if (createMultipleVMs) {
				CreateAnotherJVM ();
			}
		}

		static void CreateJLO ()
		{
			var jlo = new Java.Lang.Object ();
			Console.WriteLine ($"binding? {jlo.ToString ()}");
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

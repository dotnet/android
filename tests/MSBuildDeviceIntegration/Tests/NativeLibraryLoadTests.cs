using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Android.Tasks;
using Microsoft.Build.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.Combinatorial;
using Xamarin.Android.Tasks;
using Xamarin.Android.Tools;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests;

[TestClass]
[TestCategory ("UsesDevice")]
public class NativeLibraryLoadTests : DeviceTest
{
	[TestMethod]
	public void UnknownSystemLibraryLoadsWithoutMainThreadDispatch ()
	{
		if (IgnoreUnsupportedConfiguration (AndroidRuntime.CoreCLR, release: false)) {
			return;
		}

		const string completedMessage = "UNKNOWN_SYSTEM_LIBRARY_LOAD_COMPLETED";
		const string timeoutMessage = "UNKNOWN_SYSTEM_LIBRARY_LOAD_TIMED_OUT";
		string packageName = PackageUtils.MakePackageName (AndroidRuntime.CoreCLR, "unknownsystemlibrary");
		var proj = new XamarinAndroidApplicationProject (packageName: packageName) {
			EmbedAssembliesIntoApk = true,
			ProjectName = "UnknownSystemLibrary",
		};
		proj.SetRuntime (AndroidRuntime.CoreCLR);
		proj.SetRuntimeIdentifiers ([DeviceAbi]);
		proj.SetDefaultTargetDevice ();
		proj.MainActivity = proj.DefaultMainActivity
			.Replace ("//${USINGS}", "using System;\nusing System.Runtime.InteropServices;\nusing System.Threading;\nusing System.Threading.Tasks;")
			.Replace (
				"//${AFTER_ONCREATE}",
				$$"""
			// Blocking the main thread here verifies CoreCLR directly loads libraries missing from the
			// build-time DSO cache instead of dispatching System.loadLibrary to the main thread.
			var mainThread = Thread.CurrentThread;
			var loadTask = Task.Run (() => {
				if (!SpinWait.SpinUntil (() => (mainThread.ThreadState & ThreadState.WaitSleepJoin) != 0, TimeSpan.FromSeconds (20))) {
					throw new InvalidOperationException ("The main thread did not enter the wait.");
				}
				return NativeMethods.GetError ();
			});
			if (!loadTask.Wait (TimeSpan.FromSeconds (30))) {
				Android.Util.Log.Error ("NativeLibraryLoadTest", "{{timeoutMessage}}");
				return;
			}
			Android.Util.Log.Info ("NativeLibraryLoadTest", "{{completedMessage}}");
"""
			)
			.Replace (
				"//${AFTER_MAINACTIVITY}",
				"""
	static class NativeMethods
	{
		[DllImport ("libGLESv2", EntryPoint = "glGetError")]
		public static extern uint GetError ();
	}
"""
			);

		using var builder = CreateApkBuilder (packageName: packageName);
		Assert.IsTrue (builder.Install (proj), "Project should have installed.");

		ClearAdbLogcat ();
		bool loadTimedOut = false;
		bool appCompleted = MonitorAdbLogcat (
			line => {
				loadTimedOut |= line.Contains (timeoutMessage, StringComparison.Ordinal);
				return loadTimedOut || line.Contains (completedMessage, StringComparison.Ordinal);
			},
			Path.Combine (Root, builder.ProjectDirectory, "unknown-system-library-load.log"),
			timeout: 60,
			onMonitoringStarted: () => StartActivityAndAssert (proj)
		);

		Assert.IsFalse (loadTimedOut, "The worker P/Invoke waited for the blocked Android main thread.");
		Assert.IsTrue (appCompleted, $"Output did not contain {completedMessage}.");
	}

	[TestMethod]
	[CombinatorialData]
	public void ExemptJniLibraryLoadsOnWorkerThreadWithBlockedMainThread (bool extractNativeLibs)
	{
		if (IgnoreUnsupportedConfiguration (AndroidRuntime.CoreCLR, release: true)) {
			return;
		}

		const string completedMessage = "WORKER_JNI_LIBRARY_LOAD_COMPLETED:115:115:73:10:99:1:1:1";
		const string failedMessage = "WORKER_JNI_LIBRARY_LOAD_FAILED:";
		const string progressMessage = "WORKER_JNI_LIBRARY_LOAD_PROGRESS:";
		string packageName = PackageUtils.MakePackageName (
			AndroidRuntime.CoreCLR, $"workerjniload{(extractNativeLibs ? "extracted" : "embedded")}");
		var proj = new XamarinAndroidApplicationProject (packageName: packageName) {
			IsRelease = true,
			ProjectName = "WorkerJniLibraryLoad",
		};
		proj.SetRuntime (AndroidRuntime.CoreCLR);
		proj.SetRuntimeIdentifiers ([DeviceAbi]);
		proj.SetDefaultTargetDevice ();
		proj.SetProperty ("AllowUnsafeBlocks", "true");
		proj.SetProperty ("AndroidLinkTool", "r8");
		proj.SetProperty ("PublishReadyToRun", "false");
		proj.AndroidManifest = proj.AndroidManifest.Replace (
			"<application ", $"<application android:extractNativeLibs=\"{extractNativeLibs.ToString ().ToLowerInvariant ()}\" ");

		using var builder = CreateApkBuilder (packageName: packageName);
		string nativeWorkDir = Path.Combine (Root, builder.ProjectDirectory, "native-fixtures");
		Directory.CreateDirectory (nativeWorkDir);

		byte [] outerLibrary = CompileJniFixtureLibrary (
			nativeWorkDir,
			"libjniworkerouter.so",
			"""
			#include <jni.h>

			static volatile jint outer_loaded = 0;

			JNIEXPORT jint JNICALL
			JNI_OnLoad (JavaVM *vm, void *reserved)
			{
				(void)reserved;
				JNIEnv *env = NULL;
				if ((*vm)->GetEnv (vm, (void**)&env, JNI_VERSION_1_6) != JNI_OK || env == NULL) {
					return JNI_ERR;
				}

				jclass probe = (*env)->FindClass (env, "com/xamarin/jniloadertest/JniLoadProbe");
				if (probe == NULL) {
					return JNI_ERR;
				}

				jmethodID on_outer_load = (*env)->GetStaticMethodID (env, probe, "onOuterLoad", "()I");
				if (on_outer_load == NULL) {
					(*env)->DeleteLocalRef (env, probe);
					return JNI_ERR;
				}

				outer_loaded = 10;
				jint inner_value = (*env)->CallStaticIntMethod (env, probe, on_outer_load);
				(*env)->DeleteLocalRef (env, probe);
				if ((*env)->ExceptionCheck (env) || inner_value != 73) {
					return JNI_ERR;
				}

				outer_loaded = 42 + inner_value;
				return JNI_VERSION_1_6;
			}

			JNIEXPORT int JNICALL
			jni_worker_outer_get_value (void)
			{
				return (int)outer_loaded;
			}
			""");

		byte [] innerLibrary = CompileJniFixtureLibrary (
			nativeWorkDir,
			"libjniworkerinner.so",
			"""
			#include <jni.h>

			static jint inner_loaded = 0;

			JNIEXPORT jint JNICALL
			JNI_OnLoad (JavaVM *vm, void *reserved)
			{
				(void)reserved;
				JNIEnv *env = NULL;
				if ((*vm)->GetEnv (vm, (void**)&env, JNI_VERSION_1_6) != JNI_OK || env == NULL) {
					return JNI_ERR;
				}

				jclass probe = (*env)->FindClass (env, "com/xamarin/jniloadertest/JniLoadProbe");
				if (probe == NULL) {
					return JNI_ERR;
				}

				jmethodID on_inner_load = (*env)->GetStaticMethodID (env, probe, "onInnerLoad", "()I");
				if (on_inner_load == NULL) {
					(*env)->DeleteLocalRef (env, probe);
					return JNI_ERR;
				}

				jint value = (*env)->CallStaticIntMethod (env, probe, on_inner_load);
				(*env)->DeleteLocalRef (env, probe);
				if ((*env)->ExceptionCheck (env) || value != 73) {
					return JNI_ERR;
				}

				inner_loaded = value;
				return JNI_VERSION_1_6;
			}

			JNIEXPORT int JNICALL
			jni_worker_inner_get_value (void)
			{
				return (int)inner_loaded;
			}
			""");

		byte [] failLibrary = CompileJniFixtureLibrary (
			nativeWorkDir,
			"libjniworkerfail.dll.so",
			"""
			#include <jni.h>

			static volatile jint fail_loaded = 0;

			JNIEXPORT jint JNICALL
			JNI_OnLoad (JavaVM *vm, void *reserved)
			{
				(void)reserved;
				JNIEnv *env = NULL;
				if ((*vm)->GetEnv (vm, (void**)&env, JNI_VERSION_1_6) != JNI_OK || env == NULL) {
					return JNI_ERR;
				}

				jclass probe = (*env)->FindClass (env, "com/xamarin/jniloadertest/JniLoadProbe");
				if (probe == NULL) {
					return JNI_ERR;
				}

				jmethodID on_fail_load = (*env)->GetStaticMethodID (env, probe, "onFailLoad", "()I");
				if (on_fail_load == NULL) {
					(*env)->DeleteLocalRef (env, probe);
					return JNI_ERR;
				}

				fail_loaded = 99;
				jint recursive_value = (*env)->CallStaticIntMethod (env, probe, on_fail_load);
				(*env)->DeleteLocalRef (env, probe);
				if ((*env)->ExceptionCheck (env) || recursive_value != 99) {
					return JNI_ERR;
				}

				return JNI_ERR;
			}

			JNIEXPORT int JNICALL
			jni_worker_fail_get_value (void)
			{
				return (int)fail_loaded;
			}
			""");

		proj.OtherBuildItems.Add (new AndroidItem.AndroidJavaSource ("JniLoadProbe.java") {
			Encoding = new UTF8Encoding (encoderShouldEmitUTF8Identifier: false),
			TextContent = () => """
			package com.xamarin.jniloadertest;

			public final class JniLoadProbe {
				public static volatile int outerOnLoadCount;
				public static volatile int innerOnLoadCount;
				public static volatile int failOnLoadCount;

				public static native int invokeInnerFromManaged ();
				public static native int invokeFailFromManaged ();

				public static int onOuterLoad () {
					outerOnLoadCount++;
					return invokeInnerFromManaged ();
				}

				public static int onInnerLoad () {
					innerOnLoadCount++;
					return 73;
				}

				public static int onFailLoad () {
					failOnLoadCount++;
					return invokeFailFromManaged ();
				}

				// Diagnostics only: Java stacks remain available even if managed code cannot run.
				static volatile boolean finished;

				public static void startWatchdog (final long delayMillis) {
					Thread watchdog = new Thread (new Runnable () {
						public void run () {
							try {
								Thread.sleep (delayMillis);
							} catch (InterruptedException e) {
								return;
							}
							if (!finished) {
								dumpThreads ("Watchdog");
							}
						}
					}, "JniLoadWatchdog");
					watchdog.setDaemon (true);
					watchdog.start ();
				}

				public static void finish () {
					finished = true;
				}

				public static void dumpThreads (String reason) {
					String prefix = "WORKER_JNI_LIBRARY_LOAD_DIAGNOSTIC:" + reason + ":";
					for (java.util.Map.Entry<Thread, StackTraceElement[]> entry : Thread.getAllStackTraces ().entrySet ()) {
						Thread thread = entry.getKey ();
						android.util.Log.w ("NativeLibraryLoadTest", prefix + "thread '" + thread.getName () + "' " + thread.getState ());
						for (StackTraceElement frame : entry.getValue ()) {
							android.util.Log.w ("NativeLibraryLoadTest", prefix + "  at " + frame);
						}
					}
					android.util.Log.w ("NativeLibraryLoadTest", prefix + "end");
				}
			}
			""",
		});

		foreach ((string fileName, byte [] content) in new [] {
			("libjniworkerouter.so", outerLibrary),
			("libjniworkerinner.so", innerLibrary),
			("libjniworkerfail.dll.so", failLibrary),
		}) {
			proj.OtherBuildItems.Add (new AndroidItem.AndroidNativeLibrary ($"native/{DeviceAbi}/{fileName}") {
				BinaryContent = () => content,
				MetadataValues = $"Link={DeviceAbi}/{fileName}",
			});
			proj.OtherBuildItems.Add (new AndroidItem.AndroidNativeLibraryNoJniPreload (fileName));
		}

		proj.MainActivity = proj.DefaultMainActivity
			.Replace ("//${USINGS}", "using System;\nusing System.Runtime.InteropServices;\nusing System.Threading;\nusing Java.Interop;")
			.Replace (
				"//${AFTER_ONCREATE}",
				$$"""
				try {
					NativeMethods.Progress ("OnCreate");
					using var probeType = new JniType ("com/xamarin/jniloadertest/JniLoadProbe");
					unsafe {
						var watchdogArgs = stackalloc JniArgumentValue [1];
						watchdogArgs [0] = new JniArgumentValue (65_000L);
						JniEnvironment.StaticMethods.CallStaticVoidMethod (probeType.PeerReference, probeType.GetStaticMethod ("startWatchdog", "(J)V"), watchdogArgs);
						Span<JniNativeMethod> methods = stackalloc JniNativeMethod [2];
						fixed (byte* innerNamePtr = "invokeInnerFromManaged"u8)
						fixed (byte* failNamePtr = "invokeFailFromManaged"u8)
						fixed (byte* sigPtr = "()I"u8) {
							methods [0] = new JniNativeMethod (innerNamePtr, sigPtr, (IntPtr) (delegate* unmanaged<IntPtr, IntPtr, int>) &NativeMethods.InvokeInnerFromManaged);
							methods [1] = new JniNativeMethod (failNamePtr, sigPtr, (IntPtr) (delegate* unmanaged<IntPtr, IntPtr, int>) &NativeMethods.InvokeFailFromManaged);
							JniEnvironment.Types.RegisterNatives (probeType.PeerReference, methods);
						}
					}
					NativeMethods.Progress ("NativesRegistered");

					var outerCountField = probeType.GetStaticField ("outerOnLoadCount", "I");
					var innerCountField = probeType.GetStaticField ("innerOnLoadCount", "I");
					var failCountField = probeType.GetStaticField ("failOnLoadCount", "I");
					int initialOuterCount = JniEnvironment.StaticFields.GetStaticIntField (probeType.PeerReference, outerCountField);
					int initialInnerCount = JniEnvironment.StaticFields.GetStaticIntField (probeType.PeerReference, innerCountField);
					int initialFailCount = JniEnvironment.StaticFields.GetStaticIntField (probeType.PeerReference, failCountField);
					if (initialOuterCount != 0 || initialInnerCount != 0 || initialFailCount != 0) {
						Android.Util.Log.Error ("NativeLibraryLoadTest", $"{{failedMessage}}Libraries were preloaded ({initialOuterCount}, {initialInnerCount}, {initialFailCount})");
						return;
					}

					int workerValue = 0;
					Exception? workerException = null;
					var worker = new Thread (() => {
						try {
							NativeMethods.Progress ("OuterPInvokeStarting");
							workerValue = NativeMethods.GetOuterValue ();
							NativeMethods.Progress ("OuterPInvokeReturned");
							if (NativeMethods.ConcurrentOuterThread == null || !NativeMethods.ConcurrentOuterThread.Join (TimeSpan.FromSeconds (5))) {
								throw new TimeoutException ("Concurrent outer reader thread did not finish.");
							}
							if (NativeMethods.ConcurrentOuterException != null) {
								throw NativeMethods.ConcurrentOuterException;
							}
							if (NativeMethods.CompetingInnerThread == null || !NativeMethods.CompetingInnerThread.Join (TimeSpan.FromSeconds (5))) {
								throw new TimeoutException ("Competing inner reader thread did not finish.");
							}
							if (NativeMethods.CompetingInnerException != null) {
								throw NativeMethods.CompetingInnerException;
							}
							NativeMethods.Progress ("ReadersJoined");

							bool firstFailThrew = false;
							try {
								NativeMethods.GetFailValue ();
							} catch (DllNotFoundException) {
								firstFailThrew = true;
							}
							if (!firstFailThrew) {
								throw new InvalidOperationException ("Initial P/Invoke to library with failing JNI_OnLoad should throw DllNotFoundException.");
							}

							bool secondFailThrew = false;
							try {
								NativeMethods.GetFailValueAfterFailure ();
							} catch (DllNotFoundException) {
								secondFailThrew = true;
							}
							if (!secondFailThrew) {
								throw new InvalidOperationException ("Subsequent P/Invoke to library with failing JNI_OnLoad should throw DllNotFoundException.");
							}
							NativeMethods.Progress ("FailingPInvokesCompleted");
						} catch (Exception ex) {
							workerException = NativeMethods.CallbackError == null ? ex : new InvalidOperationException (NativeMethods.CallbackError, ex);
						}
					}) {
						IsBackground = true,
						Name = "WorkerJniLoader",
					};
					worker.Start ();
					NativeMethods.Progress ("WorkerStarted");
					if (!worker.Join (TimeSpan.FromSeconds (60))) {
						NativeMethods.DumpJavaThreads ("WorkerJoinTimedOut");
						Android.Util.Log.Error ("NativeLibraryLoadTest", "{{failedMessage}}TimedOut");
						return;
					}
					if (workerException != null) {
						Android.Util.Log.Error ("NativeLibraryLoadTest", "{{failedMessage}}" + workerException);
						return;
					}

					int finalOuterCount = JniEnvironment.StaticFields.GetStaticIntField (probeType.PeerReference, outerCountField);
					int finalInnerCount = JniEnvironment.StaticFields.GetStaticIntField (probeType.PeerReference, innerCountField);
					int finalFailCount = JniEnvironment.StaticFields.GetStaticIntField (probeType.PeerReference, failCountField);
					Android.Util.Log.Info (
						"NativeLibraryLoadTest",
						$"WORKER_JNI_LIBRARY_LOAD_COMPLETED:{workerValue}:{NativeMethods.ConcurrentOuterValue}:{NativeMethods.CompetingInnerValue}:{NativeMethods.ProvisionalOuterValue}:{NativeMethods.ProvisionalFailValue}:{finalOuterCount}:{finalInnerCount}:{finalFailCount}");
				} catch (Exception ex) {
					Android.Util.Log.Error ("NativeLibraryLoadTest", "{{failedMessage}}" + ex);
				} finally {
					NativeMethods.FinishDiagnostics ();
				}
				""")
			.Replace (
				"//${AFTER_MAINACTIVITY}",
				"""
				static class NativeMethods
				{
					public static volatile int ProvisionalOuterValue;
					public static volatile int ProvisionalFailValue;
					public static volatile int ConcurrentOuterValue;
					public static volatile int CompetingInnerValue;
					public static volatile string? CallbackError;
					public static volatile Exception? ConcurrentOuterException;
					public static volatile Exception? CompetingInnerException;
					public static Thread? ConcurrentOuterThread;
					public static Thread? CompetingInnerThread;

					[UnmanagedCallersOnly]
					public static int InvokeInnerFromManaged (IntPtr jnienv, IntPtr klass)
					{
						Progress ("OuterOnLoadCallback");
						// Use a distinct P/Invoke declaration so CoreCLR resolves through PinvokeOverride::load_symbol ->
						// MonodroidDl::monodroid_dlopen during JNI_OnLoad and exercises same-thread provisional handle lookup.
						ProvisionalOuterValue = GetOuterValueProvisional ();

						using var started = new ManualResetEventSlim (false);
						Java.Lang.Thread? outerJavaThread = null;
						ConcurrentOuterThread = new Thread (() => {
							try {
								try {
									outerJavaThread = Java.Lang.Thread.CurrentThread ();
								} finally {
									started.Set ();
								}
								Progress ("ConcurrentOuterPInvokeStarting");
								ConcurrentOuterValue = GetOuterValueConcurrent ();
								Progress ("ConcurrentOuterPInvokeReturned");
							} catch (Exception ex) {
								ConcurrentOuterException = ex;
							}
						}) {
							IsBackground = true,
							Name = "ConcurrentOuterReader",
						};
						ConcurrentOuterThread.Start ();
						started.Wait ();
						if (!WaitForJavaLoadBlock (outerJavaThread, ConcurrentOuterThread)) {
							CallbackError ??= "Concurrent outer reader did not block in Java while JNI_OnLoad was active.";
							return -1;
						}
						Progress ("ConcurrentOuterBlocked");

						// Start a competing caller for cold library B while this thread holds the Java Runtime
						// loadLibrary0 monitor inside A's JNI_OnLoad, then P/Invoke B from A's callback.
						using var innerStarted = new ManualResetEventSlim (false);
						Java.Lang.Thread? innerJavaThread = null;
						CompetingInnerThread = new Thread (() => {
							try {
								try {
									innerJavaThread = Java.Lang.Thread.CurrentThread ();
								} finally {
									innerStarted.Set ();
								}
								Progress ("CompetingInnerPInvokeStarting");
								CompetingInnerValue = GetInnerValueConcurrent ();
								Progress ("CompetingInnerPInvokeReturned");
							} catch (Exception ex) {
								CompetingInnerException = ex;
							}
						}) {
							IsBackground = true,
							Name = "CompetingInnerReader",
						};
						CompetingInnerThread.Start ();
						innerStarted.Wait ();
						if (!WaitForJavaLoadBlock (innerJavaThread, CompetingInnerThread)) {
							CallbackError ??= "Competing inner reader did not block in Java while JNI_OnLoad was active.";
							return -1;
						}
						Progress ("CompetingInnerBlocked");

						int innerValue = GetInnerValue ();
						Progress ("InnerPInvokeReturned");
						return innerValue;
					}

					public static void Progress (string step)
					{
						Android.Util.Log.Info ("NativeLibraryLoadTest", "WORKER_JNI_LIBRARY_LOAD_PROGRESS:" + step);
					}

					public static unsafe void DumpJavaThreads (string reason)
					{
						try {
							using var probeType = new JniType ("com/xamarin/jniloadertest/JniLoadProbe");
							var reasonReference = JniEnvironment.Strings.NewString (reason);
							try {
								var args = stackalloc JniArgumentValue [1];
								args [0] = new JniArgumentValue (reasonReference);
								JniEnvironment.StaticMethods.CallStaticVoidMethod (probeType.PeerReference, probeType.GetStaticMethod ("dumpThreads", "(Ljava/lang/String;)V"), args);
							} finally {
								JniObjectReference.Dispose (ref reasonReference);
							}
						} catch (Exception ex) {
							Android.Util.Log.Warn ("NativeLibraryLoadTest", "WORKER_JNI_LIBRARY_LOAD_DIAGNOSTIC:Could not dump Java threads: " + ex);
						}
					}

					public static void FinishDiagnostics ()
					{
						try {
							using var probeType = new JniType ("com/xamarin/jniloadertest/JniLoadProbe");
							JniEnvironment.StaticMethods.CallStaticVoidMethod (probeType.PeerReference, probeType.GetStaticMethod ("finish", "()V"));
						} catch (Exception ex) {
							Android.Util.Log.Warn ("NativeLibraryLoadTest", "WORKER_JNI_LIBRARY_LOAD_DIAGNOSTIC:Could not stop the watchdog: " + ex);
						}
					}

					static bool WaitForJavaLoadBlock (Java.Lang.Thread? javaThread, Thread managedThread)
					{
						if (javaThread == null) {
							return false;
						}
						try {
							bool observed = SpinWait.SpinUntil (
								() => !managedThread.IsAlive || javaThread.GetState ().Equals (Java.Lang.Thread.State.Blocked),
								TimeSpan.FromSeconds (20));
							if (!observed || !managedThread.IsAlive || !javaThread.GetState ().Equals (Java.Lang.Thread.State.Blocked)) {
								return false;
							}
							foreach (var frame in javaThread.GetStackTrace ()) {
								if (frame.ClassName == "java.lang.Runtime" && frame.MethodName == "loadLibrary0") {
									return true;
								}
							}
							return false;
						} catch (Exception ex) {
							CallbackError = "Could not observe the Java loader: " + ex;
							return false;
						}
					}

					[UnmanagedCallersOnly]
					public static int InvokeFailFromManaged (IntPtr jnienv, IntPtr klass)
					{
						Progress ("FailOnLoadCallback");
						ProvisionalFailValue = GetFailValueProvisional ();
						return ProvisionalFailValue;
					}

					[DllImport ("jniworkerouter", EntryPoint = "jni_worker_outer_get_value")]
					public static extern int GetOuterValue ();

					[DllImport ("jniworkerouter", EntryPoint = "jni_worker_outer_get_value")]
					public static extern int GetOuterValueProvisional ();

					[DllImport ("jniworkerouter", EntryPoint = "jni_worker_outer_get_value")]
					public static extern int GetOuterValueConcurrent ();

					[DllImport ("jniworkerinner", EntryPoint = "jni_worker_inner_get_value")]
					public static extern int GetInnerValue ();

					[DllImport ("jniworkerinner", EntryPoint = "jni_worker_inner_get_value")]
					public static extern int GetInnerValueConcurrent ();

					[DllImport ("jniworkerfail", EntryPoint = "jni_worker_fail_get_value")]
					public static extern int GetFailValue ();

					[DllImport ("jniworkerfail", EntryPoint = "jni_worker_fail_get_value")]
					public static extern int GetFailValueProvisional ();

					[DllImport ("jniworkerfail", EntryPoint = "jni_worker_fail_get_value")]
					public static extern int GetFailValueAfterFailure ();
				}
				""");

		Assert.IsTrue (builder.Install (proj), "Project should have built and installed.");
		AssertExtractNativeLibs (builder.Output.GetIntermediaryPath (Path.Combine ("android", "AndroidManifest.xml")), extractNativeLibs);

		ClearAdbLogcat ();
		string? failureLine = null;
		string? lastProgressLine = null;
		bool completed = MonitorAdbLogcat (
			line => {
				if (line.Contains (progressMessage, StringComparison.Ordinal)) {
					lastProgressLine = line;
				}
				if (line.Contains (failedMessage, StringComparison.Ordinal)) {
					failureLine = line;
					return true;
				}
				return line.Contains (completedMessage, StringComparison.Ordinal);
			},
			Path.Combine (Root, builder.ProjectDirectory, "worker-jni-library-load.log"),
			timeout: 90,
			onMonitoringStarted: () => StartActivityAndAssert (proj)
		);

		string progress = "";
		if (failureLine != null || !completed) {
			progress = $" Last progress marker: {lastProgressLine ?? "<none>"}.";
			TestContext.Out.WriteLine (CaptureWorkerJniLibraryLoadDiagnostics (proj.PackageName, Path.Combine (Root, builder.ProjectDirectory)));
		}
		Assert.IsNull (failureLine, $"Worker JNI library load failed: {failureLine}{progress}");
		Assert.IsTrue (completed, $"Output did not contain {completedMessage}.{progress}");
	}

	// Best-effort and bounded: failures of individual commands are reported rather than thrown, so they
	// cannot mask the original assertion. The output goes to the test's standard output because CI task
	// logs keep it for failed first attempts, even when an automatic retry later passes.
	static string CaptureWorkerJniLibraryLoadDiagnostics (string packageName, string outputDirectory)
	{
		const int MaxLogcatLines = 400;
		var sb = new StringBuilder ();
		sb.AppendLine ("===== Worker JNI library load diagnostics =====");

		string? pidOutput = RunDiagnosticAdbCommand (sb, $"shell pidof {packageName}", timeout: 10);
		string pid = pidOutput?.Trim () ?? "";
		if (int.TryParse (pid, NumberStyles.None, CultureInfo.InvariantCulture, out _)) {
			AppendCommandOutput (sb, RunDiagnosticAdbCommand (sb, $"shell ps -T -p {pid} -o TID,S,WCHAN,CMD", timeout: 10));
		} else {
			sb.AppendLine ($"Process {packageName} is not running.");
		}

		string? activities = RunDiagnosticAdbCommand (sb, "shell dumpsys activity activities", timeout: 20);
		if (activities != null) {
			AppendCommandOutput (sb, string.Join (Environment.NewLine, activities.Split ('\n')
				.Where (line => line.Contains (packageName, StringComparison.Ordinal))
				.Take (40)
				.Select (line => line.TrimEnd ())));
		}

		string? logcat = RunDiagnosticAdbCommand (sb, "logcat -d", timeout: 30);
		if (logcat != null) {
			string logcatPath = Path.Combine (outputDirectory, "worker-jni-library-load-diagnostics.log");
			try {
				File.WriteAllText (logcatPath, logcat);
				TestContext.AddTestAttachment (logcatPath);
			} catch (Exception ex) {
				sb.AppendLine ($"Could not save {logcatPath}: {ex.Message}");
			}

			string [] relevant = logcat.Split ('\n')
				.Select (line => line.TrimEnd ())
				.Where (line => line.Contains ("NativeLibraryLoadTest", StringComparison.Ordinal) ||
					line.Contains (packageName, StringComparison.Ordinal) ||
					(pid.Length > 0 && line.Contains ($" {pid} ", StringComparison.Ordinal)) ||
					line.Contains ("Fatal signal", StringComparison.Ordinal) ||
					line.Contains (" F DEBUG", StringComparison.Ordinal) ||
					line.Contains (" E AndroidRuntime", StringComparison.Ordinal))
				.ToArray ();
			sb.AppendLine ($"----- relevant logcat lines ({Math.Min (relevant.Length, MaxLogcatLines)} of {relevant.Length}, most recent) -----");
			foreach (string line in relevant.Skip (Math.Max (0, relevant.Length - MaxLogcatLines))) {
				sb.AppendLine (line);
			}
		}
		sb.AppendLine ("===== End worker JNI library load diagnostics =====");
		return sb.ToString ();
	}

	static string? RunDiagnosticAdbCommand (StringBuilder sb, string command, int timeout)
	{
		sb.AppendLine ($"----- adb {command} -----");
		try {
			var (code, stdOutput, stdError) = RunAdbCommandWithExitCode (command, timeout);
			if (stdOutput == null) {
				sb.AppendLine ($"Diagnostic command timed out after {timeout}s.");
				return null;
			}
			if (code != 0 || !string.IsNullOrWhiteSpace (stdError)) {
				sb.AppendLine ($"Diagnostic command exited with code {code}: {stdError?.Trim ()}");
			}
			return stdOutput;
		} catch (Exception ex) {
			sb.AppendLine ($"Diagnostic command failed: {ex.Message}");
			return null;
		}
	}

	static void AppendCommandOutput (StringBuilder sb, string? output)
	{
		if (!string.IsNullOrWhiteSpace (output)) {
			sb.AppendLine (output.TrimEnd ());
		}
	}

	byte [] CompileJniFixtureLibrary (string directory, string libraryFileName, string source)
	{
		var log = new TaskLoggingHelper (new MockBuildEngine (Console.Out), nameof (NativeLibraryLoadTests));
		var ndk = AndroidNdkTools.Create (AndroidNdkPath, log)
			?? throw new InvalidOperationException ("The configured Android NDK must be available.");
		string clang = ndk.GetToolPath ("clang")
			?? throw new InvalidOperationException ("Unable to locate clang in the configured Android NDK.");

		string targetTriple = DeviceAbi switch {
			"arm64-v8a" => "aarch64-linux-android24",
			"armeabi-v7a" => "armv7a-linux-androideabi24",
			"x86_64" => "x86_64-linux-android24",
			"x86" => "i686-linux-android24",
			_ => throw new NotSupportedException ($"Unsupported device ABI '{DeviceAbi}'."),
		};

		string sourcePath = Path.Combine (directory, Path.ChangeExtension (libraryFileName, ".c"));
		string outputPath = Path.Combine (directory, libraryFileName);
		File.WriteAllText (sourcePath, source, new UTF8Encoding (encoderShouldEmitUTF8Identifier: false));

		using var stdout = new StringWriter (CultureInfo.InvariantCulture);
		using var stderr = new StringWriter (CultureInfo.InvariantCulture);
		var startInfo = ProcessUtils.CreateProcessStartInfo (
			clang,
			$"--target={targetTriple}",
			"-shared",
			"-fPIC",
			"-O2",
			"-Wall",
			"-Wextra",
			"-Werror",
			$"-Wl,-soname,{libraryFileName}",
			"-Wl,--no-undefined",
			sourcePath,
			"-o",
			outputPath
		);
		int exitCode = ProcessUtils.StartProcess (startInfo, stdout, stderr, CancellationToken.None).GetAwaiter ().GetResult ();
		Assert.AreEqual (0, exitCode, $"clang failed for {libraryFileName}: {stderr}{stdout}");
		return File.ReadAllBytes (outputPath);
	}

	[TestMethod]
	[CombinatorialData]
	public void MissingNativeLibraryHasUsefulErrorMessage ([CombinatorialValues (AndroidRuntime.CoreCLR, AndroidRuntime.NativeAOT)] AndroidRuntime runtime)
	{
		bool isRelease = runtime == AndroidRuntime.NativeAOT;
		if (IgnoreUnsupportedConfiguration (runtime, release: isRelease)) {
			return;
		}

		var proj = new XamarinAndroidApplicationProject (
			packageName: PackageUtils.MakePackageName (runtime, "missingnativelibrary")
		) {
			IsRelease = isRelease,
			ProjectName = "MissingNativeLibrary",
		};
		proj.SetRuntime (runtime);
		proj.SetRuntimeIdentifiers (new [] { DeviceAbi });
		proj.SetDefaultTargetDevice ();

		string libraryName;
		string removeLibraryItems;
		if (runtime == AndroidRuntime.NativeAOT) {
			libraryName = $"lib{proj.ProjectName}.so";
			removeLibraryItems = $@"
			<FrameworkNativeLibrary Remove=""@(FrameworkNativeLibrary)""
				Condition="" '%(FrameworkNativeLibrary.ArchiveFileName)' == '{libraryName}' or '%(FrameworkNativeLibrary.FileName)%(FrameworkNativeLibrary.Extension)' == '{libraryName}' "" />";
		} else {
			libraryName = "libmonodroid.so";
			removeLibraryItems = @"
			<FrameworkNativeLibrary Remove=""@(FrameworkNativeLibrary)""
				Condition="" '%(FrameworkNativeLibrary.ArchiveFileName)' == 'libmonodroid.so' or '%(FrameworkNativeLibrary.FileName)%(FrameworkNativeLibrary.Extension)' == 'libmonodroid.so' "" />";
		}

		proj.Imports.Add (new Import (() => "Directory.Build.targets") {
			TextContent = () => $"""
<Project>
	<Target Name="_RemoveNativeLibraryForTest" BeforeTargets="_BuildApkEmbed">
		<ItemGroup>
			{removeLibraryItems}
		</ItemGroup>
	</Target>
</Project>
"""
		});

		using var builder = CreateApkBuilder ();
		Assert.IsTrue (builder.Install (proj), "Project should have installed.");

		string outputDirectory = Path.Combine (Root, builder.ProjectDirectory, proj.OutputPath);
		string[] apks = Directory.GetFiles (outputDirectory, $"{proj.PackageName}-Signed.apk", SearchOption.AllDirectories);
		Assert.IsNotEmpty (apks, "The signed APK should exist.");
		using (var apk = ZipHelper.OpenZip (apks [0])) {
			Assert.IsNull (apk.GetEntry ($"lib/{DeviceAbi}/{libraryName}"),
				$"{libraryName} should have been removed from the APK.");
		}

		var expectedMessages = new HashSet<string> {
			$"Failed to load native library '{libraryName}'.",
			"Supported ABIs:",
			"Native library directory:",
			"library exists: false",
			"APKs:",
			"contains no matching native libraries",
			"The application installation may be corrupt; reinstalling the application may fix this error.",
		};
		ClearAdbLogcat ();
		string logcatPath = Path.Combine (Root, builder.ProjectDirectory, "native-library-load.log");
		bool foundDiagnostic = MonitorAdbLogcat (
			line => {
				if (Regex.IsMatch (line, @"\bE\s+DOTNET\s*:")) {
					expectedMessages.RemoveWhere (message => line.Contains (message, StringComparison.Ordinal));
				}
				return expectedMessages.Count == 0;
			},
			logcatPath,
			timeout: 45,
			onMonitoringStarted: () => AdbStartActivity ($"{proj.PackageName}/{proj.JavaPackageName}.MainActivity")
		);

		Assert.IsTrue (foundDiagnostic,
			$"The native library diagnostic was incomplete. Missing: {string.Join (", ", expectedMessages)}");
	}
}

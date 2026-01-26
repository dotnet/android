using Android.App;
using Android.Widget;
using Android.OS;
using System;
using Android.Runtime;
using Android.Views;
using System.Runtime.InteropServices;
using Java.Interop;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace HelloWorld
{
	[Activity (
		Icon            = "@mipmap/icon",
		Label           = "HelloWorld",
		MainLauncher    = true,
		Name            = "example.MainActivity")]
	public class MainActivity : Activity
	{
		const int Iterations = 10_000_000;
		const int WarmupIterations = 1000;

		// Default constructor required by Android
		public MainActivity ()
		{
		}

		// Activation constructor for TypeMap v2
		protected MainActivity (IntPtr handle, JniHandleOwnership transfer)
			: base (handle, transfer)
		{
		}

		[Register ("onCreate", "(Landroid/os/Bundle;)V", "n_onCreate")]
		protected override void OnCreate (Bundle savedInstanceState)
		{
			var sw = Stopwatch.StartNew();
			base.OnCreate (savedInstanceState);
			Android.Util.Log.Info("PERF", $"base.OnCreate: {sw.ElapsedMilliseconds}ms");

			sw.Restart();
			SetContentView (Resource.Layout.Main);
			Android.Util.Log.Info("PERF", $"SetContentView: {sw.ElapsedMilliseconds}ms");

			sw.Restart();
			Button button = FindViewById<Button> (Resource.Id.myButton);
			Android.Util.Log.Info("PERF", $"FindViewById: {sw.ElapsedMilliseconds}ms");

			sw.Restart();
			button.Click += (s, e) => RunBenchmarks();
			Android.Util.Log.Info("PERF", $"button.Click+=: {sw.ElapsedMilliseconds}ms");

			sw.Stop();
			Android.Util.Log.Info("PERF", "OnCreate complete - tap button to run benchmarks");
		}

		void RunBenchmarks()
		{
			Android.Util.Log.Info("PERF", "=== Starting Benchmarks ===");

			// Benchmark 1: JNI class lookup (FindClass)
			BenchmarkFindClass();

			// Benchmark 2: Java object creation (Button, TextView, etc.)
			BenchmarkObjectCreation();

			// Benchmark 3: GetObject (wrapping Java handle in .NET object)
			BenchmarkGetObject();

			// Benchmark 4: Method dispatch (calling virtual method -> JNI)
			BenchmarkMethodDispatch();

			// Benchmark 5: Callback dispatch (Java -> .NET via n_* method)
			BenchmarkCallbackDispatch();

			Android.Util.Log.Info("PERF", "=== Benchmarks Complete ===");
		}

		void BenchmarkFindClass()
		{
			// Warmup
			for (int i = 0; i < WarmupIterations; i++)
			{
				var cls = Android.Runtime.JNIEnv.FindClass("android/app/Activity");
				Android.Runtime.JNIEnv.DeleteGlobalRef(cls);
			}

			const int findClassIterations = 100_000;

			var sw = Stopwatch.StartNew();
			for (int i = 0; i < findClassIterations; i++)
			{
				var cls = Android.Runtime.JNIEnv.FindClass("android/app/Activity");
				Android.Runtime.JNIEnv.DeleteGlobalRef(cls);
			}
			sw.Stop();

			double avgUs = (double)sw.ElapsedTicks / findClassIterations * (1_000_000.0 / Stopwatch.Frequency);
			Android.Util.Log.Info("PERF", $"FindClass: {findClassIterations} iterations in {sw.ElapsedMilliseconds}ms, avg={avgUs:F2}us/call");
		}

		void BenchmarkObjectCreation()
		{
			// Create Button instances (common UI object)
			// Warmup
			for (int i = 0; i < WarmupIterations; i++)
			{
				using var obj = new Button(this);
			}

			const int createIterations = 10_000;

			var sw = Stopwatch.StartNew();
			for (int i = 0; i < createIterations; i++)
			{
				using var obj = new Button(this);
			}
			sw.Stop();

			double avgUs = (double)sw.ElapsedTicks / createIterations * (1_000_000.0 / Stopwatch.Frequency);
			Android.Util.Log.Info("PERF", $"ObjectCreation(Button): {createIterations} iterations in {sw.ElapsedMilliseconds}ms, avg={avgUs:F2}us/call");
		}

		void BenchmarkGetObject()
		{
			// Get a Java handle to wrap repeatedly
			using var testButton = new Button(this);
			var handle = testButton.Handle;

			// Warmup
			for (int i = 0; i < WarmupIterations; i++)
			{
				_ = Java.Lang.Object.GetObject<Button>(handle, Android.Runtime.JniHandleOwnership.DoNotTransfer);
			}

			const int getObjectIterations = 1_000_000;

			var sw = Stopwatch.StartNew();
			for (int i = 0; i < getObjectIterations; i++)
			{
				_ = Java.Lang.Object.GetObject<Button>(handle, Android.Runtime.JniHandleOwnership.DoNotTransfer);
			}
			sw.Stop();

			double avgNs = (double)sw.ElapsedTicks / getObjectIterations * (1_000_000_000.0 / Stopwatch.Frequency);
			Android.Util.Log.Info("PERF", $"GetObject: {getObjectIterations} iterations in {sw.ElapsedMilliseconds}ms, avg={avgNs:F2}ns/call");
		}

		void BenchmarkMethodDispatch()
		{
			// Benchmark calling a Java method from .NET (managed -> JNI -> Java)
			using var testButton = new Button(this);

			// Warmup
			for (int i = 0; i < WarmupIterations; i++)
			{
				_ = testButton.Text;
			}

			var sw = Stopwatch.StartNew();
			for (int i = 0; i < Iterations; i++)
			{
				_ = testButton.Text;
			}
			sw.Stop();

			double avgNs = (double)sw.ElapsedTicks / Iterations * (1_000_000_000.0 / Stopwatch.Frequency);
			Android.Util.Log.Info("PERF", $"MethodDispatch(getText): {Iterations} iterations in {sw.ElapsedMilliseconds}ms, avg={avgNs:F2}ns/call");
		}

		void BenchmarkCallbackDispatch()
		{
			// Benchmark Java calling back to .NET (Java -> JNI -> managed)
			// We use a click listener callback
			int callbackCount = 0;
			using var testButton = new Button(this);
			testButton.Click += (s, e) => callbackCount++;

			const int callbackIterations = 100_000;

			// Warmup
			for (int i = 0; i < WarmupIterations; i++)
			{
				testButton.PerformClick();
			}
			callbackCount = 0;

			var sw = Stopwatch.StartNew();
			for (int i = 0; i < callbackIterations; i++)
			{
				testButton.PerformClick();
			}
			sw.Stop();

			double avgUs = (double)sw.ElapsedTicks / callbackIterations * (1_000_000.0 / Stopwatch.Frequency);
			Android.Util.Log.Info("PERF", $"CallbackDispatch(Click): {callbackIterations} iterations in {sw.ElapsedMilliseconds}ms, avg={avgUs:F2}us/call, count={callbackCount}");
		}

		static void n_onCreate (IntPtr jnienv, IntPtr native__this, IntPtr native_savedInstanceState)
		{
			var __this = Java.Lang.Object.GetObject<MainActivity> (native__this, JniHandleOwnership.DoNotTransfer);
			var bundle = Java.Lang.Object.GetObject<Bundle> (native_savedInstanceState, JniHandleOwnership.DoNotTransfer);
			__this.OnCreate (bundle);
		}
	}
}

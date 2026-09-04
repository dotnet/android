using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.OS;
using Android.Views;
using BenchmarkDotNet.Attributes;

namespace Xamarin.Android.Benchmarks;

[MemoryDiagnoser]
public class AndroidApiBenchmarks
{
	const string BundleKey = "value";
	const int BundleValue = 42;

	readonly Context context;
	readonly PackageManager packageManager;
	readonly global::Android.Content.Res.Resources resources;
	readonly Bundle bundle;
	readonly Rect rect;
	readonly View view;
	Java.Lang.Object? powerManager;

	public AndroidApiBenchmarks ()
	{
		context = Application.Context;
		packageManager = context.PackageManager ?? throw new InvalidOperationException ("PackageManager is unavailable.");
		resources = context.Resources ?? throw new InvalidOperationException ("Resources are unavailable.");
		bundle = new Bundle ();
		bundle.PutInt (BundleKey, BundleValue);
		rect = new Rect (0, 0, 100, 100);
		view = new View (context);
	}

	[GlobalSetup]
	public void Setup ()
	{
		powerManager = GetPowerManager ();
		if (GetBundleInt () != BundleValue)
			throw new InvalidOperationException ("Bundle value was not preserved.");
		if (!RectContainsPoint ())
			throw new InvalidOperationException ("The benchmark point is outside the rectangle.");
		if (GetSystemResourceString ().Length == 0)
			throw new InvalidOperationException ("The system resource string is empty.");
	}

	[GlobalCleanup]
	public void Cleanup ()
	{
		powerManager?.Dispose ();
		view.Dispose ();
		rect.Dispose ();
		bundle.Dispose ();
	}

	[Benchmark]
	public long GetElapsedRealtime ()
	{
		return SystemClock.ElapsedRealtime ();
	}

	[Benchmark]
	public bool RectContainsPoint ()
	{
		return rect.Contains (50, 50);
	}

	[Benchmark]
	public int GetBundleInt ()
	{
		return bundle.GetInt (BundleKey);
	}

	[Benchmark]
	public bool HasCameraFeature ()
	{
		return packageManager.HasSystemFeature (PackageManager.FeatureCamera);
	}

	[Benchmark]
	public Java.Lang.Object? GetPowerManager ()
	{
		return context.GetSystemService (Context.PowerService);
	}

	[Benchmark]
	public Context? GetApplicationContext ()
	{
		return context.ApplicationContext;
	}

	[Benchmark]
	public ContentResolver? GetContentResolver ()
	{
		return context.ContentResolver;
	}

	[Benchmark]
	public global::Android.Content.Res.Configuration? GetConfiguration ()
	{
		return resources.Configuration;
	}

	[Benchmark]
	public global::Android.Util.DisplayMetrics? GetDisplayMetrics ()
	{
		return resources.DisplayMetrics;
	}

	[Benchmark]
	public View? GetRootView ()
	{
		return view.RootView;
	}

	[Benchmark]
	public string GetSystemResourceString ()
	{
		return resources.GetString (global::Android.Resource.String.Ok);
	}

	[Benchmark]
	public void SetViewPadding ()
	{
		view.SetPadding (1, 2, 3, 4);
	}
}

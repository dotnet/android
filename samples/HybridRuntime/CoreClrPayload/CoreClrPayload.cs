namespace HybridRuntime.CoreClrPayload;

public static class CoreClrPayload
{
	[System.Runtime.InteropServices.UnmanagedCallersOnly]
	public static void Warmup ()
	{
		long start = Android.OS.SystemClock.ElapsedRealtime ();
		LargeMauiApp.PageCatalog.PrepareForNavigation ();
		long elapsed = Android.OS.SystemClock.ElapsedRealtime () - start;
		Android.Util.Log.Info (
			"HybridRuntime",
			$"CoreCLR managed MAUI payload ready: {LargeMauiApp.PageCatalog.Count} XAML pages prepared in {elapsed} ms"
		);
	}
}

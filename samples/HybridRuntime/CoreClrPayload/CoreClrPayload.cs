using Android.App;
using Android.Runtime;
using Android.Widget;
using Microsoft.Maui;
using Microsoft.Maui.Controls.Embedding;
using Microsoft.Maui.Platform;

namespace HybridRuntime.CoreClrPayload;

public static class CoreClrPayload
{
	static MauiApp? mauiApp;
	static Microsoft.Maui.Controls.Shell? shell;
	static Microsoft.Maui.Controls.Window? window;
	static Android.Views.View? platformView;

	[System.Runtime.InteropServices.UnmanagedCallersOnly]
	public static void Warmup ()
	{
		long start = Android.OS.SystemClock.ElapsedRealtime ();
		mauiApp = HybridTodoApp.MauiProgram.CreateMauiApp (embedded: true);
		long elapsed = Android.OS.SystemClock.ElapsedRealtime () - start;
		Android.Util.Log.Info (
			"HybridRuntime",
			$"CoreCLR managed MAUI TODO application ready in {elapsed} ms"
		);
	}

	[System.Runtime.InteropServices.UnmanagedCallersOnly]
	public static void ShowTodoApp (IntPtr activityHandle)
	{
		Activity? activity = Java.Lang.Object.GetObject<Activity> (activityHandle, JniHandleOwnership.DoNotTransfer);
		if (activity is null) {
			Android.Util.Log.Error ("HybridRuntime", "Could not resolve the Android Activity passed to the CoreCLR payload.");
			return;
		}

		try {
			MauiApp app = mauiApp ?? HybridTodoApp.MauiProgram.CreateMauiApp (embedded: true);
			mauiApp = app;
			shell = new HybridTodoApp.AppShell ();
			IMauiContext windowContext = app.CreateEmbeddedWindowContext (activity);
			Microsoft.Maui.Controls.Application? application = Microsoft.Maui.Controls.Application.Current;
			if (application is null || application.Windows.Count == 0) {
				throw new InvalidOperationException ("The embedded MAUI application did not create a window.");
			}
			window = application.Windows [application.Windows.Count - 1];
			window.Page = shell;
			platformView = shell.ToPlatform (windowContext);
			platformView.Alpha = 0;
			activity.AddContentView (
				platformView,
				new Android.Views.ViewGroup.LayoutParams (
					Android.Views.ViewGroup.LayoutParams.MatchParent,
					Android.Views.ViewGroup.LayoutParams.MatchParent
				)
			);
			NavigateToRequestedRoute (activity, shell, platformView);
		} catch (Exception error) {
			Android.Util.Log.Error ("HybridRuntime", error.ToString ());
			var errorView = new TextView (activity) {
				Text = $"Could not render the CoreCLR MAUI UI:\n\n{error}",
				TextSize = 14,
			};
			errorView.SetPadding (32, 64, 32, 32);
			errorView.SetTextColor (Android.Graphics.Color.White);
			activity.SetContentView (errorView);
		}
	}

	[System.Runtime.InteropServices.UnmanagedCallersOnly]
	public static void HideTodoApp ()
	{
		Microsoft.Maui.Controls.Window? currentWindow = window;
		if (currentWindow is not null) {
			Microsoft.Maui.Controls.Application.Current?.CloseWindow (currentWindow);
		}
		platformView?.Dispose ();
		platformView = null;
		window = null;
		shell = null;
	}

	static void NavigateToRequestedRoute (
		Activity activity,
		Microsoft.Maui.Controls.Shell todoShell,
		Android.Views.View todoView
	)
	{
		string? route = activity.Intent?.GetStringExtra ("hybrid-route");
		int id = activity.Intent?.GetIntExtra ("hybrid-id", 0) ?? 0;
		string destination = route switch {
			null or "" or "main" => "",
			"projects" => "//projects",
			"manage" => "//manage",
			"project" when id > 0 => $"project?id={id}",
			"task" when id > 0 => $"task?id={id}",
			"task" => "task",
			_ => throw new InvalidOperationException ($"Unknown CoreCLR route '{route}'."),
		};
		todoShell.Dispatcher.Dispatch (async () => {
			try {
				if (destination.Length > 0) {
					await todoShell.GoToAsync (destination);
				}
				todoView.Animate ()?.Alpha (1)?.SetDuration (120)?.Start ();
				Android.Util.Log.Info ("HybridRuntime", "CoreCLR MAUI TODO UI attached to the secondary-process Activity.");
			} catch (Exception error) {
				Android.Util.Log.Error ("HybridRuntime", $"Could not navigate to '{destination}': {error}");
				var errorView = new TextView (activity) {
					Text = $"Could not navigate the CoreCLR MAUI UI:\n\n{error}",
					TextSize = 14,
				};
				errorView.SetPadding (32, 64, 32, 32);
				errorView.SetTextColor (Android.Graphics.Color.White);
				activity.SetContentView (errorView);
			}
		});
	}
}

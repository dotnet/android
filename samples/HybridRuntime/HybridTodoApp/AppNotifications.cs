using CommunityToolkit.Maui.Alerts;

namespace HybridTodoApp;

static class AppNotifications
{
	public static async Task DisplayToastAsync (string message)
	{
		if (OperatingSystem.IsWindows ()) {
			return;
		}

		var toast = Toast.Make (message, textSize: 18);
		using var cancellation = new CancellationTokenSource (TimeSpan.FromSeconds (5));
		await toast.Show (cancellation.Token);
	}
}

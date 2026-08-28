using Android.Widget;
using Microsoft.Maui.Platform;

namespace HybridTodoApp.Platforms.Android;

sealed class EmbeddedEntryHandler : Microsoft.Maui.Handlers.EntryHandler
{
	protected override void ConnectHandler (MauiAppCompatEditText platformView)
	{
		platformView.TextChanged += OnTextChanged;
	}

	protected override void DisconnectHandler (MauiAppCompatEditText platformView)
	{
		platformView.TextChanged -= OnTextChanged;
		base.DisconnectHandler (platformView);
	}

	void OnTextChanged (object? sender, global::Android.Text.TextChangedEventArgs e)
	{
		if (VirtualView is Entry entry && entry.Text != e.Text?.ToString ()) {
			entry.Text = e.Text?.ToString ();
		}
	}
}

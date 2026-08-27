using Android.Content;
using Android.Graphics;
using Android.Util;
using Android.Views;
using Android.Widget;

namespace HybridRuntime.Shell;

[Activity (Label = "NativeAOT shell", MainLauncher = true, Name = "net.dot.hybrid.MainActivity", Theme = "@android:style/Theme.Material.NoActionBar")]
public sealed class MainActivity : Activity
{
	const string PreferencesName = "hybrid-runtime";
	const string AuthenticatedPreference = "authenticated";

	int nativeAotClickCount;
	bool coreClrWarmupRequested;

	protected override void OnCreate (Bundle? savedInstanceState)
	{
		base.OnCreate (savedInstanceState);

		if (GetPreferences ().GetBoolean (AuthenticatedPreference, false)) {
			ShowLanding ();
		} else {
			ShowLogin ();
		}
	}

	void ShowLogin ()
	{
		var title = new TextView (this) {
			Text = "Hybrid runtime login",
			TextSize = 28,
		};
		title.SetTextColor (Color.White);

		var username = new EditText (this) {
			Hint = "Username",
			Text = "benchmark@example.com",
		};
		var password = new EditText (this) {
			Hint = "Password",
			InputType = Android.Text.InputTypes.ClassText | Android.Text.InputTypes.TextVariationPassword,
			Text = "password",
		};
		var login = new Button (this) {
			Text = "Log in",
		};
		login.Click += (_, _) => {
			var editor = GetPreferences ().Edit ();
			if (editor is null) {
				throw new InvalidOperationException ("Could not edit authentication preferences.");
			}

			editor.PutBoolean (AuthenticatedPreference, true);
			editor.Apply ();
			ShowLanding ();
		};

		var layout = CreateLayout ();
		layout.AddView (title);
		layout.AddView (username);
		layout.AddView (password);
		layout.AddView (login);
		SetContentView (layout);
	}

	void ShowLanding ()
	{
		var status = new TextView (this) {
			Text = $"NativeAOT dashboard\nProcess: {PackageName} (PID {Android.OS.Process.MyPid ()})",
			TextSize = 20,
		};
		status.SetTextColor (Color.White);

		var nativeAotButton = new Button (this) {
			Text = "Exercise NativeAOT",
		};
		nativeAotButton.Click += (_, _) => {
			nativeAotClickCount++;
			status.Text = $"NativeAOT is still responsive ({nativeAotClickCount}).";
			Log.Info ("HybridRuntime", $"NativeAOT UI callback {nativeAotClickCount}");
		};

		var coreClrButton = new Button (this) {
			Text = "Open CoreCLR process",
		};
		coreClrButton.Click += (_, _) => {
			StartActivity (CreateExplicitIntent ("net.dot.hybrid.CoreClrBootstrapActivity"));
		};

		var layout = CreateLayout ();
		layout.AddView (status);
		layout.AddView (nativeAotButton);
		layout.AddView (coreClrButton);
		SetContentView (layout);
	}

	LinearLayout CreateLayout ()
	{
		var layout = new LinearLayout (this) {
			Orientation = Orientation.Vertical,
		};
		layout.SetPadding (48, 96, 48, 48);
		return layout;
	}

	ISharedPreferences GetPreferences ()
	{
		ISharedPreferences? preferences = GetSharedPreferences (PreferencesName, FileCreationMode.Private);
		if (preferences is null) {
			throw new InvalidOperationException ("Authentication preferences are unavailable.");
		}

		return preferences;
	}

	public override void OnWindowFocusChanged (bool hasFocus)
	{
		base.OnWindowFocusChanged (hasFocus);

		if (!hasFocus || coreClrWarmupRequested) {
			return;
		}

		coreClrWarmupRequested = true;
		Window?.DecorView.Post (() => {
			SendBroadcast (CreateExplicitIntent ("net.dot.hybrid.CoreClrWarmupReceiver"));
		});
	}

	Intent CreateExplicitIntent (string className)
	{
		string? packageName = PackageName;
		if (packageName is null) {
			throw new InvalidOperationException ("The application package name is unavailable.");
		}

		var intent = new Intent ();
		intent.SetClassName (packageName, className);
		return intent;
	}
}

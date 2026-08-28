using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Text;
using Android.Views;
using Android.Views.InputMethods;
using Android.Widget;

namespace HybridRuntime.Shell;

[Activity (Label = "NativeAOT shell", MainLauncher = true, Name = "net.dot.hybrid.MainActivity", Theme = "@android:style/Theme.Material.NoActionBar")]
public sealed class MainActivity : Activity
{
	const string PreferencesName = "hybrid-runtime";
	const string AuthenticatedPreference = "authenticated";
	const string DemoEmail = "benchmark@example.com";
	const string DemoPassword = "password";

	bool coreClrWarmupRequested;

	protected override void OnCreate (Bundle? savedInstanceState)
	{
		base.OnCreate (savedInstanceState);

		if (GetPreferences ().GetBoolean (AuthenticatedPreference, false)) {
			OpenTodoApp ();
		} else {
			ShowLogin ();
		}
	}

	void ShowLogin ()
	{
		Window?.SetSoftInputMode (SoftInput.AdjustResize);

		var brandMark = new TextView (this) {
			Gravity = GravityFlags.Center,
			Text = "T",
			TextSize = 24,
			Typeface = Typeface.DefaultBold,
		};
		brandMark.SetTextColor (Color.White);
		brandMark.Background = CreateRoundedBackground (Color.Rgb (81, 43, 212), 18);
		brandMark.LayoutParameters = new LinearLayout.LayoutParams (Dp (56), Dp (56));

		var brandName = new TextView (this) {
			Text = "Tasks",
			TextSize = 22,
			Typeface = Typeface.DefaultBold,
		};
		brandName.SetTextColor (Color.White);
		var brandNameLayout = MatchContent ();
		brandNameLayout.LeftMargin = Dp (14);
		brandName.LayoutParameters = brandNameLayout;

		var brand = new LinearLayout (this) {
			Orientation = Orientation.Horizontal,
		};
		brand.SetGravity (GravityFlags.CenterVertical);
		brand.AddView (brandMark);
		brand.AddView (brandName);

		var title = new TextView (this) {
			Text = "Welcome back",
			TextSize = 30,
			Typeface = Typeface.DefaultBold,
		};
		title.SetTextColor (Color.White);
		var titleLayout = MatchWidth ();
		titleLayout.TopMargin = Dp (36);
		title.LayoutParameters = titleLayout;

		var subtitle = new TextView (this) {
			Text = "Sign in to continue to your projects and tasks.",
			TextSize = 16,
		};
		subtitle.SetTextColor (Color.Rgb (195, 195, 195));
		var subtitleLayout = MatchWidth ();
		subtitleLayout.TopMargin = Dp (8);
		subtitleLayout.BottomMargin = Dp (28);
		subtitle.LayoutParameters = subtitleLayout;

		var emailLabel = CreateLabel ("Email");
		var username = new EditText (this) {
			Hint = "you@example.com",
			InputType = InputTypes.ClassText | InputTypes.TextVariationEmailAddress,
			Text = DemoEmail,
			TextSize = 16,
		};
		username.SetSingleLine (true);
		StyleField (username);

		var passwordLabel = CreateLabel ("Password");
		var passwordLabelLayout = MatchWidth ();
		passwordLabelLayout.TopMargin = Dp (18);
		passwordLabel.LayoutParameters = passwordLabelLayout;
		var password = new EditText (this) {
			Hint = "Password",
			InputType = InputTypes.ClassText | InputTypes.TextVariationPassword,
			Text = DemoPassword,
			TextSize = 16,
		};
		password.SetSingleLine (true);
		password.TransformationMethod = Android.Text.Method.PasswordTransformationMethod.Instance;
		StyleField (password);

		var error = new TextView (this) {
			Text = "Use the demo email and password shown below.",
			TextSize = 14,
			Visibility = ViewStates.Gone,
		};
		error.SetTextColor (Color.Rgb (255, 159, 159));
		var errorLayout = MatchWidth ();
		errorLayout.TopMargin = Dp (12);
		error.LayoutParameters = errorLayout;

		var login = new Button (this) {
			Text = "Sign in",
			TextSize = 17,
			Typeface = Typeface.DefaultBold,
		};
		login.SetAllCaps (false);
		login.SetTextColor (Color.White);
		login.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf (Color.Rgb (81, 43, 212));
		var loginLayout = MatchWidth (56);
		loginLayout.TopMargin = Dp (24);
		login.LayoutParameters = loginLayout;
		login.Click += (_, _) => {
			if (username.Text != DemoEmail || password.Text != DemoPassword) {
				error.Visibility = ViewStates.Visible;
				return;
			}

			login.Enabled = false;
			login.Text = "Signing in...";
			var inputMethodManager = GetSystemService (InputMethodService) as InputMethodManager;
			inputMethodManager?.HideSoftInputFromWindow (login.WindowToken, HideSoftInputFlags.None);

			var editor = GetPreferences ().Edit ();
			if (editor is null) {
				throw new InvalidOperationException ("Could not edit authentication preferences.");
			}

			editor.PutBoolean (AuthenticatedPreference, true);
			if (!editor.Commit ()) {
				throw new InvalidOperationException ("Could not persist authentication state.");
			}
			OpenTodoApp ();
		};

		var demo = new TextView (this) {
			Gravity = GravityFlags.Center,
			Text = $"Demo access\n{DemoEmail}  /  {DemoPassword}",
			TextSize = 13,
		};
		demo.SetTextColor (Color.Rgb (172, 153, 234));
		var demoLayout = MatchWidth ();
		demoLayout.TopMargin = Dp (22);
		demo.LayoutParameters = demoLayout;

		var card = new LinearLayout (this) {
			Orientation = Orientation.Vertical,
		};
		card.SetPadding (Dp (28), Dp (30), Dp (28), Dp (28));
		card.Background = CreateRoundedBackground (Color.Rgb (34, 34, 40), 22);
		card.AddView (brand);
		card.AddView (title);
		card.AddView (subtitle);
		card.AddView (emailLabel);
		card.AddView (username);
		card.AddView (passwordLabel);
		card.AddView (password);
		card.AddView (error);
		card.AddView (login);
		card.AddView (demo);

		var content = new LinearLayout (this) {
			Orientation = Orientation.Vertical,
		};
		content.SetGravity (GravityFlags.CenterVertical);
		content.SetPadding (Dp (24), Dp (32), Dp (24), Dp (32));
		content.SetBackgroundColor (Color.Rgb (23, 23, 26));
		content.AddView (card, MatchWidth ());

		var scroll = new ScrollView (this) {
			FillViewport = true,
		};
		scroll.AddView (content, MatchWidth (ViewGroup.LayoutParams.MatchParent));
		SetContentView (scroll);
	}

	TextView CreateLabel (string text)
	{
		var label = new TextView (this) {
			Text = text,
			TextSize = 14,
			Typeface = Typeface.DefaultBold,
		};
		label.SetTextColor (Color.Rgb (225, 225, 225));
		return label;
	}

	void StyleField (EditText field)
	{
		field.SetTextColor (Color.White);
		field.SetHintTextColor (Color.Rgb (145, 145, 145));
		field.SetPadding (Dp (16), 0, Dp (16), 0);
		field.Background = CreateRoundedBackground (Color.Rgb (31, 31, 31), 12, Color.Rgb (64, 64, 64));
		var fieldLayout = MatchWidth (54);
		fieldLayout.TopMargin = Dp (8);
		field.LayoutParameters = fieldLayout;
	}

	GradientDrawable CreateRoundedBackground (Color color, int radius, Color? strokeColor = null)
	{
		var background = new GradientDrawable ();
		background.SetColor (color);
		background.SetCornerRadius (Dp (radius));
		if (strokeColor is Color stroke) {
			background.SetStroke (Dp (1), stroke);
		}
		return background;
	}

	LinearLayout.LayoutParams MatchWidth (int height = ViewGroup.LayoutParams.WrapContent)
	{
		return new LinearLayout.LayoutParams (ViewGroup.LayoutParams.MatchParent, height < 0 ? height : Dp (height));
	}

	LinearLayout.LayoutParams MatchContent ()
	{
		return new LinearLayout.LayoutParams (ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent);
	}

	int Dp (int value)
	{
		float density = Resources?.DisplayMetrics?.Density ?? 1f;
		return (int)(value * density + 0.5f);
	}

	void OpenTodoApp ()
	{
		StartActivity (CreateExplicitIntent ("net.dot.hybrid.CoreClrBootstrapActivity"));
		Finish ();
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

using Android.Content;
using Android.Database.Sqlite;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Text;
using Android.Util;
using Android.Views;
using Android.Views.InputMethods;
using Android.Widget;

namespace HybridRuntime.Shell;

[Activity (Label = "NativeAOT shell", MainLauncher = true, Name = "net.dot.hybrid.MainActivity", Theme = "@android:style/Theme.Material.NoActionBar")]
public sealed class MainActivity : Activity
{
	sealed record TodoItem (string Title, bool IsCompleted);

	const string PreferencesName = "hybrid-runtime";
	const string AuthenticatedPreference = "authenticated";
	const string DemoEmail = "benchmark@example.com";
	const string DemoPassword = "password";

	bool coreClrWarmupRequested;
	bool dashboardVisible;
	bool hasResumed;

	protected override void OnCreate (Bundle? savedInstanceState)
	{
		base.OnCreate (savedInstanceState);

		if (GetPreferences ().GetBoolean (AuthenticatedPreference, false)) {
			ShowTodoDashboard ();
		} else {
			ShowLogin ();
		}
	}

	protected override void OnResume ()
	{
		base.OnResume ();

		if (hasResumed && dashboardVisible) {
			ShowTodoDashboard ();
		}
		hasResumed = true;
	}

	void ShowLogin ()
	{
		dashboardVisible = false;
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
			ShowTodoDashboard ();
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

	void ShowTodoDashboard ()
	{
		dashboardVisible = true;
		Window?.SetSoftInputMode (SoftInput.StateAlwaysHidden);

		var brandMark = new TextView (this) {
			Gravity = GravityFlags.Center,
			Text = "T",
			TextSize = 20,
			Typeface = Typeface.DefaultBold,
		};
		brandMark.SetTextColor (Color.White);
		brandMark.Background = CreateRoundedBackground (Color.Rgb (81, 43, 212), 14);
		brandMark.LayoutParameters = new LinearLayout.LayoutParams (Dp (46), Dp (46));

		var brandName = new TextView (this) {
			Text = "Tasks",
			TextSize = 21,
			Typeface = Typeface.DefaultBold,
		};
		brandName.SetTextColor (Color.White);
		var brandNameLayout = MatchContent ();
		brandNameLayout.LeftMargin = Dp (12);
		brandName.LayoutParameters = brandNameLayout;

		var header = new LinearLayout (this) {
			Orientation = Orientation.Horizontal,
		};
		header.SetGravity (GravityFlags.CenterVertical);
		header.AddView (brandMark);
		header.AddView (brandName);

		var date = new TextView (this) {
			Text = DateTime.Now.ToString ("dddd, MMM d"),
			TextSize = 14,
		};
		date.SetTextColor (Color.Rgb (172, 153, 234));
		var dateLayout = MatchWidth ();
		dateLayout.TopMargin = Dp (34);
		date.LayoutParameters = dateLayout;

		var title = new TextView (this) {
			Text = "Your day at a glance",
			TextSize = 30,
			Typeface = Typeface.DefaultBold,
		};
		title.SetTextColor (Color.White);
		var titleLayout = MatchWidth ();
		titleLayout.TopMargin = Dp (4);
		title.LayoutParameters = titleLayout;

		IReadOnlyList<TodoItem> tasks = LoadTodoItems ();
		int openTaskCount = tasks.Count (task => !task.IsCompleted);
		var summary = new TextView (this) {
			Text = $"{openTaskCount} open tasks across 4 projects",
			TextSize = 16,
		};
		summary.SetTextColor (Color.Rgb (195, 195, 195));
		var summaryLayout = MatchWidth ();
		summaryLayout.TopMargin = Dp (8);
		summaryLayout.BottomMargin = Dp (28);
		summary.LayoutParameters = summaryLayout;

		var sectionTitle = new TextView (this) {
			Text = "Today's tasks",
			TextSize = 21,
			Typeface = Typeface.DefaultBold,
		};
		sectionTitle.SetTextColor (Color.White);

		var taskList = new LinearLayout (this) {
			Orientation = Orientation.Vertical,
		};
		var taskListLayout = MatchWidth ();
		taskListLayout.TopMargin = Dp (12);
		taskList.LayoutParameters = taskListLayout;
		foreach (TodoItem task in tasks.Take (6)) {
			taskList.AddView (CreateTodoRow (task));
		}

		var openWorkspace = new Button (this) {
			Text = "Open full workspace",
			TextSize = 17,
			Typeface = Typeface.DefaultBold,
		};
		openWorkspace.SetAllCaps (false);
		openWorkspace.SetTextColor (Color.White);
		openWorkspace.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf (Color.Rgb (81, 43, 212));
		var openWorkspaceLayout = MatchWidth (56);
		openWorkspaceLayout.TopMargin = Dp (24);
		openWorkspace.LayoutParameters = openWorkspaceLayout;
		openWorkspace.Click += (_, _) => OpenTodoApp ();

		var explanation = new TextView (this) {
			Gravity = GravityFlags.Center,
			Text = "View projects, edit tasks, and manage categories",
			TextSize = 13,
		};
		explanation.SetTextColor (Color.Rgb (145, 145, 145));
		var explanationLayout = MatchWidth ();
		explanationLayout.TopMargin = Dp (10);
		explanation.LayoutParameters = explanationLayout;

		var content = new LinearLayout (this) {
			Orientation = Orientation.Vertical,
		};
		content.SetPadding (Dp (24), Dp (32), Dp (24), Dp (32));
		content.SetBackgroundColor (Color.Rgb (23, 23, 26));
		content.AddView (header);
		content.AddView (date);
		content.AddView (title);
		content.AddView (summary);
		content.AddView (sectionTitle);
		content.AddView (taskList);
		content.AddView (openWorkspace);
		content.AddView (explanation);

		var scroll = new ScrollView (this) {
			FillViewport = true,
		};
		scroll.AddView (content, MatchWidth (ViewGroup.LayoutParams.MatchParent));
		SetContentView (scroll);
	}

	View CreateTodoRow (TodoItem task)
	{
		var completed = new CheckBox (this) {
			Checked = task.IsCompleted,
			Clickable = false,
			Focusable = false,
		};
		completed.ButtonTintList = Android.Content.Res.ColorStateList.ValueOf (Color.Rgb (172, 153, 234));
		completed.LayoutParameters = new LinearLayout.LayoutParams (Dp (48), Dp (48));

		var taskTitle = new TextView (this) {
			Text = task.Title,
			TextSize = 16,
		};
		taskTitle.SetTextColor (task.IsCompleted ? Color.Rgb (145, 145, 145) : Color.White);
		var taskTitleLayout = new LinearLayout.LayoutParams (0, ViewGroup.LayoutParams.WrapContent, 1);
		taskTitleLayout.LeftMargin = Dp (8);
		taskTitle.LayoutParameters = taskTitleLayout;

		var row = new LinearLayout (this) {
			Orientation = Orientation.Horizontal,
		};
		row.SetGravity (GravityFlags.CenterVertical);
		row.SetPadding (Dp (14), Dp (10), Dp (16), Dp (10));
		row.Background = CreateRoundedBackground (Color.Rgb (34, 34, 40), 14);
		row.AddView (completed);
		row.AddView (taskTitle);
		var rowLayout = MatchWidth (68);
		rowLayout.BottomMargin = Dp (10);
		row.LayoutParameters = rowLayout;
		return row;
	}

	IReadOnlyList<TodoItem> LoadTodoItems ()
	{
		TodoItem [] fallback = [
			new ("Survey Employees", false),
			new ("Analyze Survey Results", false),
			new ("Develop Action Plan", false),
			new ("Read a Book", false),
			new ("Attend a Workshop", false),
			new ("Practice a Hobby", false),
			new ("Morning Yoga", false),
			new ("Evening Run", false),
			new ("Healthy Cooking Class", false),
			new ("Plan a Family Reunion", false),
			new ("Organize a Friends' Get-together", false),
			new ("Weekly Phone Calls", false),
		];

		string? filesDirectory = FilesDir?.AbsolutePath;
		if (filesDirectory is null) {
			throw new InvalidOperationException ("The application files directory is unavailable.");
		}
		string databasePath = System.IO.Path.Combine (filesDirectory, "AppSQLite.db3");
		if (!File.Exists (databasePath)) {
			return fallback;
		}

		try {
			using SQLiteDatabase? database = SQLiteDatabase.OpenDatabase (
				databasePath,
				null,
				DatabaseOpenFlags.OpenReadonly
			);
			if (database is null) {
				throw new InvalidOperationException ("The TODO database could not be opened.");
			}
			using Android.Database.ICursor? cursor = database.RawQuery (
				"SELECT Title, IsCompleted FROM Task ORDER BY ID",
				null
			);
			if (cursor is null) {
				throw new InvalidOperationException ("The TODO query did not return a cursor.");
			}

			var tasks = new List<TodoItem> ();
			while (cursor.MoveToNext ()) {
				string? taskTitle = cursor.GetString (0);
				if (taskTitle is not null) {
					tasks.Add (new TodoItem (taskTitle, cursor.GetInt (1) != 0));
				}
			}
			return tasks.Count == 0 ? fallback : tasks;
		} catch (SQLiteException error) {
			Log.Warn ("HybridRuntime", $"Could not read the TODO dashboard snapshot: {error.Message}");
			return fallback;
		}
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

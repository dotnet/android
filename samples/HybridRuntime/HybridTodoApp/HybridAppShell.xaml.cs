namespace HybridTodoApp;

public partial class HybridAppShell : Shell
{
	public HybridAppShell ()
	{
		InitializeComponent ();
		Application? application = Application.Current;
		if (application is null) {
			throw new InvalidOperationException ("The current MAUI application is unavailable.");
		}
		ThemeSegmentedControl.SelectedIndex = application.RequestedTheme == AppTheme.Light ? 0 : 1;
	}

	void OnProjectsClicked (object? sender, EventArgs e)
		=> HybridRuntimeNavigation.OpenFullWorkspace ("projects");

	void OnManageMetaClicked (object? sender, EventArgs e)
		=> HybridRuntimeNavigation.OpenFullWorkspace ("manage");

	void SfSegmentedControl_SelectionChanged (object? sender, Syncfusion.Maui.Toolkit.SegmentedControl.SelectionChangedEventArgs e)
	{
		Application? application = Application.Current;
		if (application is null) {
			throw new InvalidOperationException ("The current MAUI application is unavailable.");
		}
		application.UserAppTheme = e.NewIndex == 0 ? AppTheme.Light : AppTheme.Dark;
	}
}

using LargeMauiApp;

namespace MauiBenchmarkApp;

public sealed record PageLink (int Index, string Title)
{
	public string AutomationId => $"Page{Index + 1:000}";
}

public partial class LandingPage : ContentPage
{
	public LandingPage ()
	{
		InitializeComponent ();
		PageList.ItemsSource = Enumerable.Range (0, PageCatalog.Count)
			.Select (index => new PageLink (index, $"Open page {index + 1:000}"))
			.ToArray ();
	}

	async void OnPageClicked (object? sender, EventArgs e)
	{
		if (sender is not Button { BindingContext: PageLink page }) {
			return;
		}

		await Navigation.PushAsync (PageCatalog.Create (page.Index));
	}
}

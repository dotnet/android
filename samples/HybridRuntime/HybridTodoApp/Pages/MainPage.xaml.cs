using HybridTodoApp.Models;
using HybridTodoApp.PageModels;

namespace HybridTodoApp.Pages;

public partial class MainPage : ContentPage
{
	public MainPage(MainPageModel model)
	{
		InitializeComponent();
		BindingContext = model;
#if HYBRID_RUNTIME
		ProjectsCollectionView.SelectionChanged += OnHybridSelectionChanged;
		TasksCollectionView.SelectionChanged += OnHybridSelectionChanged;
#endif
	}

#if HYBRID_RUNTIME
	void OnHybridSelectionChanged (object? sender, SelectionChangedEventArgs e)
	{
		if (e.CurrentSelection.Count == 0) {
			return;
		}

		if (sender is CollectionView collection) {
			collection.SelectedItem = null;
		}
		switch (e.CurrentSelection [0]) {
		case Project project:
			HybridRuntimeNavigation.OpenFullWorkspace ("project", project.ID);
			break;
		case ProjectTask task:
			HybridRuntimeNavigation.OpenFullWorkspace ("task", task.ID);
			break;
		}
	}
#endif
}
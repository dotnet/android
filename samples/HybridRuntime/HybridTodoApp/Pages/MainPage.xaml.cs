using HybridTodoApp.Models;
using HybridTodoApp.PageModels;

namespace HybridTodoApp.Pages;

public partial class MainPage : ContentPage
{
	public MainPage(MainPageModel model)
	{
		InitializeComponent();
		BindingContext = model;
	}
}
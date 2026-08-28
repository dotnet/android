using CommunityToolkit.Mvvm.Input;
using HybridTodoApp.Models;

namespace HybridTodoApp.PageModels;

public interface IProjectTaskPageModel
{
	IAsyncRelayCommand<ProjectTask> NavigateToTaskCommand { get; }
	bool IsBusy { get; }
}
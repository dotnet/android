using CommunityToolkit.Maui;
using Microsoft.Maui.Controls.Embedding;
using Microsoft.Extensions.Logging;
using Syncfusion.Maui.Toolkit.Hosting;

namespace HybridTodoApp;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp(bool embedded = false)
	{
		var builder = MauiApp.CreateBuilder();
#if ANDROID
		if (embedded)
		{
			// The manually initialized CoreCLR host currently wraps InputMethodManager as
			// Java.Lang.Object, so the stock Entry handler cannot restart input on attach.
			Microsoft.Maui.Handlers.EntryHandler.Mapper.ModifyMapping(
				nameof(IEntry.ReturnType),
				(handler, entry, _) =>
					handler.PlatformView.ImeOptions = Microsoft.Maui.Platform.ImeActionExtensions.ToPlatform(entry.ReturnType)
			);
		}
#endif
		if (embedded)
		{
			builder.UseMauiEmbeddedApp<App>();
		}
		else
		{
			builder.UseMauiApp<App>();
		}

		builder
			.UseMauiCommunityToolkit()
			.ConfigureSyncfusionToolkit()
			.ConfigureMauiHandlers(handlers =>
			{
#if ANDROID
				if (embedded)
				{
					handlers.AddHandler<Entry, Platforms.Android.EmbeddedEntryHandler>();
				}
#endif
#if WINDOWS
				Microsoft.Maui.Controls.Handlers.Items.CollectionViewHandler.Mapper.AppendToMapping("KeyboardAccessibleCollectionView", (handler, view) =>
				{
					handler.PlatformView.SingleSelectionFollowsFocus = false;
				});

				Microsoft.Maui.Handlers.ContentViewHandler.Mapper.AppendToMapping(nameof(Pages.Controls.CategoryChart), (handler, view) =>
				{
					if (view is Pages.Controls.CategoryChart && handler.PlatformView is Microsoft.Maui.Platform.ContentPanel contentPanel)
					{
						contentPanel.IsTabStop = true;
					}
				});
#endif
			})
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
				fonts.AddFont("SegoeUI-Semibold.ttf", "SegoeSemibold");
				fonts.AddFont("FluentSystemIcons-Regular.ttf", FluentUI.FontFamily);
			});

#if DEBUG
		builder.Logging.AddDebug();
		builder.Services.AddLogging(configure => configure.AddDebug());
#endif

		builder.Services.AddSingleton<ProjectRepository>();
		builder.Services.AddSingleton<TaskRepository>();
		builder.Services.AddSingleton<CategoryRepository>();
		builder.Services.AddSingleton<TagRepository>();
		builder.Services.AddSingleton<SeedDataService>();
		builder.Services.AddSingleton<ModalErrorHandler>();
		builder.Services.AddSingleton<MainPageModel>();
#if !HYBRID_RUNTIME
		builder.Services.AddSingleton<ProjectListPageModel>();
		builder.Services.AddSingleton<ManageMetaPageModel>();

		builder.Services.AddTransientWithShellRoute<ProjectDetailPage, ProjectDetailPageModel>("project");
		builder.Services.AddTransientWithShellRoute<TaskDetailPage, TaskDetailPageModel>("task");
#endif

		return builder.Build();
	}
}


using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

public class CreateEmbeddedAssemblyStore : AndroidTask
{
	public override string TaskPrefix => "CEAS";

	[Required]
	public string AndroidBinUtilsDirectory { get; set; }

	[Required]
	public string AppSharedLibrariesDir { get; set; }

	[Required]
	public ITaskItem[] ResolvedUserAssemblies { get; set; }

	[Required]
	public ITaskItem[] ResolvedFrameworkAssemblies { get; set; }

	[Output]
	public ITaskItem[] NativeAssemblySources { get; set; }

	[Output]
	public ITaskItem[] EmbeddedObjectFiles { get; set; }

	public override bool RunTask ()
	{
		return !Log.HasLoggedErrors;
	}
}

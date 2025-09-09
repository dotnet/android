using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;

namespace Xamarin.Android.Tasks;

public class GenerateNativeAotEnvironmentAssemblerSources : AndroidTask
{
	public override string TaskPrefix => "GNAEAS";

	[Required]
	public string EnvironmentOutputDirectory { get; set; } = "";

	public ITaskItem[]? Environments { get; set; }

	public override bool RunTask ()
	{
		if (Environments == null || Environments.Length == 0) {
			return true;
		}

		return !Log.HasLoggedErrors;
	}
}

using Xamarin.Android.Tools;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks;

public class CreateEmbeddedAssemblyStore : AndroidTask
{
	public override string TaskPrefix => "CEAS";

	public override bool RunTask ()
	{
		return !Log.HasLoggedErrors;
	}
}

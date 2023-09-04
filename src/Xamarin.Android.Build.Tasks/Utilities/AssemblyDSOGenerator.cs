using System.Collections.Generic;

using Xamarin.Android.Tasks.LLVMIR;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

class AssemblyDSOGenerator : LlvmIrComposer
{
	Dictionary<AndroidTargetArch, List<DSOAssemblyInfo>> assemblies;

	public AssemblyDSOGenerator (Dictionary<AndroidTargetArch, List<DSOAssemblyInfo>> dsoAssemblies)
	{
		assemblies = dsoAssemblies;
	}

	protected override void Construct (LlvmIrModule module)
	{
		throw new System.NotImplementedException();
	}
}

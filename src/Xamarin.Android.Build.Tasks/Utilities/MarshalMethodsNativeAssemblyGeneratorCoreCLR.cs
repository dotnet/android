#nullable enable
using System.Collections.Generic;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

class MarshalMethodsNativeAssemblyGeneratorCoreCLR : MarshalMethodsNativeAssemblyGenerator
{
	/// <summary>
	/// CoreCLR always needs xamarin_app_init for runtime initialization,
	/// even when marshal methods are disabled.
	/// </summary>
	protected override bool AlwaysGenerateXamarinAppInit => true;

	/// <summary>
	/// Constructor to be used ONLY when marshal methods are DISABLED
	/// </summary>
	public MarshalMethodsNativeAssemblyGeneratorCoreCLR (TaskLoggingHelper log, AndroidTargetArch targetArch, ICollection<string> uniqueAssemblyNames)
		: base (log, targetArch, uniqueAssemblyNames)
	{}

	public MarshalMethodsNativeAssemblyGeneratorCoreCLR (TaskLoggingHelper log, ICollection<string> uniqueAssemblyNames, NativeCodeGenStateObject codeGenState)
		: base (log, uniqueAssemblyNames, codeGenState)
	{}
}

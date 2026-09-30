#nullable enable
using System.Collections.Generic;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

class MarshalMethodsNativeAssemblyGenerator : MarshalMethodsNativeAssemblyGeneratorBase
{
	/// <summary>
	/// Constructor to be used ONLY when marshal methods are DISABLED
	/// </summary>
	public MarshalMethodsNativeAssemblyGenerator (TaskLoggingHelper log, AndroidTargetArch targetArch, ICollection<string> uniqueAssemblyNames)
		: base (log, targetArch, uniqueAssemblyNames)
	{}

	public MarshalMethodsNativeAssemblyGenerator (TaskLoggingHelper log, ICollection<string> uniqueAssemblyNames, NativeCodeGenStateObject codeGenState)
		: base (log, uniqueAssemblyNames, codeGenState)
	{}
}

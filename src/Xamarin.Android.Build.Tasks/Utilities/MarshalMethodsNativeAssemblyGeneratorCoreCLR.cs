#nullable enable
using System.Collections.Generic;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks;

class MarshalMethodsNativeAssemblyGeneratorCoreCLR : MarshalMethodsNativeAssemblyGenerator
{
	public MarshalMethodsNativeAssemblyGeneratorCoreCLR (TaskLoggingHelper log, ICollection<string> uniqueAssemblyNames, NativeCodeGenStateObject codeGenState, bool managedMarshalMethodsLookupEnabled)
		: base (log, uniqueAssemblyNames, codeGenState, managedMarshalMethodsLookupEnabled)
	{}
}

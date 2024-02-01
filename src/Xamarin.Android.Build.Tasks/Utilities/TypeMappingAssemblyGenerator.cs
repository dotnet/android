using System;
using Xamarin.Android.Tasks.LLVMIR;

namespace Xamarin.Android.Tasks
{
	abstract class TypeMappingAssemblyGenerator : LlvmIrComposer
	{
		protected TypeMappingAssemblyGenerator (Action<string> logger)
			: base (logger)
		{
		}
	}
}

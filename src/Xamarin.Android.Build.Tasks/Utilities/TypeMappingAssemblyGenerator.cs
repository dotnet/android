using System;
using Xamarin.Android.Tasks.LLVMIR;

using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks
{
	abstract class TypeMappingAssemblyGenerator : LlvmIrComposer
	{
		protected TypeMappingAssemblyGenerator (TaskLoggingHelper log)
			: base (log)
		{
		}
	}
}

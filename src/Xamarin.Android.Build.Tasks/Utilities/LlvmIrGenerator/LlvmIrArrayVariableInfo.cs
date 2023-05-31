using System;
using System.Collections;

namespace Xamarin.Android.Tasks.LLVM.IR;

sealed class LlvmIrArrayVariableInfo
{
	public readonly Type ElementType;
	public readonly IList Entries;
	public readonly object OriginalVariableValue;
	public readonly StructureInfo? StructureInfo;

	public LlvmIrArrayVariableInfo (Type elementType, IList entries, object originalVariableValue, StructureInfo? structureInfo = null)
	{
		ElementType = elementType;
		Entries = entries;
		OriginalVariableValue = originalVariableValue;
		StructureInfo = structureInfo;
	}
}

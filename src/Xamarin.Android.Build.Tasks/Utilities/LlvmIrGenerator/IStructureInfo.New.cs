using System;
using System.Collections.Generic;

namespace Xamarin.Android.Tasks.LLVM.IR
{
	interface IStructureInfo
	{
		Type Type                          { get; }
		ulong Size                         { get; }
		int MaxFieldAlignment              { get; }
		string Name                        { get; }
		string NativeTypeDesignator        { get; }
		IList<StructureMemberInfo> Members { get; }
		bool IsOpaque                      { get; }
	}
}

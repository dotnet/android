using System;

namespace Xamarin.Android.Tasks.LLVMIR
{
	interface IStructureInfo
	{
		Type Type             { get; }
		ulong Size            { get; }
		int MaxFieldAlignment { get; }
		string Name           { get; }
		string NativeTypeDesignator { get; }

		void RenderDeclaration (LlvmIrGenerator generator);
	}
}

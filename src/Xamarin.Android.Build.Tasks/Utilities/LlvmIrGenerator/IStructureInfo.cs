using System;

namespace Xamarin.Android.Tasks.LLVMIR
{
	interface IStructureInfo
	{
		Type Type             { get; }
		ulong Size            { get; }
		int MaxFieldAlignment { get; }

		void RenderDeclaration (LlvmIrGenerator generator);
	}
}

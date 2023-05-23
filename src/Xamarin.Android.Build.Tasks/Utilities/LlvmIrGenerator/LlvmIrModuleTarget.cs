using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks.LLVM.IR
{
	abstract class LlvmIrModuleTarget
	{
		public abstract LlvmIrDataLayout DataLayout  { get; }
		public abstract string Triple                { get; }
		public abstract AndroidTargetArch TargetArch { get; }
		public abstract uint NativePointerSize       { get; }

		public virtual void AddTargetSpecificAttributes (LlvmIrFunctionAttributeSet attrSet)
		{}
	}
}

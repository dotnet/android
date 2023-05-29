using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks.LLVM.IR
{
	abstract class LlvmIrModuleTarget
	{
		public abstract LlvmIrDataLayout DataLayout  { get; }
		public abstract string Triple                { get; }
		public abstract AndroidTargetArch TargetArch { get; }
		public abstract uint NativePointerSize       { get; }

		/// <summary>
		/// Adds target-specific attributes which are common to many attribute sets. Usually this specifies CPU type, tuning and
		/// features.
		/// </summary>
		public virtual void AddTargetSpecificAttributes (LlvmIrFunctionAttributeSet attrSet)
		{}

		public virtual void SetParameterFlags (LlvmIrFunctionParameter parameter)
		{
			if (!parameter.NoUndef.HasValue) {
				parameter.NoUndef = true;
			}
		}

		/// <summary>
		/// Sets the <c>zeroext</c> or <c>signext</c> attributes on the parameter, if not set previously and if
		/// the parameter is a small integral type.  Out of our supported architectures, all except AArch64 set
		/// the flags, thus the reason to put this method in the base class.
		/// </summary>
		protected void SetIntegerParameterUpcastFlags (LlvmIrFunctionParameter parameter)
		{
			if (parameter.Type == typeof(bool) ||
			    parameter.Type == typeof(byte) ||
			    parameter.Type == typeof(char) ||
			    parameter.Type == typeof(ushort))
			{
				if (!parameter.ZeroExt.HasValue) {
					parameter.ZeroExt = true;
					parameter.SignExt = false;
				}
				return;
			}

			if (parameter.Type == typeof(sbyte) ||
			    parameter.Type == typeof(short))
			{
				if (!parameter.SignExt.HasValue) {
					parameter.SignExt = true;
					parameter.ZeroExt = false;
				}
			}
		}

		public virtual int GetAggregateAlignment (int maxFieldAlignment, ulong dataSize)
		{
			return maxFieldAlignment;
		}
	}
}

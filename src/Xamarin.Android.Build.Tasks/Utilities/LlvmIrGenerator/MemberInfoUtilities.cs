using System;
using System.Reflection;

namespace Xamarin.Android.Tasks.LLVMIR
{
	static class MemberInfoUtilities
	{
		public static bool IsNativePointer (this MemberInfo mi, LlvmIrTypeCache cache)
		{
			return cache.GetNativePointerAttribute (mi) != null;
		}

		public static bool IsNativePointerToPreallocatedBuffer (this MemberInfo mi, LlvmIrTypeCache cache, out ulong requiredBufferSize)
		{
			var attr = cache.GetNativePointerAttribute (mi);
			if (attr == null) {
				requiredBufferSize = 0;
				return false;
			}

			requiredBufferSize = attr.PreAllocatedBufferSize;
			return attr.PointsToPreAllocatedBuffer;
		}

		public static bool PointsToSymbol (this MemberInfo mi, LlvmIrTypeCache cache, out string? symbolName)
		{
			var attr = cache.GetNativePointerAttribute (mi);
			if (attr == null || attr.PointsToSymbol == null) {
				symbolName = null;
				return false;
			}

			symbolName = attr.PointsToSymbol;
			return true;
		}

		public static bool IsUnicodeString (this MemberInfo mi, LlvmIrTypeCache cache)
		{
			var attr = cache.GetNativeAssemblerAttribute (mi);
			if (attr == null) {
				return false;
			}

			return attr.IsUTF16;
		}

		public static LlvmIrStringEncoding GetStringEncoding (this MemberInfo mi, LlvmIrTypeCache cache)
		{
			const LlvmIrStringEncoding DefaultStringEncoding = LlvmIrStringEncoding.UTF8;

			var attr = cache.GetNativeAssemblerAttribute (mi);
			if (attr == null) {
				return DefaultStringEncoding;
			}

			return attr.IsUTF16 ? LlvmIrStringEncoding.Unicode : DefaultStringEncoding;
		}

		public static bool ShouldBeIgnored (this MemberInfo mi, LlvmIrTypeCache cache)
		{
			var attr = cache.GetNativeAssemblerAttribute (mi);
			return attr != null && attr.Ignore;
		}

		public static bool UsesDataProvider (this MemberInfo mi, LlvmIrTypeCache cache)
		{
			var attr = cache.GetNativeAssemblerAttribute (mi);
			return attr != null && attr.UsesDataProvider;
		}

		public static bool IsInlineArray (this MemberInfo mi, LlvmIrTypeCache cache)
		{
			var attr = cache.GetNativeAssemblerAttribute (mi);
			return attr != null && attr.InlineArray;
		}

		public static int GetInlineArraySize (this MemberInfo mi, LlvmIrTypeCache cache)
		{
			var attr = cache.GetNativeAssemblerAttribute (mi);
			if (attr == null || !attr.InlineArray) {
				return -1;
			}

			return attr.InlineArraySize;
		}

		public static bool InlineArrayNeedsPadding (this MemberInfo mi, LlvmIrTypeCache cache)
		{
			var attr = cache.GetNativeAssemblerAttribute (mi);
			if (attr == null || !attr.InlineArray) {
				return false;
			}

			return attr.NeedsPadding;
		}

		public static LlvmIrVariableNumberFormat GetNumberFormat (this MemberInfo mi, LlvmIrTypeCache cache)
		{
			var attr = cache.GetNativeAssemblerAttribute (mi);
			if (attr == null) {
				return LlvmIrVariableNumberFormat.Default;
			}

			return attr.NumberFormat;
		}
	}
}

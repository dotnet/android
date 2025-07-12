using System;
using System.Reflection;

namespace Xamarin.Android.Tasks.LLVMIR
{
	/// <summary>
	/// Provides utility methods for working with member information in the context of LLVM IR generation.
	/// </summary>
	static class MemberInfoUtilities
	{
		/// <summary>
		/// Determines whether a member is marked as a native pointer.
		/// </summary>
		/// <param name="mi">The member info to check.</param>
		/// <param name="cache">The LLVM IR type cache.</param>
		/// <returns>true if the member is a native pointer; otherwise, false.</returns>
		public static bool IsNativePointer (this MemberInfo mi, LlvmIrTypeCache cache)
		{
			return cache.GetNativePointerAttribute (mi) != null;
		}

		/// <summary>
		/// Determines whether a member is a native pointer that points to a pre-allocated buffer.
		/// </summary>
		/// <param name="mi">The member info to check.</param>
		/// <param name="cache">The LLVM IR type cache.</param>
		/// <param name="requiredBufferSize">When this method returns, contains the required buffer size if the member points to a pre-allocated buffer; otherwise, 0.</param>
		/// <returns>true if the member points to a pre-allocated buffer; otherwise, false.</returns>
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

		/// <summary>
		/// Determines whether a member points to a symbol and gets the symbol name.
		/// </summary>
		/// <param name="mi">The member info to check.</param>
		/// <param name="cache">The LLVM IR type cache.</param>
		/// <param name="symbolName">When this method returns, contains the symbol name if the member points to a symbol; otherwise, null.</param>
		/// <returns>true if the member points to a symbol; otherwise, false.</returns>
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

		/// <summary>
		/// Determines whether a string member should use Unicode (UTF-16) encoding.
		/// </summary>
		/// <param name="mi">The member info to check.</param>
		/// <param name="cache">The LLVM IR type cache.</param>
		/// <returns>true if the member should use Unicode encoding; otherwise, false.</returns>
		public static bool IsUnicodeString (this MemberInfo mi, LlvmIrTypeCache cache)
		{
			var attr = cache.GetNativeAssemblerAttribute (mi);
			if (attr == null) {
				return false;
			}

			return attr.IsUTF16;
		}

		/// <summary>
		/// Gets the string encoding to use for a member.
		/// </summary>
		/// <param name="mi">The member info to get encoding for.</param>
		/// <param name="cache">The LLVM IR type cache.</param>
		/// <returns>The string encoding to use for the member.</returns>
		public static LlvmIrStringEncoding GetStringEncoding (this MemberInfo mi, LlvmIrTypeCache cache)
		{
			const LlvmIrStringEncoding DefaultStringEncoding = LlvmIrStringEncoding.UTF8;

			var attr = cache.GetNativeAssemblerAttribute (mi);
			if (attr == null) {
				return DefaultStringEncoding;
			}

			return attr.IsUTF16 ? LlvmIrStringEncoding.Unicode : DefaultStringEncoding;
		}

		/// <summary>
		/// Gets the overridden name for a member if specified in attributes.
		/// </summary>
		/// <param name="mi">The member info to get the overridden name for.</param>
		/// <param name="cache">The LLVM IR type cache.</param>
		/// <returns>The overridden name if specified; otherwise, null.</returns>
		public static string? GetOverriddenName (this MemberInfo mi, LlvmIrTypeCache cache)
		{
			var attr = cache.GetNativeAssemblerAttribute (mi);
			return attr != null ? attr.MemberName : null;
		}

		/// <summary>
		/// Gets the valid target specification for a member.
		/// </summary>
		/// <param name="mi">The member info to get the valid target for.</param>
		/// <param name="cache">The LLVM IR type cache.</param>
		/// <returns>The valid target specification for the member.</returns>
		public static NativeAssemblerValidTarget GetValidTarget (this MemberInfo mi, LlvmIrTypeCache cache)
		{
			var attr = cache.GetNativeAssemblerAttribute (mi);
			return attr != null ? attr.ValidTarget : NativeAssemblerValidTarget.Any;
		}

		/// <summary>
		/// Determines whether a member should be ignored during native assembler generation.
		/// </summary>
		/// <param name="mi">The member info to check.</param>
		/// <param name="cache">The LLVM IR type cache.</param>
		/// <returns>true if the member should be ignored; otherwise, false.</returns>
		public static bool ShouldBeIgnored (this MemberInfo mi, LlvmIrTypeCache cache)
		{
			var attr = cache.GetNativeAssemblerAttribute (mi);
			return attr != null && attr.Ignore;
		}

		/// <summary>
		/// Determines whether a member uses a data provider for dynamic data generation.
		/// </summary>
		/// <param name="mi">The member info to check.</param>
		/// <param name="cache">The LLVM IR type cache.</param>
		/// <returns>true if the member uses a data provider; otherwise, false.</returns>
		public static bool UsesDataProvider (this MemberInfo mi, LlvmIrTypeCache cache)
		{
			var attr = cache.GetNativeAssemblerAttribute (mi);
			return attr != null && attr.UsesDataProvider;
		}

		/// <summary>
		/// Determines whether a member represents an inline array.
		/// </summary>
		/// <param name="mi">The member info to check.</param>
		/// <param name="cache">The LLVM IR type cache.</param>
		/// <returns>true if the member is an inline array; otherwise, false.</returns>
		public static bool IsInlineArray (this MemberInfo mi, LlvmIrTypeCache cache)
		{
			var attr = cache.GetNativeAssemblerAttribute (mi);
			return attr != null && attr.InlineArray;
		}

		/// <summary>
		/// Gets the size of an inline array member.
		/// </summary>
		/// <param name="mi">The member info to get the array size for.</param>
		/// <param name="cache">The LLVM IR type cache.</param>
		/// <returns>The size of the inline array, or -1 if the member is not an inline array.</returns>
		public static int GetInlineArraySize (this MemberInfo mi, LlvmIrTypeCache cache)
		{
			var attr = cache.GetNativeAssemblerAttribute (mi);
			if (attr == null || !attr.InlineArray) {
				return -1;
			}

			return attr.InlineArraySize;
		}

		/// <summary>
		/// Determines whether an inline array member needs padding.
		/// </summary>
		/// <param name="mi">The member info to check.</param>
		/// <param name="cache">The LLVM IR type cache.</param>
		/// <returns>true if the inline array needs padding; otherwise, false.</returns>
		public static bool InlineArrayNeedsPadding (this MemberInfo mi, LlvmIrTypeCache cache)
		{
			var attr = cache.GetNativeAssemblerAttribute (mi);
			if (attr == null || !attr.InlineArray) {
				return false;
			}

			return attr.NeedsPadding;
		}

		/// <summary>
		/// Gets the number format to use for a member.
		/// </summary>
		/// <param name="mi">The member info to get the number format for.</param>
		/// <param name="cache">The LLVM IR type cache.</param>
		/// <returns>The number format to use for the member.</returns>
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

using System.Collections.Generic;
using System.Reflection;

namespace Xamarin.Android.Tasks.LLVMIR;

/// <summary>
/// Provides caching for frequently accessed type and member attributes in the LLVM IR generation process.
/// This cache improves performance by avoiding repeated reflection calls for the same attributes.
/// </summary>
class LlvmIrTypeCache
{
    readonly Dictionary<MemberInfo, NativePointerAttribute?> pointerAttributes = [];
    readonly Dictionary<MemberInfo, NativeAssemblerAttribute?> assemblerAttributes = [];

    /// <summary>
    /// Gets the <see cref="NativePointerAttribute"/> for the specified member, using caching to improve performance.
    /// </summary>
    /// <param name="mi">The member info to get the attribute for.</param>
    /// <returns>The <see cref="NativePointerAttribute"/> if present; otherwise, null.</returns>
    public NativePointerAttribute? GetNativePointerAttribute (MemberInfo mi)
    {
        if (!pointerAttributes.TryGetValue (mi, out var attr)) {
            pointerAttributes[mi] = attr = mi.GetCustomAttribute<NativePointerAttribute> ();
        }
        return attr;
    }

    /// <summary>
    /// Gets the <see cref="NativeAssemblerAttribute"/> for the specified member, using caching to improve performance.
    /// </summary>
    /// <param name="mi">The member info to get the attribute for.</param>
    /// <returns>The <see cref="NativeAssemblerAttribute"/> if present; otherwise, null.</returns>
    public NativeAssemblerAttribute? GetNativeAssemblerAttribute (MemberInfo mi)
    {
        if (!assemblerAttributes.TryGetValue (mi, out var attr)) {
            assemblerAttributes[mi] = attr = mi.GetCustomAttribute<NativeAssemblerAttribute> ();
        }
        return attr;
    }
}

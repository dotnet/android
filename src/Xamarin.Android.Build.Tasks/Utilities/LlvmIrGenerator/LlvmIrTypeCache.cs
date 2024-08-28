using System.Collections.Generic;
using System.Reflection;

namespace Xamarin.Android.Tasks.LLVMIR;

class LlvmIrTypeCache
{
    readonly Dictionary<MemberInfo, NativePointerAttribute?> pointerAttributes = [];
    readonly Dictionary<MemberInfo, NativeAssemblerAttribute?> assemblerAttributes = [];

    public NativePointerAttribute? GetNativePointerAttribute (MemberInfo mi)
    {
        if (!pointerAttributes.TryGetValue (mi, out var attr)) {
            pointerAttributes[mi] = attr = mi.GetCustomAttribute<NativePointerAttribute> ();
        }
        return attr;
    }

    public NativeAssemblerAttribute? GetNativeAssemblerAttribute (MemberInfo mi)
    {
        if (!assemblerAttributes.TryGetValue (mi, out var attr)) {
            assemblerAttributes[mi] = attr = mi.GetCustomAttribute<NativeAssemblerAttribute> ();
        }
        return attr;
    }
}

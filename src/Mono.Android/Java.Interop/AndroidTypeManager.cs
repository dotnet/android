using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Java.Interop;

namespace Android.Runtime
{
	/// <summary>
	/// Unified JniTypeManager implementation for Android that delegates type mapping to ITypeMap.
	/// This type manager works with both TypeMapAttributeTypeMap (NativeAOT/CoreCLR) and 
	/// LlvmIrTypeMap (Mono/CoreCLR) implementations.
	/// </summary>
	class AndroidTypeManager : TypeMapTypeManager
	{
		const DynamicallyAccessedMemberTypes Constructors = DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors;
		const DynamicallyAccessedMemberTypes Methods = DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods;
		const DynamicallyAccessedMemberTypes MethodsAndPrivateNested = Methods | DynamicallyAccessedMemberTypes.NonPublicNestedTypes;

		public AndroidTypeManager (ITypeMap typeMap)
			: base(typeMap)
		{
		}

		[return: DynamicallyAccessedMembers (Constructors)]
		protected override Type? GetInvokerTypeCore (
			[DynamicallyAccessedMembers (Constructors)]
			Type type)
		{
			if (!type.IsInterface && !type.IsAbstract) {
				return null;
			}

			// First, try to get invoker type from the JavaPeerProxy attribute
			// This is the AOT-safe and trim-safe approach for the trimmable type map
			var proxy = (JavaPeerProxy?) Attribute.GetCustomAttribute (type, typeof (JavaPeerProxy), inherit: false);
			if (proxy?.InvokerType is Type invokerType) {
				// The linker preserves constructors via typeof(T) in the attribute
				return invokerType;
			}

			// Fallback: use reflection-based lookup (legacy path)
			// TODO: Log warning when this path is taken - we want to eliminate it
			Logger.Log (LogLevel.Warn, "monodroid-typemap", $"GetInvokerTypeCore: falling back to reflection for type '{type.FullName}'");
			return JavaObjectExtensions.GetInvokerType (type);
		}

		public override void RegisterNativeMembers (
				JniType nativeClass,
				[DynamicallyAccessedMembers (MethodsAndPrivateNested)] Type type,
				ReadOnlySpan<char> methods)
		{
			// Note: Dynamic native member registration ([Export] attribute) is NOT supported with the trimmable type map.
			// [Export] requires Mono.Android.Export which uses Reflection.Emit to generate delegates at runtime,
			// which is incompatible with NativeAOT and trimming. Use [JavaCallable] with source generators instead.
			throw new NotSupportedException ($"Dynamic native member registration is not supported with the trimmable type map. Type: {type.FullName}");
		}
	}
}

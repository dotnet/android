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
			// This is the AOT-safe and trim-safe approach for TypeMap v3
			var invokerType = GetInvokerTypeFromProxy (type);
			if (invokerType != null) {
				return invokerType;
			}

			// Fallback: use reflection-based lookup (legacy path)
			// TODO: Log warning when this path is taken - we want to eliminate it
			Logger.Log (LogLevel.Warn, "monodroid-typemap", $"GetInvokerTypeCore: falling back to reflection for type '{type.FullName}'");
			return JavaObjectExtensions.GetInvokerType (type);
		}

		[return: DynamicallyAccessedMembers (Constructors)]
		static Type? GetInvokerTypeFromProxy (
			[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.All)]
			Type type)
		{
			// Look for JavaPeerProxy on the type and check its InvokerType property
			var proxy = (JavaPeerProxy?) Attribute.GetCustomAttribute (type, typeof (JavaPeerProxy), inherit: false);
			return proxy?.InvokerType;
		}

		public override void RegisterNativeMembers (
				JniType nativeClass,
				[DynamicallyAccessedMembers (MethodsAndPrivateNested)] Type type,
				ReadOnlySpan<char> methods)
		{
			// Note: Dynamic native member registration ([Export] attribute) is NOT supported with TypeMap v3.
			// [Export] requires Mono.Android.Export which uses Reflection.Emit to generate delegates at runtime,
			// which is incompatible with NativeAOT and trimming. Use [JavaCallable] with source generators instead.
			throw new NotSupportedException ($"Dynamic native member registration is not supported with TypeMap v3. Type: {type.FullName}");
		}
	}
}

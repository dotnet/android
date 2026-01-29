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
		readonly bool _jniAddNativeMethodRegistrationAttributePresent;

		const DynamicallyAccessedMemberTypes Constructors = DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors;
		const DynamicallyAccessedMemberTypes Methods = DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods;
		const DynamicallyAccessedMemberTypes MethodsAndPrivateNested = Methods | DynamicallyAccessedMemberTypes.NonPublicNestedTypes;

		public AndroidTypeManager (ITypeMap typeMap, bool jniAddNativeMethodRegistrationAttributePresent)
			: base(typeMap)
		{
			_jniAddNativeMethodRegistrationAttributePresent = jniAddNativeMethodRegistrationAttributePresent;
		}

		[return: DynamicallyAccessedMembers (Constructors)]
		protected override Type? GetInvokerTypeCore (
			[DynamicallyAccessedMembers (Constructors)]
			Type type)
		{
			if (!type.IsInterface && !type.IsAbstract) {
				return null;
			}

			// First, try to get invoker type from the JavaPeerProxyWithInvokerAttribute<TInvoker>
			// This is the AOT-safe and trim-safe approach for TypeMap v3
			var invokerType = GetInvokerTypeFromAttribute (type);
			if (invokerType != null) {
				return invokerType;
			}

			// Fallback: use reflection-based lookup (legacy path)
			// TODO: Log warning when this path is taken - we want to eliminate it
			Logger.Log (LogLevel.Warn, "monodroid-typemap", $"GetInvokerTypeCore: falling back to reflection for type '{type.FullName}'");
			return JavaObjectExtensions.GetInvokerType (type);
		}

		[return: DynamicallyAccessedMembers (Constructors)]
		static Type? GetInvokerTypeFromAttribute (
			[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.All)]
			Type type)
		{
			// Look for JavaPeerProxyWithInvokerAttribute<TInvoker> on the type
			// The attribute is a generic type, so we need to find it by its generic type definition
			foreach (var attr in type.GetCustomAttributes (inherit: false)) {
				var attrType = attr.GetType ();
				if (!attrType.IsGenericType) {
					continue;
				}

				var genericDef = attrType.GetGenericTypeDefinition ();
				if (genericDef == typeof (JavaPeerProxyWithInvokerAttribute<>)) {
					// Get the InvokerType property from the attribute
					var invokerTypeProperty = attrType.GetProperty (nameof (JavaPeerProxyWithInvokerAttribute<IJavaPeerable>.InvokerType));
					if (invokerTypeProperty != null) {
						return invokerTypeProperty.GetValue (attr) as Type;
					}
				}
			}

			return null;
		}

		public override void RegisterNativeMembers (
				JniType nativeClass,
				[DynamicallyAccessedMembers (MethodsAndPrivateNested)] Type type,
				ReadOnlySpan<char> methods)
		{
			try {
				if (methods.IsEmpty) {
					if (_jniAddNativeMethodRegistrationAttributePresent)
						base.RegisterNativeMembers (nativeClass, type, methods);
					return;
				}

				Logger.Log (LogLevel.Info, "monodroid", $"RegisterNativeMembers: type={type.FullName}, methods={methods.ToString ()}");
				DynamicNativeMembersRegistration.RegisterNativeMembers (nativeClass, type, methods);
			} catch (Exception e) {
				JniEnvironment.Runtime.RaisePendingException (e);
			}
		}
	}
}

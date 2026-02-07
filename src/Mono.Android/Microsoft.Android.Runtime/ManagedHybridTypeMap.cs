using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Android.Runtime;
using Java.Interop;

namespace Microsoft.Android.Runtime
{
	/// <summary>
	/// <see cref="ITypeMap"/> implementation that wraps <see cref="ManagedTypeMapping"/>'s
	/// hash-based lookups. Used when <see cref="RuntimeFeature.ManagedTypeMap"/> is enabled
	/// (NativeAOT / CoreCLR with managed type maps).
	///
	/// Invoker type resolution uses the <c>TypeNameInvoker</c> convention.
	/// </summary>
	class ManagedHybridTypeMap : ITypeMap
	{
		const DynamicallyAccessedMemberTypes Constructors = DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors;

		public bool TryGetManagedType (string jniTypeName, [NotNullWhen (true)] out Type? managedType)
		{
			return ManagedTypeMapping.TryGetType (jniTypeName, out managedType);
		}

		public bool TryGetJniTypeName (Type managedType, [NotNullWhen (true)] out string? jniTypeName)
		{
			return ManagedTypeMapping.TryGetJniName (managedType, out jniTypeName);
		}

		public IJavaPeerable? CreatePeer (IntPtr handle, JniHandleOwnership transfer, Type? targetType)
		{
			return PeerCreationHelper.CreatePeer (this, ResolveInvokerType, handle, transfer, targetType);
		}

		// Invoker resolution follows the same convention as ManagedTypeManager.GetInvokerTypeCore()
		const string Suffix = "Invoker";
		const string assemblyGetTypeMessage = "'Invoker' types are preserved by the MarkJavaObjects trimmer step.";
		const string makeGenericTypeMessage = "Generic 'Invoker' types are preserved by the MarkJavaObjects trimmer step.";

		[return: DynamicallyAccessedMembers (Constructors)]
		static Type? ResolveInvokerType (Type type)
		{
			if (!type.IsInterface && !type.IsAbstract)
				return null;

			Type[] arguments = type.GetGenericArguments ();
			if (arguments.Length == 0)
				return AssemblyGetType (type.Assembly, type + Suffix);
			Type definition = type.GetGenericTypeDefinition ();
			int bt = definition.FullName!.IndexOf ("`", StringComparison.Ordinal);
			if (bt == -1)
				throw new NotSupportedException ("Generic type doesn't follow generic type naming convention! " + type.FullName);
			Type? suffixDefinition = AssemblyGetType (definition.Assembly,
					definition.FullName.Substring (0, bt) + Suffix + definition.FullName.Substring (bt));
			if (suffixDefinition == null)
				return null;
			return MakeGenericType (suffixDefinition, arguments);
		}

		[UnconditionalSuppressMessage ("Trimming", "IL2026", Justification = assemblyGetTypeMessage)]
		[UnconditionalSuppressMessage ("Trimming", "IL2073", Justification = assemblyGetTypeMessage)]
		[return: DynamicallyAccessedMembers (Constructors)]
		static Type? AssemblyGetType (Assembly assembly, string typeName) =>
			assembly.GetType (typeName);

		[UnconditionalSuppressMessage ("Trimming", "IL2055", Justification = makeGenericTypeMessage)]
		[return: DynamicallyAccessedMembers (Constructors)]
		static Type MakeGenericType (
				[DynamicallyAccessedMembers (Constructors)]
				Type type,
				Type[] arguments) =>
			// FIXME: https://github.com/dotnet/java-interop/issues/1192
			#pragma warning disable IL3050
			type.MakeGenericType (arguments);
			#pragma warning restore IL3050
	}
}

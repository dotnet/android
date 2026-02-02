#nullable enable

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Android.Runtime;

namespace Java.Interop
{
	/// <summary>
	/// Alternative ITypeMap implementation that uses trimmer-safe reflection instead of pre-generated code.
	/// 
	/// Key differences from TypeMapAttributeTypeMap:
	/// 1. Proxies only contain Type reference with [DynamicallyAccessedMembers] annotation
	/// 2. Constructor invocation uses ConstructorInfo.Invoke (reflection, but trimmer-safe)
	/// 3. Marshal methods are registered dynamically via JNI RegisterNatives
	/// 4. No pre-generated LLVM IR stubs - smaller binaries
	/// 
	/// Trade-offs:
	/// - Smaller binary size (no LLVM IR, minimal proxy IL)
	/// - Slower startup (reflection + JIT for marshal methods)
	/// - Better debugging (step through real methods, not generated stubs)
	/// - Same runtime steady-state performance (after warmup)
	/// 
	/// This is a PoC to explore the design space. Not intended for production without further validation.
	/// </summary>
	class ReflectionTypeMap : ITypeMap
	{
		const DynamicallyAccessedMemberTypes Constructors = 
			DynamicallyAccessedMemberTypes.PublicConstructors | 
			DynamicallyAccessedMemberTypes.NonPublicConstructors;

		static readonly Type[] ActivationCtorSignature = new[] { typeof(IntPtr), typeof(JniHandleOwnership) };

		readonly IReadOnlyDictionary<string, Type> _jniToManagedMap;
		readonly ConcurrentDictionary<Type, ReflectionPeerProxy?> _proxyCache = new ();
		readonly ConcurrentDictionary<Type, ConstructorInfo?> _ctorCache = new ();
		readonly ConcurrentDictionary<Type, string> _managedToJniCache = new ();

		public ReflectionTypeMap ()
		{
			_jniToManagedMap = BuildTypeMapFromAttributes ();
		}

		/// <summary>
		/// Scans for ReflectionTypeMapAttribute instances to build the JNI→Managed mapping.
		/// </summary>
		static IReadOnlyDictionary<string, Type> BuildTypeMapFromAttributes ()
		{
			var result = new Dictionary<string, Type> (StringComparer.Ordinal);

			// In a real implementation, this would scan the generated TypeMaps assembly
			// For this PoC, we scan all loaded assemblies for types with ReflectionPeerProxy attributes
			foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies ())
			{
				if (assembly.IsDynamic)
					continue;

				try
				{
					foreach (var type in assembly.GetTypes ())
					{
						var proxy = type.GetCustomAttribute<ReflectionPeerProxy> (inherit: false);
						if (proxy != null)
						{
							var jniName = GetJniNameFromRegisterAttribute (type);
							if (jniName != null)
							{
								result[jniName] = type;
							}
						}
					}
				}
				catch (ReflectionTypeLoadException)
				{
					// Skip assemblies that can't be loaded
				}
			}

			return result;
		}

		static string? GetJniNameFromRegisterAttribute (Type type)
		{
			var register = type.GetCustomAttribute<RegisterAttribute> (inherit: false);
			return register?.Name?.Replace ('.', '/');
		}

		#region ITypeMap Implementation

		public bool TryGetTypesForJniName (string jniSimpleReference, [NotNullWhen (true)] out IEnumerable<Type>? types)
		{
			if (_jniToManagedMap.TryGetValue (jniSimpleReference, out var type))
			{
				types = new[] { type };
				return true;
			}

			types = null;
			return false;
		}

		public bool TryGetJniNameForType (Type type, [NotNullWhen (true)] out string? jniName)
		{
			jniName = _managedToJniCache.GetOrAdd (type, t =>
			{
				var register = t.GetCustomAttribute<RegisterAttribute> (inherit: false);
				return register?.Name?.Replace ('.', '/') ?? string.Empty;
			});

			if (string.IsNullOrEmpty (jniName))
			{
				jniName = null;
				return false;
			}

			return true;
		}

		public IEnumerable<string> GetJniNamesForType (Type type)
		{
			if (TryGetJniNameForType (type, out var jniName))
				yield return jniName;
		}

		public bool TryGetInvokerType (Type type, [NotNullWhen (true)] out Type? invokerType)
		{
			var proxy = GetProxyForManagedType (type);
			invokerType = proxy?.InvokerType;
			return invokerType != null;
		}

		public JavaPeerProxy? GetProxyForManagedType (Type managedType)
		{
			return _proxyCache.GetOrAdd (managedType, t =>
				t.GetCustomAttribute<ReflectionPeerProxy> (inherit: false));
		}

		public IJavaPeerable? CreatePeer (IntPtr handle, JniHandleOwnership transfer, Type? targetType)
		{
			if (handle == IntPtr.Zero)
				return null;

			if (targetType == null)
			{
				// Get class name from JNI handle
				var jniClassName = GetJniClassName (handle);
				if (jniClassName == null || !TryGetTypesForJniName (jniClassName, out var types))
					return null;

				foreach (var type in types)
				{
					targetType = type;
					break;
				}
			}

			if (targetType == null)
				return null;

			// Use reflection to create instance - this is trimmer-safe because
			// ReflectionPeerProxy.TargetType has [DynamicallyAccessedMembers(Constructors)]
			var ctor = GetActivationConstructor (targetType);
			if (ctor == null)
				return null;

			try
			{
				return (IJavaPeerable?) ctor.Invoke (new object[] { handle, transfer });
			}
			catch (TargetInvocationException ex)
			{
				throw ex.InnerException ?? ex;
			}
		}

		ConstructorInfo? GetActivationConstructor (
			[DynamicallyAccessedMembers (Constructors)] Type type)
		{
			return _ctorCache.GetOrAdd (type, t =>
				t.GetConstructor (
					BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
					binder: null,
					ActivationCtorSignature,
					modifiers: null));
		}

		public Array CreateArray (Type elementType, int length, int rank)
		{
			// NOTE: Array.CreateInstance is NOT trimmer-safe
			// This is a known limitation of the reflection-based approach
			// A production implementation would need to either:
			// 1. Pre-generate array factory methods (losing some of the simplicity)
			// 2. Accept this limitation for array-heavy scenarios
			// 3. Use a source generator to create array factories
			return rank switch
			{
				1 => Array.CreateInstance (elementType, length),
				2 => Array.CreateInstance (elementType.MakeArrayType (), length),
				_ => throw new ArgumentOutOfRangeException (nameof (rank), rank, "Rank must be 1 or 2")
			};
		}

		public IntPtr GetFunctionPointer (ReadOnlySpan<char> className, int methodIndex)
		{
			// In the reflection-based approach, we don't have pre-generated function pointers.
			// Marshal methods are registered dynamically via JNI RegisterNatives.
			// This method would not be called in normal operation.
			throw new NotSupportedException (
				"ReflectionTypeMap does not support pre-generated function pointers. " +
				"Use dynamic native member registration instead.");
		}

		#endregion

		#region Helpers

		static string? GetJniClassName (IntPtr handle)
		{
			if (handle == IntPtr.Zero)
				return null;

			try
			{
				var obj = new JniObjectReference (handle, JniObjectReferenceType.Local);
				var klass = JniEnvironment.Types.GetObjectClass (obj);
				try
				{
					return JniEnvironment.Types.GetJniTypeNameFromClass (klass);
				}
				finally
				{
					JniObjectReference.Dispose (ref klass);
				}
			}
			catch
			{
				return null;
			}
		}

		#endregion
	}

	/// <summary>
	/// Minimal proxy attribute for the reflection-based approach.
	/// 
	/// Unlike the full JavaPeerProxy which contains factory methods and function pointer switches,
	/// this only holds a reference to the target type with proper trimmer annotations.
	/// 
	/// Generated example:
	/// <code>
	/// [ReflectionPeerProxy(typeof(MainActivity))]
	/// class MainActivity : Activity { ... }
	/// </code>
	/// 
	/// The generated attribute is ~10 bytes of IL vs ~500+ bytes for full JavaPeerProxy with factories.
	/// </summary>
	[AttributeUsage (AttributeTargets.Class | AttributeTargets.Interface, Inherited = false, AllowMultiple = false)]
	public class ReflectionPeerProxy : JavaPeerProxy
	{
		const DynamicallyAccessedMemberTypes Constructors = 
			DynamicallyAccessedMemberTypes.PublicConstructors | 
			DynamicallyAccessedMemberTypes.NonPublicConstructors;
		const DynamicallyAccessedMemberTypes Methods = 
			DynamicallyAccessedMemberTypes.PublicMethods | 
			DynamicallyAccessedMemberTypes.NonPublicMethods;

		/// <summary>
		/// The target .NET type that this proxy represents.
		/// The DynamicallyAccessedMembers annotation ensures the trimmer preserves
		/// constructors and methods needed for reflection-based activation and marshaling.
		/// </summary>
		[DynamicallyAccessedMembers (Constructors | Methods)]
		public Type TargetType { get; }

		DerivedTypeFactory? _cachedFactory;

		/// <summary>
		/// Creates a proxy for the specified target type.
		/// </summary>
		/// <param name="targetType">The .NET type to wrap.</param>
		public ReflectionPeerProxy (
			[DynamicallyAccessedMembers (Constructors | Methods)] Type targetType)
		{
			TargetType = targetType;
		}

		/// <summary>
		/// Creates a proxy for an interface/abstract type with a separate invoker.
		/// </summary>
		/// <param name="targetType">The interface or abstract type.</param>
		/// <param name="invokerType">The concrete invoker type.</param>
		public ReflectionPeerProxy (
			[DynamicallyAccessedMembers (Constructors | Methods)] Type targetType,
			[DynamicallyAccessedMembers (Constructors)] Type? invokerType)
		{
			TargetType = targetType;
			InvokerType = invokerType;
		}

		static readonly Type[] ActivationCtorSignature = new[] { typeof(IntPtr), typeof(JniHandleOwnership) };

		ConstructorInfo? _cachedCtor;

		/// <summary>
		/// Creates an instance using reflection. This is trimmer-safe because TargetType
		/// has [DynamicallyAccessedMembers(Constructors)].
		/// </summary>
		public override IJavaPeerable CreateInstance (IntPtr handle, JniHandleOwnership transfer)
		{
			_cachedCtor ??= TargetType.GetConstructor (
				BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
				binder: null,
				ActivationCtorSignature,
				modifiers: null);

			if (_cachedCtor == null)
				throw new InvalidOperationException (
					$"No activation constructor (IntPtr, JniHandleOwnership) found on {TargetType.FullName}");

			try
			{
				return (IJavaPeerable) _cachedCtor.Invoke (new object[] { handle, transfer })!;
			}
			catch (TargetInvocationException ex)
			{
				throw ex.InnerException ?? ex;
			}
		}

		/// <summary>
		/// Gets a reflection-based factory for creating derived types.
		/// NOTE: This uses Array.CreateInstance and is NOT fully trimmer-safe.
		/// </summary>
		public override DerivedTypeFactory GetDerivedTypeFactory ()
		{
			return _cachedFactory ??= new ReflectionDerivedTypeFactory (TargetType);
		}
	}

	/// <summary>
	/// Reflection-based implementation of DerivedTypeFactory.
	/// Uses Array.CreateInstance which is NOT trimmer-safe for arbitrary types.
	/// This is only used for debugging/fallback scenarios.
	/// </summary>
	class ReflectionDerivedTypeFactory : DerivedTypeFactory
	{
		readonly Type _targetType;

		public ReflectionDerivedTypeFactory (Type targetType)
		{
			_targetType = targetType;
		}

		[RequiresUnreferencedCode ("Uses Array.CreateInstance which is not AOT-safe.")]
		public override Array CreateArray (int length, int rank)
		{
			return rank switch
			{
				1 => Array.CreateInstance (_targetType, length),
				2 => Array.CreateInstance (_targetType.MakeArrayType (), length),
				3 => Array.CreateInstance (_targetType.MakeArrayType ().MakeArrayType (), length),
				_ => throw new ArgumentOutOfRangeException (nameof (rank), rank, "Rank must be 1, 2, or 3")
			};
		}

		public override IList CreateList ()
		{
			throw new NotSupportedException ("ReflectionDerivedTypeFactory does not support list creation.");
		}

		public override IList CreateListFromHandle (IntPtr handle, JniHandleOwnership transfer)
		{
			throw new NotSupportedException ("ReflectionDerivedTypeFactory does not support list creation.");
		}

		public override ICollection CreateCollectionFromHandle (IntPtr handle, JniHandleOwnership transfer)
		{
			throw new NotSupportedException ("ReflectionDerivedTypeFactory does not support collection creation.");
		}

		public override ICollection CreateSet ()
		{
			throw new NotSupportedException ("ReflectionDerivedTypeFactory does not support set creation.");
		}

		public override ICollection CreateSetFromHandle (IntPtr handle, JniHandleOwnership transfer)
		{
			throw new NotSupportedException ("ReflectionDerivedTypeFactory does not support set creation.");
		}

		public override IDictionary? CreateDictionary (DerivedTypeFactory keyFactory)
		{
			throw new NotSupportedException ("ReflectionDerivedTypeFactory does not support dictionary creation.");
		}

		public override IDictionary? CreateDictionaryFromHandle (DerivedTypeFactory keyFactory, IntPtr handle, JniHandleOwnership transfer)
		{
			throw new NotSupportedException ("ReflectionDerivedTypeFactory does not support dictionary creation.");
		}
	}

	/// <summary>
	/// Alternative attribute for the generated TypeMap assembly that uses the reflection-based approach.
	/// 
	/// Generated example:
	/// <code>
	/// [assembly: ReflectionTypeMapEntry("android/app/Activity", typeof(Android.App.Activity))]
	/// [assembly: ReflectionTypeMapEntry("com/example/MainActivity", typeof(Com.Example.MainActivity))]
	/// </code>
	/// 
	/// This is simpler than TypeMapAttribute<T> which requires a proxy type parameter.
	/// </summary>
	[AttributeUsage (AttributeTargets.Assembly, AllowMultiple = true)]
	public sealed class ReflectionTypeMapEntryAttribute : Attribute
	{
		const DynamicallyAccessedMemberTypes Constructors = 
			DynamicallyAccessedMemberTypes.PublicConstructors | 
			DynamicallyAccessedMemberTypes.NonPublicConstructors;
		const DynamicallyAccessedMemberTypes Methods = 
			DynamicallyAccessedMemberTypes.PublicMethods | 
			DynamicallyAccessedMemberTypes.NonPublicMethods;

		/// <summary>
		/// The JNI class name (e.g., "android/app/Activity").
		/// </summary>
		public string JniClassName { get; }

		/// <summary>
		/// The .NET type that maps to the JNI class.
		/// </summary>
		[DynamicallyAccessedMembers (Constructors | Methods)]
		public Type ManagedType { get; }

		/// <summary>
		/// Optional invoker type for interfaces and abstract classes.
		/// </summary>
		[DynamicallyAccessedMembers (Constructors)]
		public Type? InvokerType { get; }

		public ReflectionTypeMapEntryAttribute (
			string jniClassName,
			[DynamicallyAccessedMembers (Constructors | Methods)] Type managedType,
			[DynamicallyAccessedMembers (Constructors)] Type? invokerType = null)
		{
			JniClassName = jniClassName;
			ManagedType = managedType;
			InvokerType = invokerType;
		}
	}
}

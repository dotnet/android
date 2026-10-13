#nullable enable

using System;
using System.Collections.Concurrent;

namespace Java.Interop
{
	partial class JniPeerMembers {
	public sealed partial class JniInstanceMethods
	{
		internal JniInstanceMethods (JniPeerMembers members)
		{
			DeclaringType   = members.ManagedPeerType;
			this.members    = members;
		}

		JniInstanceMethods (Type declaringType)
		{
			var jvm     = JniEnvironment.Runtime;
			var info    = jvm.TypeManager.GetTypeSignature (declaringType);
			if (info.SimpleReference == null)
				throw new NotSupportedException (
						string.Format ("Cannot create instance of type '{0}': no Java peer type found.",
							declaringType.FullName));

			DeclaringType   = declaringType;
			targetJniTypeName   = info.Name;
			jniPeerType     = new JniType (targetJniTypeName);
			jniPeerType.RegisterWithRuntime ();
		}

		JniPeerMembers?                                     members;
		JniType?                                            jniPeerType;
		readonly string?                                    targetJniTypeName;

		string TargetJniTypeName => targetJniTypeName ?? Members.JniPeerTypeName;

		internal    JniPeerMembers                          Members => members ?? throw new InvalidOperationException ();

		internal    JniType                                 JniPeerType {
			get {return jniPeerType ?? Members?.JniPeerType ?? throw new InvalidOperationException ();}
		}

		readonly Type                                       DeclaringType;

		ConcurrentDictionary<string, JniMethodInfo>?             instanceMethods;
		ConcurrentDictionary<Type, JniInstanceMethods>?          subclassConstructors;

		ConcurrentDictionary<string, JniMethodInfo>               InstanceMethods      => GetOrCreate (ref instanceMethods, 3);
		ConcurrentDictionary<Type, JniInstanceMethods>            SubclassConstructors => GetOrCreate (ref subclassConstructors, 1);

		internal void Dispose ()
		{
			Clear (ref instanceMethods, static method => method.StaticRedirect?.Dispose ());
			Clear (ref subclassConstructors, static value => value.Dispose ());

			if (jniPeerType != null)
				jniPeerType.Dispose ();
			jniPeerType = null;
		}

		public JniMethodInfo GetConstructor (string signature)
		{
			if (signature == null)
				throw new ArgumentNullException (nameof (signature));
			return InstanceMethods.GetOrAdd (signature, static (member, methods) =>
					methods.GetConstructorCore (member), this);
		}

		JniMethodInfo GetConstructorCore (string signature)
		{
			// Constructors are never renamed, but their parameter types can be, so the descriptor
			// still has to be translated.
			var newMethod = JniPeerMembers.GetReplacementMethodInfo (TargetJniTypeName, "<init>", signature);
			if (newMethod.HasValue) {
				var info = newMethod.Value;
				using var t = CreateTargetType (info, TargetJniTypeName);
				if (TryGetInstanceMethod (t, info, "<init>", signature, out var m)) {
					return m;
				}
			}
			return JniPeerType.GetConstructor (signature.AsSpan ());
		}

		internal JniInstanceMethods GetConstructorsForType (Type declaringType)
		{
			if (declaringType == DeclaringType)
				return this;

			// Initialize before publication in case construction recursively accesses this cache.
			return GetOrAdd (
				SubclassConstructors,
				declaringType,
				static (type, _) => new JniInstanceMethods (type),
				this,
				static value => value.Dispose ());
		}

		public JniMethodInfo GetMethodInfo (string encodedMember)
		{
			return GetOrAdd (
				InstanceMethods,
				encodedMember,
				static (member, methods) => {
					ReadOnlySpan<char> method, signature;
					JniPeerMembers.GetNameAndSignature (member, out method, out signature);
					return methods.GetMethodInfo (method, signature);
				},
				this,
				static method => method.StaticRedirect?.Dispose ());
		}

		JniMethodInfo GetMethodInfo (ReadOnlySpan<char> method, ReadOnlySpan<char> signature)
		{
			var m              = (JniMethodInfo?) null;
			var newMethod      = Members.GetReplacementMethodInfo (method, signature);
			if (newMethod.HasValue) {
				var info = newMethod.Value;
				JniType? t = CreateTargetType (info, Members);
				try {
					if (info.TargetJniMethodInstanceToStatic &&
							TryGetStaticMethod (t, info, method, signature, out m)) {
						m.ParameterCount = info.TargetJniMethodParameterCount;
						m.StaticRedirect = t;
						t = null;
						return m;
					}
					if (TryGetInstanceMethod (t, info, method, signature, out m)) {
						return m;
					}
				} finally {
					t?.Dispose ();
				}
				var targetType = GetTargetTypeNameForDiagnostics (info, Members);
				var targetName = GetTargetMethodNameForDiagnostics (info, method);
				var targetSignature = GetTargetMethodSignatureForDiagnostics (info, signature);
				Console.Error.WriteLine ($"warning: For declared method `{Members.JniPeerTypeName}.{method}.{signature}`, could not find requested method `{targetType}.{targetName}.{targetSignature}`!");
			}
			if (JniPeerType.TryGetInstanceMethod (method, signature, out m))
				return m;

			newMethod = JniPeerMembers.GetBaseReplacementMethodInfo (DeclaringType, method, signature);
			if (newMethod.HasValue) {
				var info = newMethod.Value;
				JniType? t = CreateTargetType (info, TargetJniTypeName);
				try {
					if (info.TargetJniMethodInstanceToStatic &&
							TryGetStaticMethod (t, info, method, signature, out m)) {
						m.ParameterCount = info.TargetJniMethodParameterCount;
						m.StaticRedirect = t;
						t = null;
						return m;
					}
					if (TryGetInstanceMethod (t, info, method, signature, out m))
						return m;
				} finally {
					t?.Dispose ();
				}
			}

			return JniPeerType.GetInstanceMethod (method, signature);
		}

		public unsafe JniObjectReference StartCreateInstance (string constructorSignature, Type declaringType, JniArgumentValue* parameters)
		{
			#pragma warning disable CS1717
			parameters = parameters;    // Silence CA1801
			#pragma warning restore CS1717

			if (constructorSignature == null)
				throw new ArgumentNullException (nameof (constructorSignature));
			if (declaringType == null)
				throw new ArgumentNullException (nameof (declaringType));

			var r   = GetConstructorsForType (declaringType)
				.JniPeerType
				.AllocObject ();
			r.Flags = JniObjectReferenceFlags.Alloc;
			return r;
		}

		public unsafe void FinishCreateInstance (string constructorSignature, IJavaPeerable self, JniArgumentValue* parameters)
		{
			if (constructorSignature == null)
				throw new ArgumentNullException (nameof (constructorSignature));
			if (self == null)
				throw new ArgumentNullException (nameof (self));

			var methods = GetConstructorsForType (self.GetType ());
			var ctor    = methods.GetConstructor (constructorSignature);
			JniEnvironment.InstanceMethods.CallNonvirtualVoidMethod (self.PeerReference, methods.JniPeerType.PeerReference, ctor, parameters);
		}
	}
	}
}

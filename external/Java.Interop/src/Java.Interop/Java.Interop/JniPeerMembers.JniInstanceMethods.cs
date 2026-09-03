#nullable enable

using System;
using System.Collections.Concurrent;
using System.Text;

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
			jniPeerType     = new JniType (info.Name);
			jniPeerType.RegisterWithRuntime ();
		}

		JniPeerMembers?                                     members;
		JniType?                                            jniPeerType;

		internal    JniPeerMembers                          Members => members ?? throw new InvalidOperationException ();

		internal    JniType                                 JniPeerType {
			get {return jniPeerType ?? Members?.JniPeerType ?? throw new InvalidOperationException ();}
		}

		readonly Type                                       DeclaringType;

		readonly ConcurrentDictionary<string, JniMethodInfo>    InstanceMethods      = new ConcurrentDictionary<string, JniMethodInfo> (1, 3, StringComparer.Ordinal);
		readonly Utf8ValueCache<JniMethodInfo>                   Utf8InstanceMethods  = new Utf8ValueCache<JniMethodInfo> ();
		readonly ConcurrentDictionary<Type, JniInstanceMethods> SubclassConstructors = new ConcurrentDictionary<Type, JniInstanceMethods> (1, 1);

		internal void Dispose ()
		{
			InstanceMethods.Clear ();
			Utf8InstanceMethods.Clear ();
			foreach (var p in SubclassConstructors.Values)
				p.Dispose ();
			SubclassConstructors.Clear ();

			if (jniPeerType != null)
				jniPeerType.Dispose ();
			jniPeerType = null;
		}

		public JniMethodInfo GetConstructor (string signature)
		{
			if (signature == null)
				throw new ArgumentNullException (nameof (signature));
			return InstanceMethods.GetOrAdd (signature, static (member, methods) =>
					methods.JniPeerType.GetConstructor (member), this);
		}

		public JniMethodInfo GetConstructor (ReadOnlySpan<byte> signature)
		{
			return Utf8InstanceMethods.GetOrAdd (signature, static (member, methods) => {
				Span<byte> terminatedSignature = member.Length + 1 <= 512
					? stackalloc byte [member.Length + 1]
					: new byte [member.Length + 1];
				member.CopyTo (terminatedSignature);
				terminatedSignature [member.Length] = 0;
				return methods.JniPeerType.GetConstructor (terminatedSignature);
			}, this);
		}

		internal JniInstanceMethods GetConstructorsForType (Type declaringType)
		{
			if (declaringType == DeclaringType)
				return this;

			// Initialize before publication in case construction recursively accesses this cache:
			// System.ArgumentException: An item with the same key has already been added. Key: Java.Interop.JavaProxyThrowable
			//    at System.Collections.Generic.Dictionary`2.TryInsert(TKey key, TValue value, InsertionBehavior behavior)
			//    at System.Collections.Generic.Dictionary`2.Add(TKey key, TValue value)
			//    at Java.Interop.JniPeerMembers.JniInstanceMethods.GetConstructorsForType(Type declaringType) in /Users/jon/Developer/src/xamarin/java.interop/src/Java.Interop/Java.Interop/JniPeerMembers.JniInstanceMethods.cs:line 80
			//    at Java.Interop.JniPeerMembers.JniInstanceMethods.GetConstructorsForType(Type declaringType) in /Users/jon/Developer/src/xamarin/java.interop/src/Java.Interop/Java.Interop/JniPeerMembers.JniInstanceMethods.cs:line 80
			//    at Java.Interop.JniPeerMembers.JniInstanceMethods.StartCreateInstance(String constructorSignature, Type declaringType, JniArgumentValue* parameters) in /Users/jon/Developer/src/xamarin/java.interop/src/Java.Interop/Java.Interop/JniPeerMembers.JniInstanceMethods.cs:line 146
			//    at Java.Interop.JavaException..ctor(String message) in /Users/jon/Developer/src/xamarin/java.interop/src/Java.Interop/Java.Interop/JavaException.cs:line 52
			//    at Java.Interop.JavaProxyThrowable..ctor(Exception exception) in /Users/jon/Developer/src/xamarin/java.interop/src/Java.Interop/Java.Interop/JavaProxyThrowable.cs:line 15
			//    at Java.Interop.JniEnvironment.Exceptions.Throw(Exception e) in /Users/jon/Developer/src/xamarin/java.interop/src/Java.Interop/Java.Interop/JniEnvironment.Errors.cs:line 39
			//    at Java.Interop.JniRuntime.RaisePendingException(Exception pendingException) in /Users/jon/Developer/src/xamarin/java.interop/src/Java.Interop/Java.Interop/JniRuntime.cs:line 444
			//    at Java.Interop.JniTransition.Dispose() in /Users/jon/Developer/src/xamarin/java.interop/src/Java.Interop/Java.Interop/JniTransition.cs:line 39
			//    at Java.Interop.ManagedPeer.RegisterNativeMembers(IntPtr jnienv, IntPtr klass, IntPtr n_nativeClass, IntPtr n_methods) in /Users/jon/Developer/src/xamarin/java.interop/src/Java.Interop/Java.Interop/ManagedPeer.cs:line 195
			//    at Java.Interop.NativeMethods.java_interop_jnienv_find_class(IntPtr jnienv, IntPtr& thrown, String classname)
			//    at Java.Interop.NativeMethods.java_interop_jnienv_find_class(IntPtr jnienv, IntPtr& thrown, String classname)
			//    at Java.Interop.JniEnvironment.Types.TryRawFindClass(IntPtr env, String classname, IntPtr& klass, IntPtr& thrown) in /Users/jon/Developer/src/xamarin/java.interop/src/Java.Interop/Java.Interop/JniEnvironment.Types.cs:line 135
			//    at Java.Interop.JniEnvironment.Types.TryFindClass(String classname, Boolean throwOnError) in /Users/jon/Developer/src/xamarin/java.interop/src/Java.Interop/Java.Interop/JniEnvironment.Types.cs:line 49
			//    at Java.Interop.JniEnvironment.Types.FindClass(String classname) in /Users/jon/Developer/src/xamarin/java.interop/src/Java.Interop/Java.Interop/JniEnvironment.Types.cs:line 37
			//    at Java.Interop.JniType..ctor(String classname) in /Users/jon/Developer/src/xamarin/java.interop/src/Java.Interop/Java.Interop/JniType.cs:line 51
			//    at Java.Interop.JniPeerMembers.JniInstanceMethods..ctor(Type declaringType) in /Users/jon/Developer/src/xamarin/java.interop/src/Java.Interop/Java.Interop/JniPeerMembers.JniInstanceMethods.cs:line 27
			//    at Java.Interop.JniPeerMembers.JniInstanceMethods.GetConstructorsForType(Type declaringType) in /Users/jon/Developer/src/xamarin/java.interop/src/Java.Interop/Java.Interop/JniPeerMembers.JniInstanceMethods.cs:line 77
			//    at Java.Interop.JniPeerMembers.JniInstanceMethods.StartCreateInstance(String constructorSignature, Type declaringType, JniArgumentValue* parameters) in /Users/jon/Developer/src/xamarin/java.interop/src/Java.Interop/Java.Interop/JniPeerMembers.JniInstanceMethods.cs:line 146
			return SubclassConstructors.GetOrAdd (declaringType, static type => new JniInstanceMethods (type));
		}

		public JniMethodInfo GetMethodInfo (string encodedMember)
		{
			return InstanceMethods.GetOrAdd (encodedMember, static (member, methods) => {
				string method, signature;
				JniPeerMembers.GetNameAndSignature (member, out method, out signature);
				return methods.GetMethodInfo (method, signature);
			}, this);
		}

		public JniMethodInfo GetMethodInfo (ReadOnlySpan<byte> encodedMember)
		{
			return GetMethodInfo (new JniUtf8EncodedMember (encodedMember));
		}

		public JniMethodInfo GetMethodInfo (JniUtf8EncodedMember encodedMember)
		{
			return Utf8InstanceMethods.GetOrAdd (encodedMember, static (method, signature, methods) =>
					methods.ResolveMethodInfo (method, signature), this);
		}

		JniMethodInfo ResolveMethodInfo (ReadOnlySpan<byte> method, ReadOnlySpan<byte> signature)
		{
			Span<byte> terminatedMethod = method.Length + 1 <= 256
				? stackalloc byte [method.Length + 1]
				: new byte [method.Length + 1];
			Span<byte> terminatedSignature = signature.Length + 1 <= 512
				? stackalloc byte [signature.Length + 1]
				: new byte [signature.Length + 1];
			method.CopyTo (terminatedMethod);
			signature.CopyTo (terminatedSignature);
			terminatedMethod [method.Length]       = 0;
			terminatedSignature [signature.Length] = 0;
			var m                   = (JniMethodInfo?) null;
			var newMethod           = Members.GetReplacementMethodInfo (method, signature);
			if (newMethod.HasValue) {
				var typeName        = newMethod.Value.TargetJniType ?? Members.JniPeerTypeName;
				var replacementName = newMethod.Value.TargetJniMethodName ?? Encoding.UTF8.GetString (method);
				var replacementSig  = newMethod.Value.TargetJniMethodSignature ?? Encoding.UTF8.GetString (signature);

				using var t = new JniType (typeName);
				if (newMethod.Value.TargetJniMethodInstanceToStatic &&
						t.TryGetStaticMethod (replacementName, replacementSig, out m)) {
					m.ParameterCount = newMethod.Value.TargetJniMethodParameterCount;
					m.StaticRedirect = new JniType (typeName);
					return m;
				}
				if (t.TryGetInstanceMethod (replacementName, replacementSig, out m))
					return m;
				Console.Error.WriteLine ($"warning: For declared method `{Members.JniPeerTypeName}.{Encoding.UTF8.GetString (method)}.{Encoding.UTF8.GetString (signature)}`, could not find requested method `{typeName}.{replacementName}.{replacementSig}`!");
			}
			return JniPeerType.GetInstanceMethod (terminatedMethod, terminatedSignature);
		}

		JniMethodInfo GetMethodInfo (string method, string signature)
		{
			var m              = (JniMethodInfo?) null;
			var newMethod      = JniEnvironment.Runtime.TypeManager.GetReplacementMethodInfo (Members.JniPeerTypeName, method, signature);
			if (newMethod.HasValue) {
				var typeName   = newMethod.Value.TargetJniType ?? Members.JniPeerTypeName;
				var methodName = newMethod.Value.TargetJniMethodName ?? method;
				var methodSig  = newMethod.Value.TargetJniMethodSignature ?? signature;

				using var t = new JniType (typeName);
				if (newMethod.Value.TargetJniMethodInstanceToStatic &&
						t.TryGetStaticMethod (methodName, methodSig, out m)) {
					m.ParameterCount = newMethod.Value.TargetJniMethodParameterCount;
					m.StaticRedirect = new JniType (typeName);
					return m;
				}
				if (t.TryGetInstanceMethod (methodName, methodSig, out m)) {
					return m;
				}
				Console.Error.WriteLine ($"warning: For declared method `{Members.JniPeerTypeName}.{method}.{signature}`, could not find requested method `{typeName}.{methodName}.{methodSig}`!");
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

		public unsafe JniObjectReference StartCreateInstance (ReadOnlySpan<byte> constructorSignature, Type declaringType, JniArgumentValue* parameters)
		{
			if (declaringType == null)
				throw new ArgumentNullException (nameof (declaringType));

			var r   = GetConstructorsForType (declaringType)
				.JniPeerType
				.AllocObject ();
			r.Flags = JniObjectReferenceFlags.Alloc;
			return r;
		}

		internal JniObjectReference AllocObject (Type declaringType)
		{
			var r   = GetConstructorsForType (declaringType)
				.JniPeerType
				.AllocObject ();
			r.Flags = JniObjectReferenceFlags.Alloc;
			return r;
		}

		internal unsafe JniObjectReference NewObject (string constructorSignature, Type declaringType, JniArgumentValue* parameters)
		{
			var methods = GetConstructorsForType (declaringType);
			var ctor    = methods.GetConstructor (constructorSignature);
			return methods.JniPeerType.NewObject (ctor, parameters);
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

		public unsafe void FinishCreateInstance (ReadOnlySpan<byte> constructorSignature, IJavaPeerable self, JniArgumentValue* parameters)
		{
			if (self == null)
				throw new ArgumentNullException (nameof (self));

			var methods = GetConstructorsForType (self.GetType ());
			var ctor    = methods.GetConstructor (constructorSignature);
			JniEnvironment.InstanceMethods.CallNonvirtualVoidMethod (self.PeerReference, methods.JniPeerType.PeerReference, ctor, parameters);
		}
	}
	}
}

#nullable enable

using System;
using System.Collections.Generic;

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

		Dictionary<string, JniMethodInfo>                   InstanceMethods = new Dictionary<string, JniMethodInfo>(StringComparer.Ordinal);
		Dictionary<Type, JniInstanceMethods>                SubclassConstructors = new Dictionary<Type, JniInstanceMethods> ();

		internal void Dispose ()
		{
			InstanceMethods.Clear ();
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
			lock (InstanceMethods) {
				if (!InstanceMethods.TryGetValue (signature, out var m)) {
					m = JniPeerType.GetConstructor (signature);
					InstanceMethods.Add (signature, m);
				}
				return m;
			}
		}

		internal JniInstanceMethods GetConstructorsForType (Type declaringType)
		{
			if (declaringType == DeclaringType)
				return this;

			lock (SubclassConstructors) {
				if (!SubclassConstructors.TryGetValue (declaringType, out var methods)) {
					methods = new JniInstanceMethods (declaringType);
					SubclassConstructors.Add (declaringType, methods);
				}
				return methods;
			}
		}

		public JniMethodInfo GetMethodInfo (string encodedMember)
		{
			lock (InstanceMethods) {
				if (InstanceMethods.TryGetValue (encodedMember, out var m)) {
					return m;
				}
			}
			string method, signature;
			JniPeerMembers.GetNameAndSignature (encodedMember, out method, out signature);
			var info = GetMethodInfo (method, signature);
			lock (InstanceMethods) {
				if (InstanceMethods.TryGetValue (encodedMember, out var m)) {
					return m;
				}
				InstanceMethods.Add (encodedMember, info);
			}
			return info;
		}

		JniMethodInfo GetMethodInfo (string method, string signature)
		{
#if NET
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
#endif  // NET
			return JniPeerType.GetInstanceMethod (method, signature);
		}

		public unsafe JniObjectReference StartCreateInstance (string constructorSignature, Type declaringType, JniArgumentValue* parameters)
		{
			if (constructorSignature == null)
				throw new ArgumentNullException (nameof (constructorSignature));
			if (declaringType == null)
				throw new ArgumentNullException (nameof (declaringType));

			if (JniEnvironment.Runtime.NewObjectRequired) {
				return NewObject (constructorSignature, declaringType, parameters);
			}
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

			if (JniEnvironment.Runtime.NewObjectRequired) {
				return;
			}
			var methods = GetConstructorsForType (self.GetType ());
			var ctor    = methods.GetConstructor (constructorSignature);
			JniEnvironment.InstanceMethods.CallNonvirtualVoidMethod (self.PeerReference, methods.JniPeerType.PeerReference, ctor, parameters);
		}
	}
	}
}


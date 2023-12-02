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

			JniInstanceMethods? methods;

			lock (SubclassConstructors) {
				if (SubclassConstructors.TryGetValue (declaringType, out methods))
					return methods;
			}
			// Init outside of `lock` in case we have recursive access:
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
			//    at Java.Lang.Object..ctor() in /Users/jon/Developer/src/xamarin/java.interop/src/Java.Base/obj/Debug-net7.0/mcw/Java.Lang.Object.cs:line 32
			//    at Java.BaseTests.MyIntConsumer..ctor(Action`1 action) in /Users/jon/Developer/src/xamarin/java.interop/tests/Java.Base-Tests/Java.Base/JavaToManagedTests.cs:line 77
			//    at Java.BaseTests.JavaToManagedTests.InterfaceInvokerMethod() in /Users/jon/Developer/src/xamarin/java.interop/tests/Java.Base-Tests/Java.Base/JavaToManagedTests.cs:line 26
			methods = new JniInstanceMethods (declaringType);
			lock (SubclassConstructors) {
				if (SubclassConstructors.TryGetValue (declaringType, out var m))
					return m;
				SubclassConstructors.Add (declaringType, methods);
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


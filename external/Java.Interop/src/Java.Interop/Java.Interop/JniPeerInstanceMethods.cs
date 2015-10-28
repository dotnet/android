using System;
using System.Collections.Generic;

namespace Java.Interop
{
	public sealed partial class JniPeerInstanceMethods
	{
		internal JniPeerInstanceMethods (JniPeerMembers members)
		{
			DeclaringType   = members.ManagedPeerType;
			JniPeerType     = members.JniPeerType;
		}

		JniPeerInstanceMethods (Type declaringType)
		{
			var jvm     = JniEnvironment.Runtime;
			var info    = jvm.GetJniTypeInfoForType (declaringType);
			if (info.SimpleReference == null)
				throw new NotSupportedException (
						string.Format ("Cannot create instance of type '{0}': no Java peer type found.",
							declaringType.FullName));

			DeclaringType   = declaringType;
			JniPeerType     = new JniType (info.Name);
			JniPeerType.RegisterWithVM ();
		}

		internal    JniType                                 JniPeerType;

		readonly Type                                       DeclaringType;

		Dictionary<string, JniInstanceMethodInfo>           InstanceMethods = new Dictionary<string, JniInstanceMethodInfo>();
		Dictionary<Type, JniPeerInstanceMethods>            SubclassConstructors = new Dictionary<Type, JniPeerInstanceMethods> ();

		internal void Dispose ()
		{
			if (JniPeerType == null)
				return;

			// Don't dispose JniPeerType; it's shared with others.
			InstanceMethods         = null;

			foreach (var p in SubclassConstructors.Values)
				p.Dispose ();
			SubclassConstructors    = null;

			JniPeerType = null;
		}

		public JniInstanceMethodInfo GetConstructor (string signature)
		{
			if (signature == null)
				throw new ArgumentNullException ("signature");
			lock (InstanceMethods) {
				JniInstanceMethodInfo m;
				if (!InstanceMethods.TryGetValue (signature, out m)) {
					m = JniPeerType.GetConstructor (signature);
					InstanceMethods.Add (signature, m);
				}
				return m;
			}
		}

		internal JniPeerInstanceMethods GetConstructorsForType (Type declaringType)
		{
			if (declaringType == DeclaringType)
				return this;

			JniPeerInstanceMethods methods;
			lock (SubclassConstructors) {
				if (!SubclassConstructors.TryGetValue (declaringType, out methods)) {
					methods = new JniPeerInstanceMethods (declaringType);
					SubclassConstructors.Add (declaringType, methods);
				}
			}
			return methods;
		}

		public JniInstanceMethodInfo GetMethodInfo (string encodedMember)
		{
			lock (InstanceMethods) {
				JniInstanceMethodInfo m;
				if (!InstanceMethods.TryGetValue (encodedMember, out m)) {
					string method, signature;
					JniPeerMembers.GetNameAndSignature (encodedMember, out method, out signature);
					m = JniPeerType.GetInstanceMethod (method, signature);
					InstanceMethods.Add (encodedMember, m);
				}
				return m;
			}
		}

		public unsafe JniObjectReference StartCreateInstance (string constructorSignature, Type declaringType, JValue* parameters)
		{
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

		internal unsafe JniObjectReference NewObject (string constructorSignature, Type declaringType, JValue* parameters)
		{
			var methods = GetConstructorsForType (declaringType);
			var ctor    = methods.GetConstructor (constructorSignature);
			return methods.JniPeerType.NewObject (ctor, parameters);
		}

		public unsafe void FinishCreateInstance (string constructorSignature, IJavaPeerable self, JValue* parameters)
		{
			if (JniEnvironment.Runtime.NewObjectRequired) {
				return;
			}
			var methods = GetConstructorsForType (self.GetType ());
			var ctor    = methods.GetConstructor (constructorSignature);
			ctor.InvokeNonvirtualVoidMethod (self.PeerReference, methods.JniPeerType.PeerReference, parameters);
		}
	}

#if !XA_INTEGRATION
	struct JniArgumentMarshalInfo<T> {
		JValue                          jvalue;
		JniObjectReference              lref;
		IJavaPeerable                     obj;
		Action<IJavaPeerable, object>     cleanup;

		internal JniArgumentMarshalInfo (T value)
		{
			this        = new JniArgumentMarshalInfo<T> ();
			var jvm     = JniEnvironment.Runtime;
			var info    = jvm.GetJniMarshalInfoForType (typeof (T));
			if (info.CreateJValue != null)
				jvalue = info.CreateJValue (value);
			else if (info.CreateMarshalCollection != null) {
				obj     = info.CreateMarshalCollection (value);
				jvalue  = new JValue (obj);
			} else if (info.CreateLocalRef != null) {
				lref    = info.CreateLocalRef (value);
				jvalue  = new JValue (lref);
			} else
				throw new NotSupportedException ("Don't know how to get a JValue for: " + typeof (T).FullName);
			cleanup     = info.CleanupMarshalCollection;
		}

		public JValue JValue {
			get {return jvalue;}
		}

		public  void    Cleanup (object value)
		{
			if (cleanup != null && obj != null)
				cleanup (obj, value);
			JniEnvironment.References.Dispose (ref lref, JniObjectReferenceOptions.DisposeSourceReference);
		}
	}
#endif  // !XA_INTEGRATION
}


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
			var jvm     = JniEnvironment.Current.JavaVM;
			var info    = jvm.GetJniTypeInfoForType (declaringType);
			if (info.JniTypeName == null)
				throw new NotSupportedException (
						string.Format ("Cannot create instance of type '{0}': no Java peer type found.",
							declaringType.FullName));

			DeclaringType   = declaringType;
			JniPeerType     = new JniType (info.ToString ());
			JniPeerType.RegisterWithVM ();
		}

		internal    JniType                                 JniPeerType;

		readonly Type                                       DeclaringType;

		Dictionary<string, JniInstanceMethodID>             InstanceMethods = new Dictionary<string, JniInstanceMethodID>();
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

		public JniInstanceMethodID GetConstructor (string signature)
		{
			if (signature == null)
				throw new ArgumentNullException ("signature");
			lock (InstanceMethods) {
				JniInstanceMethodID m;
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

		public JniInstanceMethodID GetMethodID (string encodedMember)
		{
			lock (InstanceMethods) {
				JniInstanceMethodID m;
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
			if (JniEnvironment.Current.JavaVM.NewObjectRequired) {
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
			if (JniEnvironment.Current.JavaVM.NewObjectRequired) {
				return;
			}
			var methods = GetConstructorsForType (self.GetType ());
			var ctor    = methods.GetConstructor (constructorSignature);
			ctor.CallNonvirtualVoidMethod (self.PeerReference, methods.JniPeerType.PeerReference, parameters);
		}

		public unsafe void CallVoidMethod (string encodedMember, IJavaPeerable self, JValue* parameters)
		{
			JniPeerMembers.AssertSelf (self);

			if (self.GetType () == DeclaringType || DeclaringType == null) {
				var m = GetMethodID (encodedMember);
				m.CallVirtualVoidMethod (self.PeerReference, parameters);
				return;
			}
			var j = self.JniPeerMembers;
			var n = j.InstanceMethods.GetMethodID (encodedMember);
			n.CallNonvirtualVoidMethod (self.PeerReference, j.JniPeerType.PeerReference, parameters);
		}

		public unsafe bool CallBooleanMethod (string encodedMember, IJavaPeerable self, JValue* parameters)
		{
			JniPeerMembers.AssertSelf (self);

			if (self.GetType () == DeclaringType || DeclaringType == null) {
				var m = GetMethodID (encodedMember);
				return m.CallVirtualBooleanMethod (self.PeerReference, parameters);
			}
			var j = self.JniPeerMembers;
			var n = j.InstanceMethods.GetMethodID (encodedMember);
			return n.CallNonvirtualBooleanMethod (self.PeerReference, j.JniPeerType.PeerReference, parameters);
		}

		public unsafe sbyte CallSByteMethod (string encodedMember, IJavaPeerable self, JValue* parameters)
		{
			JniPeerMembers.AssertSelf (self);

			if (self.GetType () == DeclaringType || DeclaringType == null) {
				var m = GetMethodID (encodedMember);
				return m.CallVirtualSByteMethod (self.PeerReference, parameters);
			}
			var j = self.JniPeerMembers;
			var n = j.InstanceMethods.GetMethodID (encodedMember);
			return n.CallNonvirtualSByteMethod (self.PeerReference, j.JniPeerType.PeerReference, parameters);
		}

		public unsafe char CallCharMethod (string encodedMember, IJavaPeerable self, JValue* parameters)
		{
			JniPeerMembers.AssertSelf (self);

			if (self.GetType () == DeclaringType || DeclaringType == null) {
				var m = GetMethodID (encodedMember);
				return m.CallVirtualCharMethod (self.PeerReference, parameters);
			}
			var j = self.JniPeerMembers;
			var n = j.InstanceMethods.GetMethodID (encodedMember);
			return n.CallNonvirtualCharMethod (self.PeerReference, j.JniPeerType.PeerReference, parameters);
		}

		public unsafe short CallInt16Method (string encodedMember, IJavaPeerable self, JValue* parameters)
		{
			JniPeerMembers.AssertSelf (self);

			if (self.GetType () == DeclaringType || DeclaringType == null) {
				var m = GetMethodID (encodedMember);
				return m.CallVirtualInt16Method (self.PeerReference, parameters);
			}
			var j = self.JniPeerMembers;
			var n = j.InstanceMethods.GetMethodID (encodedMember);
			return n.CallNonvirtualInt16Method (self.PeerReference, j.JniPeerType.PeerReference, parameters);
		}

		public unsafe int CallInt32Method (string encodedMember, IJavaPeerable self, JValue* parameters)
		{
			JniPeerMembers.AssertSelf (self);

			if (self.GetType () == DeclaringType || DeclaringType == null) {
				var m = GetMethodID (encodedMember);
				return m.CallVirtualInt32Method (self.PeerReference, parameters);
			}
			var j = self.JniPeerMembers;
			var n = j.InstanceMethods.GetMethodID (encodedMember);
			return n.CallNonvirtualInt32Method (self.PeerReference, j.JniPeerType.PeerReference, parameters);
		}

		public unsafe long CallInt64Method (string encodedMember, IJavaPeerable self, JValue* parameters)
		{
			JniPeerMembers.AssertSelf (self);

			if (self.GetType () == DeclaringType || DeclaringType == null) {
				var m = GetMethodID (encodedMember);
				return m.CallVirtualInt64Method (self.PeerReference, parameters);
			}
			var j = self.JniPeerMembers;
			var n = j.InstanceMethods.GetMethodID (encodedMember);
			return n.CallNonvirtualInt64Method (self.PeerReference, j.JniPeerType.PeerReference, parameters);
		}

		public unsafe float CallSingleMethod (string encodedMember, IJavaPeerable self, JValue* parameters)
		{
			JniPeerMembers.AssertSelf (self);

			if (self.GetType () == DeclaringType || DeclaringType == null) {
				var m = GetMethodID (encodedMember);
				return m.CallVirtualSingleMethod (self.PeerReference, parameters);
			}
			var j = self.JniPeerMembers;
			var n = j.InstanceMethods.GetMethodID (encodedMember);
			return n.CallNonvirtualSingleMethod (self.PeerReference, j.JniPeerType.PeerReference, parameters);
		}

		public unsafe double CallDoubleMethod (string encodedMember, IJavaPeerable self, JValue* parameters)
		{
			JniPeerMembers.AssertSelf (self);

			if (self.GetType () == DeclaringType || DeclaringType == null) {
				var m = GetMethodID (encodedMember);
				return m.CallVirtualDoubleMethod (self.PeerReference, parameters);
			}
			var j = self.JniPeerMembers;
			var n = j.InstanceMethods.GetMethodID (encodedMember);
			return n.CallNonvirtualDoubleMethod (self.PeerReference, j.JniPeerType.PeerReference, parameters);
		}

		public unsafe JniObjectReference CallObjectMethod (string encodedMember, IJavaPeerable self, JValue* parameters)
		{
			JniPeerMembers.AssertSelf (self);

			if (self.GetType () == DeclaringType || DeclaringType == null) {
				var m = GetMethodID (encodedMember);
				return m.CallVirtualObjectMethod (self.PeerReference, parameters);
			}
			var j = self.JniPeerMembers;
			var n = j.InstanceMethods.GetMethodID (encodedMember);
			return n.CallNonvirtualObjectMethod (self.PeerReference, j.JniPeerType.PeerReference, parameters);
		}
	}

	struct JniArgumentMarshalInfo<T> {
		JValue                          jvalue;
		JniObjectReference              lref;
		IJavaPeerable                     obj;
		Action<IJavaPeerable, object>     cleanup;

		internal JniArgumentMarshalInfo (T value)
		{
			this        = new JniArgumentMarshalInfo<T> ();
			var jvm     = JniEnvironment.Current.JavaVM;
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
			JniEnvironment.Handles.Dispose (ref lref, JniHandleOwnership.Transfer);
		}
	}
}


#nullable enable

using System;

namespace Java.Interop {

	partial class JniPeerMembers {
	partial class JniInstanceFields {

		public bool GetBooleanValue (
			string encodedMember,
			IJavaPeerable   self)
		{
			JniPeerMembers.AssertSelf (self);

			var f   = GetFieldInfo (encodedMember);
			bool r;
			unsafe {
				// SAFETY: f was resolved for this member's field type; self is kept alive through the JNI read below.
				r = JniEnvironment.InstanceFields.GetBooleanField (self.PeerReference, f);
			}
			GC.KeepAlive (self);
			return r;
		}

		public void SetValue (string encodedMember, IJavaPeerable self, bool value)
		{
			JniPeerMembers.AssertSelf (self);

			var f  = GetFieldInfo (encodedMember);
			unsafe {
				// SAFETY: The resolved Boolean field receives its matching JNI value; self stays alive until after the synchronous write.
				JniEnvironment.InstanceFields.SetBooleanField (self.PeerReference, f, value);
			}
			GC.KeepAlive (self);
		}

		public sbyte GetSByteValue (
			string encodedMember,
			IJavaPeerable   self)
		{
			JniPeerMembers.AssertSelf (self);

			var f   = GetFieldInfo (encodedMember);
			sbyte r;
			unsafe {
				// SAFETY: f was resolved for this member's field type; self is kept alive through the JNI read below.
				r = JniEnvironment.InstanceFields.GetByteField (self.PeerReference, f);
			}
			GC.KeepAlive (self);
			return r;
		}

		public void SetValue (string encodedMember, IJavaPeerable self, sbyte value)
		{
			JniPeerMembers.AssertSelf (self);

			var f  = GetFieldInfo (encodedMember);
			unsafe {
				// SAFETY: The resolved Byte field receives its matching JNI value; self stays alive until after the synchronous write.
				JniEnvironment.InstanceFields.SetByteField (self.PeerReference, f, value);
			}
			GC.KeepAlive (self);
		}

		public char GetCharValue (
			string encodedMember,
			IJavaPeerable   self)
		{
			JniPeerMembers.AssertSelf (self);

			var f   = GetFieldInfo (encodedMember);
			char r;
			unsafe {
				// SAFETY: f was resolved for this member's field type; self is kept alive through the JNI read below.
				r = JniEnvironment.InstanceFields.GetCharField (self.PeerReference, f);
			}
			GC.KeepAlive (self);
			return r;
		}

		public void SetValue (string encodedMember, IJavaPeerable self, char value)
		{
			JniPeerMembers.AssertSelf (self);

			var f  = GetFieldInfo (encodedMember);
			unsafe {
				// SAFETY: The resolved Char field receives its matching JNI value; self stays alive until after the synchronous write.
				JniEnvironment.InstanceFields.SetCharField (self.PeerReference, f, value);
			}
			GC.KeepAlive (self);
		}

		public short GetInt16Value (
			string encodedMember,
			IJavaPeerable   self)
		{
			JniPeerMembers.AssertSelf (self);

			var f   = GetFieldInfo (encodedMember);
			short r;
			unsafe {
				// SAFETY: f was resolved for this member's field type; self is kept alive through the JNI read below.
				r = JniEnvironment.InstanceFields.GetShortField (self.PeerReference, f);
			}
			GC.KeepAlive (self);
			return r;
		}

		public void SetValue (string encodedMember, IJavaPeerable self, short value)
		{
			JniPeerMembers.AssertSelf (self);

			var f  = GetFieldInfo (encodedMember);
			unsafe {
				// SAFETY: The resolved Short field receives its matching JNI value; self stays alive until after the synchronous write.
				JniEnvironment.InstanceFields.SetShortField (self.PeerReference, f, value);
			}
			GC.KeepAlive (self);
		}

		public int GetInt32Value (
			string encodedMember,
			IJavaPeerable   self)
		{
			JniPeerMembers.AssertSelf (self);

			var f   = GetFieldInfo (encodedMember);
			int r;
			unsafe {
				// SAFETY: f was resolved for this member's field type; self is kept alive through the JNI read below.
				r = JniEnvironment.InstanceFields.GetIntField (self.PeerReference, f);
			}
			GC.KeepAlive (self);
			return r;
		}

		public void SetValue (string encodedMember, IJavaPeerable self, int value)
		{
			JniPeerMembers.AssertSelf (self);

			var f  = GetFieldInfo (encodedMember);
			unsafe {
				// SAFETY: The resolved Int field receives its matching JNI value; self stays alive until after the synchronous write.
				JniEnvironment.InstanceFields.SetIntField (self.PeerReference, f, value);
			}
			GC.KeepAlive (self);
		}

		public long GetInt64Value (
			string encodedMember,
			IJavaPeerable   self)
		{
			JniPeerMembers.AssertSelf (self);

			var f   = GetFieldInfo (encodedMember);
			long r;
			unsafe {
				// SAFETY: f was resolved for this member's field type; self is kept alive through the JNI read below.
				r = JniEnvironment.InstanceFields.GetLongField (self.PeerReference, f);
			}
			GC.KeepAlive (self);
			return r;
		}

		public void SetValue (string encodedMember, IJavaPeerable self, long value)
		{
			JniPeerMembers.AssertSelf (self);

			var f  = GetFieldInfo (encodedMember);
			unsafe {
				// SAFETY: The resolved Long field receives its matching JNI value; self stays alive until after the synchronous write.
				JniEnvironment.InstanceFields.SetLongField (self.PeerReference, f, value);
			}
			GC.KeepAlive (self);
		}

		public float GetSingleValue (
			string encodedMember,
			IJavaPeerable   self)
		{
			JniPeerMembers.AssertSelf (self);

			var f   = GetFieldInfo (encodedMember);
			float r;
			unsafe {
				// SAFETY: f was resolved for this member's field type; self is kept alive through the JNI read below.
				r = JniEnvironment.InstanceFields.GetFloatField (self.PeerReference, f);
			}
			GC.KeepAlive (self);
			return r;
		}

		public void SetValue (string encodedMember, IJavaPeerable self, float value)
		{
			JniPeerMembers.AssertSelf (self);

			var f  = GetFieldInfo (encodedMember);
			unsafe {
				// SAFETY: The resolved Float field receives its matching JNI value; self stays alive until after the synchronous write.
				JniEnvironment.InstanceFields.SetFloatField (self.PeerReference, f, value);
			}
			GC.KeepAlive (self);
		}

		public double GetDoubleValue (
			string encodedMember,
			IJavaPeerable   self)
		{
			JniPeerMembers.AssertSelf (self);

			var f   = GetFieldInfo (encodedMember);
			double r;
			unsafe {
				// SAFETY: f was resolved for this member's field type; self is kept alive through the JNI read below.
				r = JniEnvironment.InstanceFields.GetDoubleField (self.PeerReference, f);
			}
			GC.KeepAlive (self);
			return r;
		}

		public void SetValue (string encodedMember, IJavaPeerable self, double value)
		{
			JniPeerMembers.AssertSelf (self);

			var f  = GetFieldInfo (encodedMember);
			unsafe {
				// SAFETY: The resolved Double field receives its matching JNI value; self stays alive until after the synchronous write.
				JniEnvironment.InstanceFields.SetDoubleField (self.PeerReference, f, value);
			}
			GC.KeepAlive (self);
		}

		public JniObjectReference GetObjectValue (
			string encodedMember,
			IJavaPeerable   self)
		{
			JniPeerMembers.AssertSelf (self);

			var f   = GetFieldInfo (encodedMember);
			JniObjectReference r;
			unsafe {
				// SAFETY: f was resolved for this member's field type; self is kept alive through the JNI read below.
				r = JniEnvironment.InstanceFields.GetObjectField (self.PeerReference, f);
			}
			GC.KeepAlive (self);
			return r;
		}

		public void SetValue (string encodedMember, IJavaPeerable self, JniObjectReference value)
		{
			JniPeerMembers.AssertSelf (self);

			var f  = GetFieldInfo (encodedMember);
			unsafe {
				// SAFETY: The resolved Object field receives its matching JNI value; self stays alive until after the synchronous write.
				JniEnvironment.InstanceFields.SetObjectField (self.PeerReference, f, value);
			}
			GC.KeepAlive (self);
		}
	}

	partial class JniStaticFields {

		public bool GetBooleanValue (string encodedMember)
		{
			var f   = GetFieldInfo (encodedMember);
			unsafe {
				// SAFETY: f is the cached Boolean field ID for the resolved declaring class; no caller buffer is dereferenced.
				return JniEnvironment.StaticFields.GetStaticBooleanField (GetFieldDeclaringType (f).PeerReference, f);
			}
		}

		public void SetValue (string encodedMember, bool value)
		{
			var f   = GetFieldInfo (encodedMember);
			unsafe {
				// SAFETY: The cached declaring class and field ID match this Boolean value; the call consumes no borrowed native buffer.
				JniEnvironment.StaticFields.SetStaticBooleanField (GetFieldDeclaringType (f).PeerReference, f, value);
			}
		}

		public sbyte GetSByteValue (string encodedMember)
		{
			var f   = GetFieldInfo (encodedMember);
			unsafe {
				// SAFETY: f is the cached Byte field ID for the resolved declaring class; no caller buffer is dereferenced.
				return JniEnvironment.StaticFields.GetStaticByteField (GetFieldDeclaringType (f).PeerReference, f);
			}
		}

		public void SetValue (string encodedMember, sbyte value)
		{
			var f   = GetFieldInfo (encodedMember);
			unsafe {
				// SAFETY: The cached declaring class and field ID match this Byte value; the call consumes no borrowed native buffer.
				JniEnvironment.StaticFields.SetStaticByteField (GetFieldDeclaringType (f).PeerReference, f, value);
			}
		}

		public char GetCharValue (string encodedMember)
		{
			var f   = GetFieldInfo (encodedMember);
			unsafe {
				// SAFETY: f is the cached Char field ID for the resolved declaring class; no caller buffer is dereferenced.
				return JniEnvironment.StaticFields.GetStaticCharField (GetFieldDeclaringType (f).PeerReference, f);
			}
		}

		public void SetValue (string encodedMember, char value)
		{
			var f   = GetFieldInfo (encodedMember);
			unsafe {
				// SAFETY: The cached declaring class and field ID match this Char value; the call consumes no borrowed native buffer.
				JniEnvironment.StaticFields.SetStaticCharField (GetFieldDeclaringType (f).PeerReference, f, value);
			}
		}

		public short GetInt16Value (string encodedMember)
		{
			var f   = GetFieldInfo (encodedMember);
			unsafe {
				// SAFETY: f is the cached Short field ID for the resolved declaring class; no caller buffer is dereferenced.
				return JniEnvironment.StaticFields.GetStaticShortField (GetFieldDeclaringType (f).PeerReference, f);
			}
		}

		public void SetValue (string encodedMember, short value)
		{
			var f   = GetFieldInfo (encodedMember);
			unsafe {
				// SAFETY: The cached declaring class and field ID match this Short value; the call consumes no borrowed native buffer.
				JniEnvironment.StaticFields.SetStaticShortField (GetFieldDeclaringType (f).PeerReference, f, value);
			}
		}

		public int GetInt32Value (string encodedMember)
		{
			var f   = GetFieldInfo (encodedMember);
			unsafe {
				// SAFETY: f is the cached Int field ID for the resolved declaring class; no caller buffer is dereferenced.
				return JniEnvironment.StaticFields.GetStaticIntField (GetFieldDeclaringType (f).PeerReference, f);
			}
		}

		public void SetValue (string encodedMember, int value)
		{
			var f   = GetFieldInfo (encodedMember);
			unsafe {
				// SAFETY: The cached declaring class and field ID match this Int value; the call consumes no borrowed native buffer.
				JniEnvironment.StaticFields.SetStaticIntField (GetFieldDeclaringType (f).PeerReference, f, value);
			}
		}

		public long GetInt64Value (string encodedMember)
		{
			var f   = GetFieldInfo (encodedMember);
			unsafe {
				// SAFETY: f is the cached Long field ID for the resolved declaring class; no caller buffer is dereferenced.
				return JniEnvironment.StaticFields.GetStaticLongField (GetFieldDeclaringType (f).PeerReference, f);
			}
		}

		public void SetValue (string encodedMember, long value)
		{
			var f   = GetFieldInfo (encodedMember);
			unsafe {
				// SAFETY: The cached declaring class and field ID match this Long value; the call consumes no borrowed native buffer.
				JniEnvironment.StaticFields.SetStaticLongField (GetFieldDeclaringType (f).PeerReference, f, value);
			}
		}

		public float GetSingleValue (string encodedMember)
		{
			var f   = GetFieldInfo (encodedMember);
			unsafe {
				// SAFETY: f is the cached Float field ID for the resolved declaring class; no caller buffer is dereferenced.
				return JniEnvironment.StaticFields.GetStaticFloatField (GetFieldDeclaringType (f).PeerReference, f);
			}
		}

		public void SetValue (string encodedMember, float value)
		{
			var f   = GetFieldInfo (encodedMember);
			unsafe {
				// SAFETY: The cached declaring class and field ID match this Float value; the call consumes no borrowed native buffer.
				JniEnvironment.StaticFields.SetStaticFloatField (GetFieldDeclaringType (f).PeerReference, f, value);
			}
		}

		public double GetDoubleValue (string encodedMember)
		{
			var f   = GetFieldInfo (encodedMember);
			unsafe {
				// SAFETY: f is the cached Double field ID for the resolved declaring class; no caller buffer is dereferenced.
				return JniEnvironment.StaticFields.GetStaticDoubleField (GetFieldDeclaringType (f).PeerReference, f);
			}
		}

		public void SetValue (string encodedMember, double value)
		{
			var f   = GetFieldInfo (encodedMember);
			unsafe {
				// SAFETY: The cached declaring class and field ID match this Double value; the call consumes no borrowed native buffer.
				JniEnvironment.StaticFields.SetStaticDoubleField (GetFieldDeclaringType (f).PeerReference, f, value);
			}
		}

		public JniObjectReference GetObjectValue (string encodedMember)
		{
			var f   = GetFieldInfo (encodedMember);
			unsafe {
				// SAFETY: f is the cached Object field ID for the resolved declaring class; no caller buffer is dereferenced.
				return JniEnvironment.StaticFields.GetStaticObjectField (GetFieldDeclaringType (f).PeerReference, f);
			}
		}

		public void SetValue (string encodedMember, JniObjectReference value)
		{
			var f   = GetFieldInfo (encodedMember);
			unsafe {
				// SAFETY: The cached declaring class and field ID match this Object value; the call consumes no borrowed native buffer.
				JniEnvironment.StaticFields.SetStaticObjectField (GetFieldDeclaringType (f).PeerReference, f, value);
			}
		}
	}}
}

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
			var r   = JniEnvironment.InstanceFields.GetBooleanField (self.PeerReference, f);
			GC.KeepAlive (self);
			return r;
		}

		public void SetValue (string encodedMember, IJavaPeerable self, bool value)
		{
			JniPeerMembers.AssertSelf (self);

			var f  = GetFieldInfo (encodedMember);
			JniEnvironment.InstanceFields.SetBooleanField (self.PeerReference, f, value);
			GC.KeepAlive (self);
		}

		public sbyte GetSByteValue (
			string encodedMember,
			IJavaPeerable   self)
		{
			JniPeerMembers.AssertSelf (self);

			var f   = GetFieldInfo (encodedMember);
			var r   = JniEnvironment.InstanceFields.GetByteField (self.PeerReference, f);
			GC.KeepAlive (self);
			return r;
		}

		public void SetValue (string encodedMember, IJavaPeerable self, sbyte value)
		{
			JniPeerMembers.AssertSelf (self);

			var f  = GetFieldInfo (encodedMember);
			JniEnvironment.InstanceFields.SetByteField (self.PeerReference, f, value);
			GC.KeepAlive (self);
		}

		public char GetCharValue (
			string encodedMember,
			IJavaPeerable   self)
		{
			JniPeerMembers.AssertSelf (self);

			var f   = GetFieldInfo (encodedMember);
			var r   = JniEnvironment.InstanceFields.GetCharField (self.PeerReference, f);
			GC.KeepAlive (self);
			return r;
		}

		public void SetValue (string encodedMember, IJavaPeerable self, char value)
		{
			JniPeerMembers.AssertSelf (self);

			var f  = GetFieldInfo (encodedMember);
			JniEnvironment.InstanceFields.SetCharField (self.PeerReference, f, value);
			GC.KeepAlive (self);
		}

		public short GetInt16Value (
			string encodedMember,
			IJavaPeerable   self)
		{
			JniPeerMembers.AssertSelf (self);

			var f   = GetFieldInfo (encodedMember);
			var r   = JniEnvironment.InstanceFields.GetShortField (self.PeerReference, f);
			GC.KeepAlive (self);
			return r;
		}

		public void SetValue (string encodedMember, IJavaPeerable self, short value)
		{
			JniPeerMembers.AssertSelf (self);

			var f  = GetFieldInfo (encodedMember);
			JniEnvironment.InstanceFields.SetShortField (self.PeerReference, f, value);
			GC.KeepAlive (self);
		}

		public int GetInt32Value (
			string encodedMember,
			IJavaPeerable   self)
		{
			JniPeerMembers.AssertSelf (self);

			var f   = GetFieldInfo (encodedMember);
			var r   = JniEnvironment.InstanceFields.GetIntField (self.PeerReference, f);
			GC.KeepAlive (self);
			return r;
		}

		public void SetValue (string encodedMember, IJavaPeerable self, int value)
		{
			JniPeerMembers.AssertSelf (self);

			var f  = GetFieldInfo (encodedMember);
			JniEnvironment.InstanceFields.SetIntField (self.PeerReference, f, value);
			GC.KeepAlive (self);
		}

		public long GetInt64Value (
			string encodedMember,
			IJavaPeerable   self)
		{
			JniPeerMembers.AssertSelf (self);

			var f   = GetFieldInfo (encodedMember);
			var r   = JniEnvironment.InstanceFields.GetLongField (self.PeerReference, f);
			GC.KeepAlive (self);
			return r;
		}

		public void SetValue (string encodedMember, IJavaPeerable self, long value)
		{
			JniPeerMembers.AssertSelf (self);

			var f  = GetFieldInfo (encodedMember);
			JniEnvironment.InstanceFields.SetLongField (self.PeerReference, f, value);
			GC.KeepAlive (self);
		}

		public float GetSingleValue (
			string encodedMember,
			IJavaPeerable   self)
		{
			JniPeerMembers.AssertSelf (self);

			var f   = GetFieldInfo (encodedMember);
			var r   = JniEnvironment.InstanceFields.GetFloatField (self.PeerReference, f);
			GC.KeepAlive (self);
			return r;
		}

		public void SetValue (string encodedMember, IJavaPeerable self, float value)
		{
			JniPeerMembers.AssertSelf (self);

			var f  = GetFieldInfo (encodedMember);
			JniEnvironment.InstanceFields.SetFloatField (self.PeerReference, f, value);
			GC.KeepAlive (self);
		}

		public double GetDoubleValue (
			string encodedMember,
			IJavaPeerable   self)
		{
			JniPeerMembers.AssertSelf (self);

			var f   = GetFieldInfo (encodedMember);
			var r   = JniEnvironment.InstanceFields.GetDoubleField (self.PeerReference, f);
			GC.KeepAlive (self);
			return r;
		}

		public void SetValue (string encodedMember, IJavaPeerable self, double value)
		{
			JniPeerMembers.AssertSelf (self);

			var f  = GetFieldInfo (encodedMember);
			JniEnvironment.InstanceFields.SetDoubleField (self.PeerReference, f, value);
			GC.KeepAlive (self);
		}

		public JniObjectReference GetObjectValue (
			string encodedMember,
			IJavaPeerable   self)
		{
			JniPeerMembers.AssertSelf (self);

			var f   = GetFieldInfo (encodedMember);
			var r   = JniEnvironment.InstanceFields.GetObjectField (self.PeerReference, f);
			GC.KeepAlive (self);
			return r;
		}

		public void SetValue (string encodedMember, IJavaPeerable self, JniObjectReference value)
		{
			JniPeerMembers.AssertSelf (self);

			var f  = GetFieldInfo (encodedMember);
			JniEnvironment.InstanceFields.SetObjectField (self.PeerReference, f, value);
			GC.KeepAlive (self);
		}
	}

	partial class JniStaticFields {

		public bool GetBooleanValue (string encodedMember)
		{
			var f   = GetFieldInfo (encodedMember);
			return JniEnvironment.StaticFields.GetStaticBooleanField (Members.JniPeerType.PeerReference, f);
		}

		public void SetValue (string encodedMember, bool value)
		{
			var f   = GetFieldInfo (encodedMember);
			JniEnvironment.StaticFields.SetStaticBooleanField (Members.JniPeerType.PeerReference, f, value);
		}

		public sbyte GetSByteValue (string encodedMember)
		{
			var f   = GetFieldInfo (encodedMember);
			return JniEnvironment.StaticFields.GetStaticByteField (Members.JniPeerType.PeerReference, f);
		}

		public void SetValue (string encodedMember, sbyte value)
		{
			var f   = GetFieldInfo (encodedMember);
			JniEnvironment.StaticFields.SetStaticByteField (Members.JniPeerType.PeerReference, f, value);
		}

		public char GetCharValue (string encodedMember)
		{
			var f   = GetFieldInfo (encodedMember);
			return JniEnvironment.StaticFields.GetStaticCharField (Members.JniPeerType.PeerReference, f);
		}

		public void SetValue (string encodedMember, char value)
		{
			var f   = GetFieldInfo (encodedMember);
			JniEnvironment.StaticFields.SetStaticCharField (Members.JniPeerType.PeerReference, f, value);
		}

		public short GetInt16Value (string encodedMember)
		{
			var f   = GetFieldInfo (encodedMember);
			return JniEnvironment.StaticFields.GetStaticShortField (Members.JniPeerType.PeerReference, f);
		}

		public void SetValue (string encodedMember, short value)
		{
			var f   = GetFieldInfo (encodedMember);
			JniEnvironment.StaticFields.SetStaticShortField (Members.JniPeerType.PeerReference, f, value);
		}

		public int GetInt32Value (string encodedMember)
		{
			var f   = GetFieldInfo (encodedMember);
			return JniEnvironment.StaticFields.GetStaticIntField (Members.JniPeerType.PeerReference, f);
		}

		public void SetValue (string encodedMember, int value)
		{
			var f   = GetFieldInfo (encodedMember);
			JniEnvironment.StaticFields.SetStaticIntField (Members.JniPeerType.PeerReference, f, value);
		}

		public long GetInt64Value (string encodedMember)
		{
			var f   = GetFieldInfo (encodedMember);
			return JniEnvironment.StaticFields.GetStaticLongField (Members.JniPeerType.PeerReference, f);
		}

		public void SetValue (string encodedMember, long value)
		{
			var f   = GetFieldInfo (encodedMember);
			JniEnvironment.StaticFields.SetStaticLongField (Members.JniPeerType.PeerReference, f, value);
		}

		public float GetSingleValue (string encodedMember)
		{
			var f   = GetFieldInfo (encodedMember);
			return JniEnvironment.StaticFields.GetStaticFloatField (Members.JniPeerType.PeerReference, f);
		}

		public void SetValue (string encodedMember, float value)
		{
			var f   = GetFieldInfo (encodedMember);
			JniEnvironment.StaticFields.SetStaticFloatField (Members.JniPeerType.PeerReference, f, value);
		}

		public double GetDoubleValue (string encodedMember)
		{
			var f   = GetFieldInfo (encodedMember);
			return JniEnvironment.StaticFields.GetStaticDoubleField (Members.JniPeerType.PeerReference, f);
		}

		public void SetValue (string encodedMember, double value)
		{
			var f   = GetFieldInfo (encodedMember);
			JniEnvironment.StaticFields.SetStaticDoubleField (Members.JniPeerType.PeerReference, f, value);
		}

		public JniObjectReference GetObjectValue (string encodedMember)
		{
			var f   = GetFieldInfo (encodedMember);
			return JniEnvironment.StaticFields.GetStaticObjectField (Members.JniPeerType.PeerReference, f);
		}

		public void SetValue (string encodedMember, JniObjectReference value)
		{
			var f   = GetFieldInfo (encodedMember);
			JniEnvironment.StaticFields.SetStaticObjectField (Members.JniPeerType.PeerReference, f, value);
		}
	}}
}

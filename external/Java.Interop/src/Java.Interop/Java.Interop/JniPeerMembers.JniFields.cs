namespace Java.Interop {

	partial class JniPeerMembers {
	partial class JniInstanceFields {

		public bool GetBooleanValue (
			string encodedMember,
			IJavaPeerable   self)
		{
			JniPeerMembers.AssertSelf (self);

			return GetFieldInfo (encodedMember)
				.GetBooleanValue (self.PeerReference);
		}

		public void SetValue (string encodedMember, IJavaPeerable self, bool value)
		{
			JniPeerMembers.AssertSelf (self);

			GetFieldInfo (encodedMember)
				.SetValue (self.PeerReference, value);
		}

		public sbyte GetByteValue (
			string encodedMember,
			IJavaPeerable   self)
		{
			JniPeerMembers.AssertSelf (self);

			return GetFieldInfo (encodedMember)
				.GetByteValue (self.PeerReference);
		}

		public void SetValue (string encodedMember, IJavaPeerable self, sbyte value)
		{
			JniPeerMembers.AssertSelf (self);

			GetFieldInfo (encodedMember)
				.SetValue (self.PeerReference, value);
		}

		public char GetCharValue (
			string encodedMember,
			IJavaPeerable   self)
		{
			JniPeerMembers.AssertSelf (self);

			return GetFieldInfo (encodedMember)
				.GetCharValue (self.PeerReference);
		}

		public void SetValue (string encodedMember, IJavaPeerable self, char value)
		{
			JniPeerMembers.AssertSelf (self);

			GetFieldInfo (encodedMember)
				.SetValue (self.PeerReference, value);
		}

		public short GetInt16Value (
			string encodedMember,
			IJavaPeerable   self)
		{
			JniPeerMembers.AssertSelf (self);

			return GetFieldInfo (encodedMember)
				.GetInt16Value (self.PeerReference);
		}

		public void SetValue (string encodedMember, IJavaPeerable self, short value)
		{
			JniPeerMembers.AssertSelf (self);

			GetFieldInfo (encodedMember)
				.SetValue (self.PeerReference, value);
		}

		public int GetInt32Value (
			string encodedMember,
			IJavaPeerable   self)
		{
			JniPeerMembers.AssertSelf (self);

			return GetFieldInfo (encodedMember)
				.GetInt32Value (self.PeerReference);
		}

		public void SetValue (string encodedMember, IJavaPeerable self, int value)
		{
			JniPeerMembers.AssertSelf (self);

			GetFieldInfo (encodedMember)
				.SetValue (self.PeerReference, value);
		}

		public long GetInt64Value (
			string encodedMember,
			IJavaPeerable   self)
		{
			JniPeerMembers.AssertSelf (self);

			return GetFieldInfo (encodedMember)
				.GetInt64Value (self.PeerReference);
		}

		public void SetValue (string encodedMember, IJavaPeerable self, long value)
		{
			JniPeerMembers.AssertSelf (self);

			GetFieldInfo (encodedMember)
				.SetValue (self.PeerReference, value);
		}

		public float GetSingleValue (
			string encodedMember,
			IJavaPeerable   self)
		{
			JniPeerMembers.AssertSelf (self);

			return GetFieldInfo (encodedMember)
				.GetSingleValue (self.PeerReference);
		}

		public void SetValue (string encodedMember, IJavaPeerable self, float value)
		{
			JniPeerMembers.AssertSelf (self);

			GetFieldInfo (encodedMember)
				.SetValue (self.PeerReference, value);
		}

		public double GetDoubleValue (
			string encodedMember,
			IJavaPeerable   self)
		{
			JniPeerMembers.AssertSelf (self);

			return GetFieldInfo (encodedMember)
				.GetDoubleValue (self.PeerReference);
		}

		public void SetValue (string encodedMember, IJavaPeerable self, double value)
		{
			JniPeerMembers.AssertSelf (self);

			GetFieldInfo (encodedMember)
				.SetValue (self.PeerReference, value);
		}

		public JniObjectReference GetObjectValue (
			string encodedMember,
			IJavaPeerable   self)
		{
			JniPeerMembers.AssertSelf (self);

			return GetFieldInfo (encodedMember)
				.GetObjectValue (self.PeerReference);
		}

		public void SetValue (string encodedMember, IJavaPeerable self, JniObjectReference value)
		{
			JniPeerMembers.AssertSelf (self);

			GetFieldInfo (encodedMember)
				.SetValue (self.PeerReference, value);
		}
	}

	partial class JniStaticFields {

		public bool GetBooleanValue (string encodedMember)
		{
			return GetFieldInfo (encodedMember)
				.GetBooleanValue (Members.JniPeerType.PeerReference);
		}

		public void SetValue (string encodedMember, bool value)
		{
			GetFieldInfo (encodedMember)
				.SetValue (Members.JniPeerType.PeerReference, value);
		}

		public sbyte GetByteValue (string encodedMember)
		{
			return GetFieldInfo (encodedMember)
				.GetByteValue (Members.JniPeerType.PeerReference);
		}

		public void SetValue (string encodedMember, sbyte value)
		{
			GetFieldInfo (encodedMember)
				.SetValue (Members.JniPeerType.PeerReference, value);
		}

		public char GetCharValue (string encodedMember)
		{
			return GetFieldInfo (encodedMember)
				.GetCharValue (Members.JniPeerType.PeerReference);
		}

		public void SetValue (string encodedMember, char value)
		{
			GetFieldInfo (encodedMember)
				.SetValue (Members.JniPeerType.PeerReference, value);
		}

		public short GetInt16Value (string encodedMember)
		{
			return GetFieldInfo (encodedMember)
				.GetInt16Value (Members.JniPeerType.PeerReference);
		}

		public void SetValue (string encodedMember, short value)
		{
			GetFieldInfo (encodedMember)
				.SetValue (Members.JniPeerType.PeerReference, value);
		}

		public int GetInt32Value (string encodedMember)
		{
			return GetFieldInfo (encodedMember)
				.GetInt32Value (Members.JniPeerType.PeerReference);
		}

		public void SetValue (string encodedMember, int value)
		{
			GetFieldInfo (encodedMember)
				.SetValue (Members.JniPeerType.PeerReference, value);
		}

		public long GetInt64Value (string encodedMember)
		{
			return GetFieldInfo (encodedMember)
				.GetInt64Value (Members.JniPeerType.PeerReference);
		}

		public void SetValue (string encodedMember, long value)
		{
			GetFieldInfo (encodedMember)
				.SetValue (Members.JniPeerType.PeerReference, value);
		}

		public float GetSingleValue (string encodedMember)
		{
			return GetFieldInfo (encodedMember)
				.GetSingleValue (Members.JniPeerType.PeerReference);
		}

		public void SetValue (string encodedMember, float value)
		{
			GetFieldInfo (encodedMember)
				.SetValue (Members.JniPeerType.PeerReference, value);
		}

		public double GetDoubleValue (string encodedMember)
		{
			return GetFieldInfo (encodedMember)
				.GetDoubleValue (Members.JniPeerType.PeerReference);
		}

		public void SetValue (string encodedMember, double value)
		{
			GetFieldInfo (encodedMember)
				.SetValue (Members.JniPeerType.PeerReference, value);
		}

		public JniObjectReference GetObjectValue (string encodedMember)
		{
			return GetFieldInfo (encodedMember)
				.GetObjectValue (Members.JniPeerType.PeerReference);
		}

		public void SetValue (string encodedMember, JniObjectReference value)
		{
			GetFieldInfo (encodedMember)
				.SetValue (Members.JniPeerType.PeerReference, value);
		}
	}}
}

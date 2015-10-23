namespace Java.Interop {

	partial class JniPeerInstanceFields {

		public bool GetBooleanValue (
			string encodedMember,
			IJavaPeerable   self)
		{
			JniPeerMembers.AssertSelf (self);

			return GetFieldID (encodedMember)
				.GetBooleanValue (self.PeerReference);
		}

		public void SetValue (string encodedMember, IJavaPeerable self, bool value)
		{
			JniPeerMembers.AssertSelf (self);

			GetFieldID (encodedMember)
				.SetValue (self.PeerReference, value);
		}

		public sbyte GetByteValue (
			string encodedMember,
			IJavaPeerable   self)
		{
			JniPeerMembers.AssertSelf (self);

			return GetFieldID (encodedMember)
				.GetByteValue (self.PeerReference);
		}

		public void SetValue (string encodedMember, IJavaPeerable self, sbyte value)
		{
			JniPeerMembers.AssertSelf (self);

			GetFieldID (encodedMember)
				.SetValue (self.PeerReference, value);
		}

		public char GetCharacterValue (
			string encodedMember,
			IJavaPeerable   self)
		{
			JniPeerMembers.AssertSelf (self);

			return GetFieldID (encodedMember)
				.GetCharacterValue (self.PeerReference);
		}

		public void SetValue (string encodedMember, IJavaPeerable self, char value)
		{
			JniPeerMembers.AssertSelf (self);

			GetFieldID (encodedMember)
				.SetValue (self.PeerReference, value);
		}

		public short GetInt16Value (
			string encodedMember,
			IJavaPeerable   self)
		{
			JniPeerMembers.AssertSelf (self);

			return GetFieldID (encodedMember)
				.GetInt16Value (self.PeerReference);
		}

		public void SetValue (string encodedMember, IJavaPeerable self, short value)
		{
			JniPeerMembers.AssertSelf (self);

			GetFieldID (encodedMember)
				.SetValue (self.PeerReference, value);
		}

		public int GetInt32Value (
			string encodedMember,
			IJavaPeerable   self)
		{
			JniPeerMembers.AssertSelf (self);

			return GetFieldID (encodedMember)
				.GetInt32Value (self.PeerReference);
		}

		public void SetValue (string encodedMember, IJavaPeerable self, int value)
		{
			JniPeerMembers.AssertSelf (self);

			GetFieldID (encodedMember)
				.SetValue (self.PeerReference, value);
		}

		public long GetInt64Value (
			string encodedMember,
			IJavaPeerable   self)
		{
			JniPeerMembers.AssertSelf (self);

			return GetFieldID (encodedMember)
				.GetInt64Value (self.PeerReference);
		}

		public void SetValue (string encodedMember, IJavaPeerable self, long value)
		{
			JniPeerMembers.AssertSelf (self);

			GetFieldID (encodedMember)
				.SetValue (self.PeerReference, value);
		}

		public float GetSingleValue (
			string encodedMember,
			IJavaPeerable   self)
		{
			JniPeerMembers.AssertSelf (self);

			return GetFieldID (encodedMember)
				.GetSingleValue (self.PeerReference);
		}

		public void SetValue (string encodedMember, IJavaPeerable self, float value)
		{
			JniPeerMembers.AssertSelf (self);

			GetFieldID (encodedMember)
				.SetValue (self.PeerReference, value);
		}

		public double GetDoubleValue (
			string encodedMember,
			IJavaPeerable   self)
		{
			JniPeerMembers.AssertSelf (self);

			return GetFieldID (encodedMember)
				.GetDoubleValue (self.PeerReference);
		}

		public void SetValue (string encodedMember, IJavaPeerable self, double value)
		{
			JniPeerMembers.AssertSelf (self);

			GetFieldID (encodedMember)
				.SetValue (self.PeerReference, value);
		}

		public JniObjectReference GetObjectValue (
			string encodedMember,
			IJavaPeerable   self)
		{
			JniPeerMembers.AssertSelf (self);

			return GetFieldID (encodedMember)
				.GetObjectValue (self.PeerReference);
		}

		public void SetValue (string encodedMember, IJavaPeerable self, JniObjectReference value)
		{
			JniPeerMembers.AssertSelf (self);

			GetFieldID (encodedMember)
				.SetValue (self.PeerReference, value);
		}
	}

	partial class JniPeerStaticFields {

		public bool GetBooleanValue (string encodedMember)
		{
			return GetFieldID (encodedMember)
				.GetBooleanValue (Members.JniPeerType.PeerReference);
		}

		public void SetValue (string encodedMember, bool value)
		{
			GetFieldID (encodedMember)
				.SetValue (Members.JniPeerType.PeerReference, value);
		}

		public sbyte GetByteValue (string encodedMember)
		{
			return GetFieldID (encodedMember)
				.GetByteValue (Members.JniPeerType.PeerReference);
		}

		public void SetValue (string encodedMember, sbyte value)
		{
			GetFieldID (encodedMember)
				.SetValue (Members.JniPeerType.PeerReference, value);
		}

		public char GetCharacterValue (string encodedMember)
		{
			return GetFieldID (encodedMember)
				.GetCharacterValue (Members.JniPeerType.PeerReference);
		}

		public void SetValue (string encodedMember, char value)
		{
			GetFieldID (encodedMember)
				.SetValue (Members.JniPeerType.PeerReference, value);
		}

		public short GetInt16Value (string encodedMember)
		{
			return GetFieldID (encodedMember)
				.GetInt16Value (Members.JniPeerType.PeerReference);
		}

		public void SetValue (string encodedMember, short value)
		{
			GetFieldID (encodedMember)
				.SetValue (Members.JniPeerType.PeerReference, value);
		}

		public int GetInt32Value (string encodedMember)
		{
			return GetFieldID (encodedMember)
				.GetInt32Value (Members.JniPeerType.PeerReference);
		}

		public void SetValue (string encodedMember, int value)
		{
			GetFieldID (encodedMember)
				.SetValue (Members.JniPeerType.PeerReference, value);
		}

		public long GetInt64Value (string encodedMember)
		{
			return GetFieldID (encodedMember)
				.GetInt64Value (Members.JniPeerType.PeerReference);
		}

		public void SetValue (string encodedMember, long value)
		{
			GetFieldID (encodedMember)
				.SetValue (Members.JniPeerType.PeerReference, value);
		}

		public float GetSingleValue (string encodedMember)
		{
			return GetFieldID (encodedMember)
				.GetSingleValue (Members.JniPeerType.PeerReference);
		}

		public void SetValue (string encodedMember, float value)
		{
			GetFieldID (encodedMember)
				.SetValue (Members.JniPeerType.PeerReference, value);
		}

		public double GetDoubleValue (string encodedMember)
		{
			return GetFieldID (encodedMember)
				.GetDoubleValue (Members.JniPeerType.PeerReference);
		}

		public void SetValue (string encodedMember, double value)
		{
			GetFieldID (encodedMember)
				.SetValue (Members.JniPeerType.PeerReference, value);
		}

		public JniObjectReference GetObjectValue (string encodedMember)
		{
			return GetFieldID (encodedMember)
				.GetObjectValue (Members.JniPeerType.PeerReference);
		}

		public void SetValue (string encodedMember, JniObjectReference value)
		{
			GetFieldID (encodedMember)
				.SetValue (Members.JniPeerType.PeerReference, value);
		}
	}
}

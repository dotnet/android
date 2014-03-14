namespace Java.Interop {

	partial class JniPeerInstanceFields {

		public bool GetBooleanValue (
			string encodedMember,
			IJavaObject self)
		{
			JniPeerMembers.AssertSelf (self);

			return GetFieldID (encodedMember)
				.GetBooleanValue (self.SafeHandle);
		}

		public void SetValue (string encodedMember, IJavaObject self, bool value)
		{
			JniPeerMembers.AssertSelf (self);

			GetFieldID (encodedMember)
				.SetValue (self.SafeHandle, value);
		}

		public sbyte GetByteValue (
			string encodedMember,
			IJavaObject self)
		{
			JniPeerMembers.AssertSelf (self);

			return GetFieldID (encodedMember)
				.GetByteValue (self.SafeHandle);
		}

		public void SetValue (string encodedMember, IJavaObject self, sbyte value)
		{
			JniPeerMembers.AssertSelf (self);

			GetFieldID (encodedMember)
				.SetValue (self.SafeHandle, value);
		}

		public char GetCharValue (
			string encodedMember,
			IJavaObject self)
		{
			JniPeerMembers.AssertSelf (self);

			return GetFieldID (encodedMember)
				.GetCharValue (self.SafeHandle);
		}

		public void SetValue (string encodedMember, IJavaObject self, char value)
		{
			JniPeerMembers.AssertSelf (self);

			GetFieldID (encodedMember)
				.SetValue (self.SafeHandle, value);
		}

		public short GetInt16Value (
			string encodedMember,
			IJavaObject self)
		{
			JniPeerMembers.AssertSelf (self);

			return GetFieldID (encodedMember)
				.GetInt16Value (self.SafeHandle);
		}

		public void SetValue (string encodedMember, IJavaObject self, short value)
		{
			JniPeerMembers.AssertSelf (self);

			GetFieldID (encodedMember)
				.SetValue (self.SafeHandle, value);
		}

		public int GetInt32Value (
			string encodedMember,
			IJavaObject self)
		{
			JniPeerMembers.AssertSelf (self);

			return GetFieldID (encodedMember)
				.GetInt32Value (self.SafeHandle);
		}

		public void SetValue (string encodedMember, IJavaObject self, int value)
		{
			JniPeerMembers.AssertSelf (self);

			GetFieldID (encodedMember)
				.SetValue (self.SafeHandle, value);
		}

		public long GetInt64Value (
			string encodedMember,
			IJavaObject self)
		{
			JniPeerMembers.AssertSelf (self);

			return GetFieldID (encodedMember)
				.GetInt64Value (self.SafeHandle);
		}

		public void SetValue (string encodedMember, IJavaObject self, long value)
		{
			JniPeerMembers.AssertSelf (self);

			GetFieldID (encodedMember)
				.SetValue (self.SafeHandle, value);
		}

		public float GetSingleValue (
			string encodedMember,
			IJavaObject self)
		{
			JniPeerMembers.AssertSelf (self);

			return GetFieldID (encodedMember)
				.GetSingleValue (self.SafeHandle);
		}

		public void SetValue (string encodedMember, IJavaObject self, float value)
		{
			JniPeerMembers.AssertSelf (self);

			GetFieldID (encodedMember)
				.SetValue (self.SafeHandle, value);
		}

		public double GetDoubleValue (
			string encodedMember,
			IJavaObject self)
		{
			JniPeerMembers.AssertSelf (self);

			return GetFieldID (encodedMember)
				.GetDoubleValue (self.SafeHandle);
		}

		public void SetValue (string encodedMember, IJavaObject self, double value)
		{
			JniPeerMembers.AssertSelf (self);

			GetFieldID (encodedMember)
				.SetValue (self.SafeHandle, value);
		}

		public JniLocalReference GetObjectValue (
			string encodedMember,
			IJavaObject self)
		{
			JniPeerMembers.AssertSelf (self);

			return GetFieldID (encodedMember)
				.GetObjectValue (self.SafeHandle);
		}

		public void SetValue (string encodedMember, IJavaObject self, JniReferenceSafeHandle value)
		{
			JniPeerMembers.AssertSelf (self);

			GetFieldID (encodedMember)
				.SetValue (self.SafeHandle, value);
		}
	}

	partial class JniPeerStaticFields {

		public bool GetBooleanValue (string encodedMember)
		{
			return GetFieldID (encodedMember)
				.GetBooleanValue (Members.JniPeerType.SafeHandle);
		}

		public void SetValue (string encodedMember, bool value)
		{
			GetFieldID (encodedMember)
				.SetValue (Members.JniPeerType.SafeHandle, value);
		}

		public sbyte GetByteValue (string encodedMember)
		{
			return GetFieldID (encodedMember)
				.GetByteValue (Members.JniPeerType.SafeHandle);
		}

		public void SetValue (string encodedMember, sbyte value)
		{
			GetFieldID (encodedMember)
				.SetValue (Members.JniPeerType.SafeHandle, value);
		}

		public char GetCharValue (string encodedMember)
		{
			return GetFieldID (encodedMember)
				.GetCharValue (Members.JniPeerType.SafeHandle);
		}

		public void SetValue (string encodedMember, char value)
		{
			GetFieldID (encodedMember)
				.SetValue (Members.JniPeerType.SafeHandle, value);
		}

		public short GetInt16Value (string encodedMember)
		{
			return GetFieldID (encodedMember)
				.GetInt16Value (Members.JniPeerType.SafeHandle);
		}

		public void SetValue (string encodedMember, short value)
		{
			GetFieldID (encodedMember)
				.SetValue (Members.JniPeerType.SafeHandle, value);
		}

		public int GetInt32Value (string encodedMember)
		{
			return GetFieldID (encodedMember)
				.GetInt32Value (Members.JniPeerType.SafeHandle);
		}

		public void SetValue (string encodedMember, int value)
		{
			GetFieldID (encodedMember)
				.SetValue (Members.JniPeerType.SafeHandle, value);
		}

		public long GetInt64Value (string encodedMember)
		{
			return GetFieldID (encodedMember)
				.GetInt64Value (Members.JniPeerType.SafeHandle);
		}

		public void SetValue (string encodedMember, long value)
		{
			GetFieldID (encodedMember)
				.SetValue (Members.JniPeerType.SafeHandle, value);
		}

		public float GetSingleValue (string encodedMember)
		{
			return GetFieldID (encodedMember)
				.GetSingleValue (Members.JniPeerType.SafeHandle);
		}

		public void SetValue (string encodedMember, float value)
		{
			GetFieldID (encodedMember)
				.SetValue (Members.JniPeerType.SafeHandle, value);
		}

		public double GetDoubleValue (string encodedMember)
		{
			return GetFieldID (encodedMember)
				.GetDoubleValue (Members.JniPeerType.SafeHandle);
		}

		public void SetValue (string encodedMember, double value)
		{
			GetFieldID (encodedMember)
				.SetValue (Members.JniPeerType.SafeHandle, value);
		}

		public JniLocalReference GetObjectValue (string encodedMember)
		{
			return GetFieldID (encodedMember)
				.GetObjectValue (Members.JniPeerType.SafeHandle);
		}

		public void SetValue (string encodedMember, JniReferenceSafeHandle value)
		{
			GetFieldID (encodedMember)
				.SetValue (Members.JniPeerType.SafeHandle, value);
		}
	}
}

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
}

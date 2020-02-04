#nullable enable

using System;

namespace Java.Interop {

	partial class JniPeerMembers {

		partial class JniInstanceMethods {

			public unsafe void InvokeAbstractVoidMethod (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var m   = GetMethodInfo (encodedMember);

				JniEnvironment.InstanceMethods.CallVoidMethod (self.PeerReference, m, parameters);
				return;
			}

			public unsafe void InvokeVirtualVoidMethod (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var declaringType   = DeclaringType;
				if (Members.UsesVirtualDispatch (self, declaringType)) {
					var m   = GetMethodInfo (encodedMember);
					JniEnvironment.InstanceMethods.CallVoidMethod (self.PeerReference, m, parameters);
					return;
				}
				var j = Members.GetPeerMembers (self);
				var n = j.InstanceMethods.GetMethodInfo (encodedMember);
				JniEnvironment.InstanceMethods.CallNonvirtualVoidMethod (self.PeerReference, j.JniPeerType.PeerReference, n, parameters);
				return;
			}

			public unsafe void InvokeNonvirtualVoidMethod (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var m   = GetMethodInfo (encodedMember);

				JniEnvironment.InstanceMethods.CallNonvirtualVoidMethod (self.PeerReference, JniPeerType.PeerReference, m, parameters);
				return;
			}

			public unsafe bool InvokeAbstractBooleanMethod (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var m   = GetMethodInfo (encodedMember);

				return JniEnvironment.InstanceMethods.CallBooleanMethod (self.PeerReference, m, parameters);
				
			}

			public unsafe bool InvokeVirtualBooleanMethod (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var declaringType   = DeclaringType;
				if (Members.UsesVirtualDispatch (self, declaringType)) {
					var m   = GetMethodInfo (encodedMember);
					return JniEnvironment.InstanceMethods.CallBooleanMethod (self.PeerReference, m, parameters);
					
				}
				var j = Members.GetPeerMembers (self);
				var n = j.InstanceMethods.GetMethodInfo (encodedMember);
				return JniEnvironment.InstanceMethods.CallNonvirtualBooleanMethod (self.PeerReference, j.JniPeerType.PeerReference, n, parameters);
				
			}

			public unsafe bool InvokeNonvirtualBooleanMethod (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var m   = GetMethodInfo (encodedMember);

				return JniEnvironment.InstanceMethods.CallNonvirtualBooleanMethod (self.PeerReference, JniPeerType.PeerReference, m, parameters);
				
			}

			public unsafe sbyte InvokeAbstractSByteMethod (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var m   = GetMethodInfo (encodedMember);

				return JniEnvironment.InstanceMethods.CallByteMethod (self.PeerReference, m, parameters);
				
			}

			public unsafe sbyte InvokeVirtualSByteMethod (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var declaringType   = DeclaringType;
				if (Members.UsesVirtualDispatch (self, declaringType)) {
					var m   = GetMethodInfo (encodedMember);
					return JniEnvironment.InstanceMethods.CallByteMethod (self.PeerReference, m, parameters);
					
				}
				var j = Members.GetPeerMembers (self);
				var n = j.InstanceMethods.GetMethodInfo (encodedMember);
				return JniEnvironment.InstanceMethods.CallNonvirtualByteMethod (self.PeerReference, j.JniPeerType.PeerReference, n, parameters);
				
			}

			public unsafe sbyte InvokeNonvirtualSByteMethod (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var m   = GetMethodInfo (encodedMember);

				return JniEnvironment.InstanceMethods.CallNonvirtualByteMethod (self.PeerReference, JniPeerType.PeerReference, m, parameters);
				
			}

			public unsafe char InvokeAbstractCharMethod (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var m   = GetMethodInfo (encodedMember);

				return JniEnvironment.InstanceMethods.CallCharMethod (self.PeerReference, m, parameters);
				
			}

			public unsafe char InvokeVirtualCharMethod (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var declaringType   = DeclaringType;
				if (Members.UsesVirtualDispatch (self, declaringType)) {
					var m   = GetMethodInfo (encodedMember);
					return JniEnvironment.InstanceMethods.CallCharMethod (self.PeerReference, m, parameters);
					
				}
				var j = Members.GetPeerMembers (self);
				var n = j.InstanceMethods.GetMethodInfo (encodedMember);
				return JniEnvironment.InstanceMethods.CallNonvirtualCharMethod (self.PeerReference, j.JniPeerType.PeerReference, n, parameters);
				
			}

			public unsafe char InvokeNonvirtualCharMethod (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var m   = GetMethodInfo (encodedMember);

				return JniEnvironment.InstanceMethods.CallNonvirtualCharMethod (self.PeerReference, JniPeerType.PeerReference, m, parameters);
				
			}

			public unsafe short InvokeAbstractInt16Method (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var m   = GetMethodInfo (encodedMember);

				return JniEnvironment.InstanceMethods.CallShortMethod (self.PeerReference, m, parameters);
				
			}

			public unsafe short InvokeVirtualInt16Method (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var declaringType   = DeclaringType;
				if (Members.UsesVirtualDispatch (self, declaringType)) {
					var m   = GetMethodInfo (encodedMember);
					return JniEnvironment.InstanceMethods.CallShortMethod (self.PeerReference, m, parameters);
					
				}
				var j = Members.GetPeerMembers (self);
				var n = j.InstanceMethods.GetMethodInfo (encodedMember);
				return JniEnvironment.InstanceMethods.CallNonvirtualShortMethod (self.PeerReference, j.JniPeerType.PeerReference, n, parameters);
				
			}

			public unsafe short InvokeNonvirtualInt16Method (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var m   = GetMethodInfo (encodedMember);

				return JniEnvironment.InstanceMethods.CallNonvirtualShortMethod (self.PeerReference, JniPeerType.PeerReference, m, parameters);
				
			}

			public unsafe int InvokeAbstractInt32Method (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var m   = GetMethodInfo (encodedMember);

				return JniEnvironment.InstanceMethods.CallIntMethod (self.PeerReference, m, parameters);
				
			}

			public unsafe int InvokeVirtualInt32Method (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var declaringType   = DeclaringType;
				if (Members.UsesVirtualDispatch (self, declaringType)) {
					var m   = GetMethodInfo (encodedMember);
					return JniEnvironment.InstanceMethods.CallIntMethod (self.PeerReference, m, parameters);
					
				}
				var j = Members.GetPeerMembers (self);
				var n = j.InstanceMethods.GetMethodInfo (encodedMember);
				return JniEnvironment.InstanceMethods.CallNonvirtualIntMethod (self.PeerReference, j.JniPeerType.PeerReference, n, parameters);
				
			}

			public unsafe int InvokeNonvirtualInt32Method (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var m   = GetMethodInfo (encodedMember);

				return JniEnvironment.InstanceMethods.CallNonvirtualIntMethod (self.PeerReference, JniPeerType.PeerReference, m, parameters);
				
			}

			public unsafe long InvokeAbstractInt64Method (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var m   = GetMethodInfo (encodedMember);

				return JniEnvironment.InstanceMethods.CallLongMethod (self.PeerReference, m, parameters);
				
			}

			public unsafe long InvokeVirtualInt64Method (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var declaringType   = DeclaringType;
				if (Members.UsesVirtualDispatch (self, declaringType)) {
					var m   = GetMethodInfo (encodedMember);
					return JniEnvironment.InstanceMethods.CallLongMethod (self.PeerReference, m, parameters);
					
				}
				var j = Members.GetPeerMembers (self);
				var n = j.InstanceMethods.GetMethodInfo (encodedMember);
				return JniEnvironment.InstanceMethods.CallNonvirtualLongMethod (self.PeerReference, j.JniPeerType.PeerReference, n, parameters);
				
			}

			public unsafe long InvokeNonvirtualInt64Method (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var m   = GetMethodInfo (encodedMember);

				return JniEnvironment.InstanceMethods.CallNonvirtualLongMethod (self.PeerReference, JniPeerType.PeerReference, m, parameters);
				
			}

			public unsafe float InvokeAbstractSingleMethod (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var m   = GetMethodInfo (encodedMember);

				return JniEnvironment.InstanceMethods.CallFloatMethod (self.PeerReference, m, parameters);
				
			}

			public unsafe float InvokeVirtualSingleMethod (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var declaringType   = DeclaringType;
				if (Members.UsesVirtualDispatch (self, declaringType)) {
					var m   = GetMethodInfo (encodedMember);
					return JniEnvironment.InstanceMethods.CallFloatMethod (self.PeerReference, m, parameters);
					
				}
				var j = Members.GetPeerMembers (self);
				var n = j.InstanceMethods.GetMethodInfo (encodedMember);
				return JniEnvironment.InstanceMethods.CallNonvirtualFloatMethod (self.PeerReference, j.JniPeerType.PeerReference, n, parameters);
				
			}

			public unsafe float InvokeNonvirtualSingleMethod (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var m   = GetMethodInfo (encodedMember);

				return JniEnvironment.InstanceMethods.CallNonvirtualFloatMethod (self.PeerReference, JniPeerType.PeerReference, m, parameters);
				
			}

			public unsafe double InvokeAbstractDoubleMethod (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var m   = GetMethodInfo (encodedMember);

				return JniEnvironment.InstanceMethods.CallDoubleMethod (self.PeerReference, m, parameters);
				
			}

			public unsafe double InvokeVirtualDoubleMethod (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var declaringType   = DeclaringType;
				if (Members.UsesVirtualDispatch (self, declaringType)) {
					var m   = GetMethodInfo (encodedMember);
					return JniEnvironment.InstanceMethods.CallDoubleMethod (self.PeerReference, m, parameters);
					
				}
				var j = Members.GetPeerMembers (self);
				var n = j.InstanceMethods.GetMethodInfo (encodedMember);
				return JniEnvironment.InstanceMethods.CallNonvirtualDoubleMethod (self.PeerReference, j.JniPeerType.PeerReference, n, parameters);
				
			}

			public unsafe double InvokeNonvirtualDoubleMethod (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var m   = GetMethodInfo (encodedMember);

				return JniEnvironment.InstanceMethods.CallNonvirtualDoubleMethod (self.PeerReference, JniPeerType.PeerReference, m, parameters);
				
			}

			public unsafe JniObjectReference InvokeAbstractObjectMethod (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var m   = GetMethodInfo (encodedMember);

				return JniEnvironment.InstanceMethods.CallObjectMethod (self.PeerReference, m, parameters);
				
			}

			public unsafe JniObjectReference InvokeVirtualObjectMethod (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var declaringType   = DeclaringType;
				if (Members.UsesVirtualDispatch (self, declaringType)) {
					var m   = GetMethodInfo (encodedMember);
					return JniEnvironment.InstanceMethods.CallObjectMethod (self.PeerReference, m, parameters);
					
				}
				var j = Members.GetPeerMembers (self);
				var n = j.InstanceMethods.GetMethodInfo (encodedMember);
				return JniEnvironment.InstanceMethods.CallNonvirtualObjectMethod (self.PeerReference, j.JniPeerType.PeerReference, n, parameters);
				
			}

			public unsafe JniObjectReference InvokeNonvirtualObjectMethod (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var m   = GetMethodInfo (encodedMember);

				return JniEnvironment.InstanceMethods.CallNonvirtualObjectMethod (self.PeerReference, JniPeerType.PeerReference, m, parameters);
				
			}
		}
	}
}

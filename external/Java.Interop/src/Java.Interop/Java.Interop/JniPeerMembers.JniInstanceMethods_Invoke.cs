#nullable enable

using System;
using System.Diagnostics;

namespace Java.Interop {

	partial class JniPeerMembers {

		partial class JniInstanceMethods {

#pragma warning disable CA1801
			static unsafe bool TryInvokeVoidStaticRedirect (JniMethodInfo method, IJavaPeerable self, JniArgumentValue* parameters)
			{
				
#if !NET
				return false;
#else  // NET
				if (method.StaticRedirect == null || !method.ParameterCount.HasValue) {
					return false;
				}

				int c   = method.ParameterCount.Value;
				Debug.Assert (c > 0);
				var p   = stackalloc JniArgumentValue [c];
				p [0]   = new JniArgumentValue (self);
				for (int i = 0; i < c-1; ++i) {
					p [i+1] = parameters [i];
				}

				JniEnvironment.StaticMethods.CallStaticVoidMethod (method.StaticRedirect.PeerReference, method, p);
				return true;
#endif   // NET
			}
#pragma warning restore CA1801

			public unsafe void InvokeAbstractVoidMethod (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				try {
					var m   = GetMethodInfo (encodedMember);
					if (TryInvokeVoidStaticRedirect (m, self, parameters)) {
						return;
					}

					JniEnvironment.InstanceMethods.CallVoidMethod (self.PeerReference, m, parameters);
					return;
				}
				finally {
					GC.KeepAlive (self);
				}
			}

			public unsafe void InvokeVirtualVoidMethod (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				try {
					var declaringType   = DeclaringType;
					if (Members.UsesVirtualDispatch (self, declaringType)) {
						var m   = GetMethodInfo (encodedMember);
						if (TryInvokeVoidStaticRedirect (m, self, parameters)) {
							return;
						}
						JniEnvironment.InstanceMethods.CallVoidMethod (self.PeerReference, m, parameters);
						return;
					}
					var j = Members.GetPeerMembers (self);
					var n = j.InstanceMethods.GetMethodInfo (encodedMember);
					do {
						if (TryInvokeVoidStaticRedirect (n, self, parameters)) {
							return;
						}
						JniEnvironment.InstanceMethods.CallNonvirtualVoidMethod (self.PeerReference, j.JniPeerType.PeerReference, n, parameters);
						return;
					} while (false);
				}
				finally {
					GC.KeepAlive (self);
				}
			}

			public unsafe void InvokeNonvirtualVoidMethod (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var m   = GetMethodInfo (encodedMember);
				try {
					if (TryInvokeVoidStaticRedirect (m, self, parameters)) {
						return;
					}
					JniEnvironment.InstanceMethods.CallNonvirtualVoidMethod (self.PeerReference, JniPeerType.PeerReference, m, parameters);
					return;
				}
				finally {
					GC.KeepAlive (self);
				}
			}

#pragma warning disable CA1801
			static unsafe bool TryInvokeBooleanStaticRedirect (JniMethodInfo method, IJavaPeerable self, JniArgumentValue* parameters, out bool r)
			{
				r = default;
#if !NET
				return false;
#else  // NET
				if (method.StaticRedirect == null || !method.ParameterCount.HasValue) {
					return false;
				}

				int c   = method.ParameterCount.Value;
				Debug.Assert (c > 0);
				var p   = stackalloc JniArgumentValue [c];
				p [0]   = new JniArgumentValue (self);
				for (int i = 0; i < c-1; ++i) {
					p [i+1] = parameters [i];
				}

				r = JniEnvironment.StaticMethods.CallStaticBooleanMethod (method.StaticRedirect.PeerReference, method, p);
				return true;
#endif   // NET
			}
#pragma warning restore CA1801

			public unsafe bool InvokeAbstractBooleanMethod (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				try {
					var m   = GetMethodInfo (encodedMember);
					if (TryInvokeBooleanStaticRedirect (m, self, parameters, out bool r)) {
						return r;
					}

					r = JniEnvironment.InstanceMethods.CallBooleanMethod (self.PeerReference, m, parameters);
					return r;
				}
				finally {
					GC.KeepAlive (self);
				}
			}

			public unsafe bool InvokeVirtualBooleanMethod (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				try {
					var declaringType   = DeclaringType;
					if (Members.UsesVirtualDispatch (self, declaringType)) {
						var m   = GetMethodInfo (encodedMember);
						if (TryInvokeBooleanStaticRedirect (m, self, parameters, out bool r)) {
							return r;
						}
						r = JniEnvironment.InstanceMethods.CallBooleanMethod (self.PeerReference, m, parameters);
						return r;
					}
					var j = Members.GetPeerMembers (self);
					var n = j.InstanceMethods.GetMethodInfo (encodedMember);
					do {
						if (TryInvokeBooleanStaticRedirect (n, self, parameters, out bool r)) {
							return r;
						}
						r = JniEnvironment.InstanceMethods.CallNonvirtualBooleanMethod (self.PeerReference, j.JniPeerType.PeerReference, n, parameters);
						return r;
					} while (false);
				}
				finally {
					GC.KeepAlive (self);
				}
			}

			public unsafe bool InvokeNonvirtualBooleanMethod (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var m   = GetMethodInfo (encodedMember);
				try {
					if (TryInvokeBooleanStaticRedirect (m, self, parameters, out bool r)) {
						return r;
					}
					r = JniEnvironment.InstanceMethods.CallNonvirtualBooleanMethod (self.PeerReference, JniPeerType.PeerReference, m, parameters);
					return r;
				}
				finally {
					GC.KeepAlive (self);
				}
			}

#pragma warning disable CA1801
			static unsafe bool TryInvokeSByteStaticRedirect (JniMethodInfo method, IJavaPeerable self, JniArgumentValue* parameters, out sbyte r)
			{
				r = default;
#if !NET
				return false;
#else  // NET
				if (method.StaticRedirect == null || !method.ParameterCount.HasValue) {
					return false;
				}

				int c   = method.ParameterCount.Value;
				Debug.Assert (c > 0);
				var p   = stackalloc JniArgumentValue [c];
				p [0]   = new JniArgumentValue (self);
				for (int i = 0; i < c-1; ++i) {
					p [i+1] = parameters [i];
				}

				r = JniEnvironment.StaticMethods.CallStaticByteMethod (method.StaticRedirect.PeerReference, method, p);
				return true;
#endif   // NET
			}
#pragma warning restore CA1801

			public unsafe sbyte InvokeAbstractSByteMethod (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				try {
					var m   = GetMethodInfo (encodedMember);
					if (TryInvokeSByteStaticRedirect (m, self, parameters, out sbyte r)) {
						return r;
					}

					r = JniEnvironment.InstanceMethods.CallByteMethod (self.PeerReference, m, parameters);
					return r;
				}
				finally {
					GC.KeepAlive (self);
				}
			}

			public unsafe sbyte InvokeVirtualSByteMethod (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				try {
					var declaringType   = DeclaringType;
					if (Members.UsesVirtualDispatch (self, declaringType)) {
						var m   = GetMethodInfo (encodedMember);
						if (TryInvokeSByteStaticRedirect (m, self, parameters, out sbyte r)) {
							return r;
						}
						r = JniEnvironment.InstanceMethods.CallByteMethod (self.PeerReference, m, parameters);
						return r;
					}
					var j = Members.GetPeerMembers (self);
					var n = j.InstanceMethods.GetMethodInfo (encodedMember);
					do {
						if (TryInvokeSByteStaticRedirect (n, self, parameters, out sbyte r)) {
							return r;
						}
						r = JniEnvironment.InstanceMethods.CallNonvirtualByteMethod (self.PeerReference, j.JniPeerType.PeerReference, n, parameters);
						return r;
					} while (false);
				}
				finally {
					GC.KeepAlive (self);
				}
			}

			public unsafe sbyte InvokeNonvirtualSByteMethod (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var m   = GetMethodInfo (encodedMember);
				try {
					if (TryInvokeSByteStaticRedirect (m, self, parameters, out sbyte r)) {
						return r;
					}
					r = JniEnvironment.InstanceMethods.CallNonvirtualByteMethod (self.PeerReference, JniPeerType.PeerReference, m, parameters);
					return r;
				}
				finally {
					GC.KeepAlive (self);
				}
			}

#pragma warning disable CA1801
			static unsafe bool TryInvokeCharStaticRedirect (JniMethodInfo method, IJavaPeerable self, JniArgumentValue* parameters, out char r)
			{
				r = default;
#if !NET
				return false;
#else  // NET
				if (method.StaticRedirect == null || !method.ParameterCount.HasValue) {
					return false;
				}

				int c   = method.ParameterCount.Value;
				Debug.Assert (c > 0);
				var p   = stackalloc JniArgumentValue [c];
				p [0]   = new JniArgumentValue (self);
				for (int i = 0; i < c-1; ++i) {
					p [i+1] = parameters [i];
				}

				r = JniEnvironment.StaticMethods.CallStaticCharMethod (method.StaticRedirect.PeerReference, method, p);
				return true;
#endif   // NET
			}
#pragma warning restore CA1801

			public unsafe char InvokeAbstractCharMethod (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				try {
					var m   = GetMethodInfo (encodedMember);
					if (TryInvokeCharStaticRedirect (m, self, parameters, out char r)) {
						return r;
					}

					r = JniEnvironment.InstanceMethods.CallCharMethod (self.PeerReference, m, parameters);
					return r;
				}
				finally {
					GC.KeepAlive (self);
				}
			}

			public unsafe char InvokeVirtualCharMethod (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				try {
					var declaringType   = DeclaringType;
					if (Members.UsesVirtualDispatch (self, declaringType)) {
						var m   = GetMethodInfo (encodedMember);
						if (TryInvokeCharStaticRedirect (m, self, parameters, out char r)) {
							return r;
						}
						r = JniEnvironment.InstanceMethods.CallCharMethod (self.PeerReference, m, parameters);
						return r;
					}
					var j = Members.GetPeerMembers (self);
					var n = j.InstanceMethods.GetMethodInfo (encodedMember);
					do {
						if (TryInvokeCharStaticRedirect (n, self, parameters, out char r)) {
							return r;
						}
						r = JniEnvironment.InstanceMethods.CallNonvirtualCharMethod (self.PeerReference, j.JniPeerType.PeerReference, n, parameters);
						return r;
					} while (false);
				}
				finally {
					GC.KeepAlive (self);
				}
			}

			public unsafe char InvokeNonvirtualCharMethod (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var m   = GetMethodInfo (encodedMember);
				try {
					if (TryInvokeCharStaticRedirect (m, self, parameters, out char r)) {
						return r;
					}
					r = JniEnvironment.InstanceMethods.CallNonvirtualCharMethod (self.PeerReference, JniPeerType.PeerReference, m, parameters);
					return r;
				}
				finally {
					GC.KeepAlive (self);
				}
			}

#pragma warning disable CA1801
			static unsafe bool TryInvokeInt16StaticRedirect (JniMethodInfo method, IJavaPeerable self, JniArgumentValue* parameters, out short r)
			{
				r = default;
#if !NET
				return false;
#else  // NET
				if (method.StaticRedirect == null || !method.ParameterCount.HasValue) {
					return false;
				}

				int c   = method.ParameterCount.Value;
				Debug.Assert (c > 0);
				var p   = stackalloc JniArgumentValue [c];
				p [0]   = new JniArgumentValue (self);
				for (int i = 0; i < c-1; ++i) {
					p [i+1] = parameters [i];
				}

				r = JniEnvironment.StaticMethods.CallStaticShortMethod (method.StaticRedirect.PeerReference, method, p);
				return true;
#endif   // NET
			}
#pragma warning restore CA1801

			public unsafe short InvokeAbstractInt16Method (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				try {
					var m   = GetMethodInfo (encodedMember);
					if (TryInvokeInt16StaticRedirect (m, self, parameters, out short r)) {
						return r;
					}

					r = JniEnvironment.InstanceMethods.CallShortMethod (self.PeerReference, m, parameters);
					return r;
				}
				finally {
					GC.KeepAlive (self);
				}
			}

			public unsafe short InvokeVirtualInt16Method (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				try {
					var declaringType   = DeclaringType;
					if (Members.UsesVirtualDispatch (self, declaringType)) {
						var m   = GetMethodInfo (encodedMember);
						if (TryInvokeInt16StaticRedirect (m, self, parameters, out short r)) {
							return r;
						}
						r = JniEnvironment.InstanceMethods.CallShortMethod (self.PeerReference, m, parameters);
						return r;
					}
					var j = Members.GetPeerMembers (self);
					var n = j.InstanceMethods.GetMethodInfo (encodedMember);
					do {
						if (TryInvokeInt16StaticRedirect (n, self, parameters, out short r)) {
							return r;
						}
						r = JniEnvironment.InstanceMethods.CallNonvirtualShortMethod (self.PeerReference, j.JniPeerType.PeerReference, n, parameters);
						return r;
					} while (false);
				}
				finally {
					GC.KeepAlive (self);
				}
			}

			public unsafe short InvokeNonvirtualInt16Method (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var m   = GetMethodInfo (encodedMember);
				try {
					if (TryInvokeInt16StaticRedirect (m, self, parameters, out short r)) {
						return r;
					}
					r = JniEnvironment.InstanceMethods.CallNonvirtualShortMethod (self.PeerReference, JniPeerType.PeerReference, m, parameters);
					return r;
				}
				finally {
					GC.KeepAlive (self);
				}
			}

#pragma warning disable CA1801
			static unsafe bool TryInvokeInt32StaticRedirect (JniMethodInfo method, IJavaPeerable self, JniArgumentValue* parameters, out int r)
			{
				r = default;
#if !NET
				return false;
#else  // NET
				if (method.StaticRedirect == null || !method.ParameterCount.HasValue) {
					return false;
				}

				int c   = method.ParameterCount.Value;
				Debug.Assert (c > 0);
				var p   = stackalloc JniArgumentValue [c];
				p [0]   = new JniArgumentValue (self);
				for (int i = 0; i < c-1; ++i) {
					p [i+1] = parameters [i];
				}

				r = JniEnvironment.StaticMethods.CallStaticIntMethod (method.StaticRedirect.PeerReference, method, p);
				return true;
#endif   // NET
			}
#pragma warning restore CA1801

			public unsafe int InvokeAbstractInt32Method (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				try {
					var m   = GetMethodInfo (encodedMember);
					if (TryInvokeInt32StaticRedirect (m, self, parameters, out int r)) {
						return r;
					}

					r = JniEnvironment.InstanceMethods.CallIntMethod (self.PeerReference, m, parameters);
					return r;
				}
				finally {
					GC.KeepAlive (self);
				}
			}

			public unsafe int InvokeVirtualInt32Method (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				try {
					var declaringType   = DeclaringType;
					if (Members.UsesVirtualDispatch (self, declaringType)) {
						var m   = GetMethodInfo (encodedMember);
						if (TryInvokeInt32StaticRedirect (m, self, parameters, out int r)) {
							return r;
						}
						r = JniEnvironment.InstanceMethods.CallIntMethod (self.PeerReference, m, parameters);
						return r;
					}
					var j = Members.GetPeerMembers (self);
					var n = j.InstanceMethods.GetMethodInfo (encodedMember);
					do {
						if (TryInvokeInt32StaticRedirect (n, self, parameters, out int r)) {
							return r;
						}
						r = JniEnvironment.InstanceMethods.CallNonvirtualIntMethod (self.PeerReference, j.JniPeerType.PeerReference, n, parameters);
						return r;
					} while (false);
				}
				finally {
					GC.KeepAlive (self);
				}
			}

			public unsafe int InvokeNonvirtualInt32Method (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var m   = GetMethodInfo (encodedMember);
				try {
					if (TryInvokeInt32StaticRedirect (m, self, parameters, out int r)) {
						return r;
					}
					r = JniEnvironment.InstanceMethods.CallNonvirtualIntMethod (self.PeerReference, JniPeerType.PeerReference, m, parameters);
					return r;
				}
				finally {
					GC.KeepAlive (self);
				}
			}

#pragma warning disable CA1801
			static unsafe bool TryInvokeInt64StaticRedirect (JniMethodInfo method, IJavaPeerable self, JniArgumentValue* parameters, out long r)
			{
				r = default;
#if !NET
				return false;
#else  // NET
				if (method.StaticRedirect == null || !method.ParameterCount.HasValue) {
					return false;
				}

				int c   = method.ParameterCount.Value;
				Debug.Assert (c > 0);
				var p   = stackalloc JniArgumentValue [c];
				p [0]   = new JniArgumentValue (self);
				for (int i = 0; i < c-1; ++i) {
					p [i+1] = parameters [i];
				}

				r = JniEnvironment.StaticMethods.CallStaticLongMethod (method.StaticRedirect.PeerReference, method, p);
				return true;
#endif   // NET
			}
#pragma warning restore CA1801

			public unsafe long InvokeAbstractInt64Method (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				try {
					var m   = GetMethodInfo (encodedMember);
					if (TryInvokeInt64StaticRedirect (m, self, parameters, out long r)) {
						return r;
					}

					r = JniEnvironment.InstanceMethods.CallLongMethod (self.PeerReference, m, parameters);
					return r;
				}
				finally {
					GC.KeepAlive (self);
				}
			}

			public unsafe long InvokeVirtualInt64Method (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				try {
					var declaringType   = DeclaringType;
					if (Members.UsesVirtualDispatch (self, declaringType)) {
						var m   = GetMethodInfo (encodedMember);
						if (TryInvokeInt64StaticRedirect (m, self, parameters, out long r)) {
							return r;
						}
						r = JniEnvironment.InstanceMethods.CallLongMethod (self.PeerReference, m, parameters);
						return r;
					}
					var j = Members.GetPeerMembers (self);
					var n = j.InstanceMethods.GetMethodInfo (encodedMember);
					do {
						if (TryInvokeInt64StaticRedirect (n, self, parameters, out long r)) {
							return r;
						}
						r = JniEnvironment.InstanceMethods.CallNonvirtualLongMethod (self.PeerReference, j.JniPeerType.PeerReference, n, parameters);
						return r;
					} while (false);
				}
				finally {
					GC.KeepAlive (self);
				}
			}

			public unsafe long InvokeNonvirtualInt64Method (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var m   = GetMethodInfo (encodedMember);
				try {
					if (TryInvokeInt64StaticRedirect (m, self, parameters, out long r)) {
						return r;
					}
					r = JniEnvironment.InstanceMethods.CallNonvirtualLongMethod (self.PeerReference, JniPeerType.PeerReference, m, parameters);
					return r;
				}
				finally {
					GC.KeepAlive (self);
				}
			}

#pragma warning disable CA1801
			static unsafe bool TryInvokeSingleStaticRedirect (JniMethodInfo method, IJavaPeerable self, JniArgumentValue* parameters, out float r)
			{
				r = default;
#if !NET
				return false;
#else  // NET
				if (method.StaticRedirect == null || !method.ParameterCount.HasValue) {
					return false;
				}

				int c   = method.ParameterCount.Value;
				Debug.Assert (c > 0);
				var p   = stackalloc JniArgumentValue [c];
				p [0]   = new JniArgumentValue (self);
				for (int i = 0; i < c-1; ++i) {
					p [i+1] = parameters [i];
				}

				r = JniEnvironment.StaticMethods.CallStaticFloatMethod (method.StaticRedirect.PeerReference, method, p);
				return true;
#endif   // NET
			}
#pragma warning restore CA1801

			public unsafe float InvokeAbstractSingleMethod (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				try {
					var m   = GetMethodInfo (encodedMember);
					if (TryInvokeSingleStaticRedirect (m, self, parameters, out float r)) {
						return r;
					}

					r = JniEnvironment.InstanceMethods.CallFloatMethod (self.PeerReference, m, parameters);
					return r;
				}
				finally {
					GC.KeepAlive (self);
				}
			}

			public unsafe float InvokeVirtualSingleMethod (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				try {
					var declaringType   = DeclaringType;
					if (Members.UsesVirtualDispatch (self, declaringType)) {
						var m   = GetMethodInfo (encodedMember);
						if (TryInvokeSingleStaticRedirect (m, self, parameters, out float r)) {
							return r;
						}
						r = JniEnvironment.InstanceMethods.CallFloatMethod (self.PeerReference, m, parameters);
						return r;
					}
					var j = Members.GetPeerMembers (self);
					var n = j.InstanceMethods.GetMethodInfo (encodedMember);
					do {
						if (TryInvokeSingleStaticRedirect (n, self, parameters, out float r)) {
							return r;
						}
						r = JniEnvironment.InstanceMethods.CallNonvirtualFloatMethod (self.PeerReference, j.JniPeerType.PeerReference, n, parameters);
						return r;
					} while (false);
				}
				finally {
					GC.KeepAlive (self);
				}
			}

			public unsafe float InvokeNonvirtualSingleMethod (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var m   = GetMethodInfo (encodedMember);
				try {
					if (TryInvokeSingleStaticRedirect (m, self, parameters, out float r)) {
						return r;
					}
					r = JniEnvironment.InstanceMethods.CallNonvirtualFloatMethod (self.PeerReference, JniPeerType.PeerReference, m, parameters);
					return r;
				}
				finally {
					GC.KeepAlive (self);
				}
			}

#pragma warning disable CA1801
			static unsafe bool TryInvokeDoubleStaticRedirect (JniMethodInfo method, IJavaPeerable self, JniArgumentValue* parameters, out double r)
			{
				r = default;
#if !NET
				return false;
#else  // NET
				if (method.StaticRedirect == null || !method.ParameterCount.HasValue) {
					return false;
				}

				int c   = method.ParameterCount.Value;
				Debug.Assert (c > 0);
				var p   = stackalloc JniArgumentValue [c];
				p [0]   = new JniArgumentValue (self);
				for (int i = 0; i < c-1; ++i) {
					p [i+1] = parameters [i];
				}

				r = JniEnvironment.StaticMethods.CallStaticDoubleMethod (method.StaticRedirect.PeerReference, method, p);
				return true;
#endif   // NET
			}
#pragma warning restore CA1801

			public unsafe double InvokeAbstractDoubleMethod (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				try {
					var m   = GetMethodInfo (encodedMember);
					if (TryInvokeDoubleStaticRedirect (m, self, parameters, out double r)) {
						return r;
					}

					r = JniEnvironment.InstanceMethods.CallDoubleMethod (self.PeerReference, m, parameters);
					return r;
				}
				finally {
					GC.KeepAlive (self);
				}
			}

			public unsafe double InvokeVirtualDoubleMethod (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				try {
					var declaringType   = DeclaringType;
					if (Members.UsesVirtualDispatch (self, declaringType)) {
						var m   = GetMethodInfo (encodedMember);
						if (TryInvokeDoubleStaticRedirect (m, self, parameters, out double r)) {
							return r;
						}
						r = JniEnvironment.InstanceMethods.CallDoubleMethod (self.PeerReference, m, parameters);
						return r;
					}
					var j = Members.GetPeerMembers (self);
					var n = j.InstanceMethods.GetMethodInfo (encodedMember);
					do {
						if (TryInvokeDoubleStaticRedirect (n, self, parameters, out double r)) {
							return r;
						}
						r = JniEnvironment.InstanceMethods.CallNonvirtualDoubleMethod (self.PeerReference, j.JniPeerType.PeerReference, n, parameters);
						return r;
					} while (false);
				}
				finally {
					GC.KeepAlive (self);
				}
			}

			public unsafe double InvokeNonvirtualDoubleMethod (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var m   = GetMethodInfo (encodedMember);
				try {
					if (TryInvokeDoubleStaticRedirect (m, self, parameters, out double r)) {
						return r;
					}
					r = JniEnvironment.InstanceMethods.CallNonvirtualDoubleMethod (self.PeerReference, JniPeerType.PeerReference, m, parameters);
					return r;
				}
				finally {
					GC.KeepAlive (self);
				}
			}

#pragma warning disable CA1801
			static unsafe bool TryInvokeObjectStaticRedirect (JniMethodInfo method, IJavaPeerable self, JniArgumentValue* parameters, out JniObjectReference r)
			{
				r = default;
#if !NET
				return false;
#else  // NET
				if (method.StaticRedirect == null || !method.ParameterCount.HasValue) {
					return false;
				}

				int c   = method.ParameterCount.Value;
				Debug.Assert (c > 0);
				var p   = stackalloc JniArgumentValue [c];
				p [0]   = new JniArgumentValue (self);
				for (int i = 0; i < c-1; ++i) {
					p [i+1] = parameters [i];
				}

				r = JniEnvironment.StaticMethods.CallStaticObjectMethod (method.StaticRedirect.PeerReference, method, p);
				return true;
#endif   // NET
			}
#pragma warning restore CA1801

			public unsafe JniObjectReference InvokeAbstractObjectMethod (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				try {
					var m   = GetMethodInfo (encodedMember);
					if (TryInvokeObjectStaticRedirect (m, self, parameters, out JniObjectReference r)) {
						return r;
					}

					r = JniEnvironment.InstanceMethods.CallObjectMethod (self.PeerReference, m, parameters);
					return r;
				}
				finally {
					GC.KeepAlive (self);
				}
			}

			public unsafe JniObjectReference InvokeVirtualObjectMethod (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				try {
					var declaringType   = DeclaringType;
					if (Members.UsesVirtualDispatch (self, declaringType)) {
						var m   = GetMethodInfo (encodedMember);
						if (TryInvokeObjectStaticRedirect (m, self, parameters, out JniObjectReference r)) {
							return r;
						}
						r = JniEnvironment.InstanceMethods.CallObjectMethod (self.PeerReference, m, parameters);
						return r;
					}
					var j = Members.GetPeerMembers (self);
					var n = j.InstanceMethods.GetMethodInfo (encodedMember);
					do {
						if (TryInvokeObjectStaticRedirect (n, self, parameters, out JniObjectReference r)) {
							return r;
						}
						r = JniEnvironment.InstanceMethods.CallNonvirtualObjectMethod (self.PeerReference, j.JniPeerType.PeerReference, n, parameters);
						return r;
					} while (false);
				}
				finally {
					GC.KeepAlive (self);
				}
			}

			public unsafe JniObjectReference InvokeNonvirtualObjectMethod (string encodedMember, IJavaPeerable self, JniArgumentValue* parameters)
			{
				JniPeerMembers.AssertSelf (self);

				var m   = GetMethodInfo (encodedMember);
				try {
					if (TryInvokeObjectStaticRedirect (m, self, parameters, out JniObjectReference r)) {
						return r;
					}
					r = JniEnvironment.InstanceMethods.CallNonvirtualObjectMethod (self.PeerReference, JniPeerType.PeerReference, m, parameters);
					return r;
				}
				finally {
					GC.KeepAlive (self);
				}
			}
		}
	}
}

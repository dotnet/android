using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

using Java.Interop;

using Android.Runtime;

namespace Java.Lang
{
	public partial class Throwable : System.Exception, IJavaPeerable
	{
		public virtual JniPeerMembers JniPeerMembers {
			get { return null; }
		}

		public int JniIdentityHashCode {
			get { return 0; }
		}

		public JniObjectReference PeerReference {
			get { return default (JniObjectReference); }
		}

		public JniManagedPeerStates JniManagedPeerState {
			get { return 0; }
		}

		public void DisposeUnlessReferenced ()
		{
		}

		public void UnregisterFromRuntime ()
		{
		}

		public void Disposed ()
		{
		}

		public void Finalized ()
		{
		}

		public void SetJniIdentityHashCode (int value)
		{
		}

		public void SetJniManagedPeerState (JniManagedPeerStates value)
		{
		}

		public void SetPeerReference (JniObjectReference value)
		{
		}
	}
}

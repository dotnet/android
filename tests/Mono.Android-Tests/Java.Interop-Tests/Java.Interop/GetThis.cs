using System;

using Java.Interop;

namespace Java.InteropTests
{
	[JniTypeSignature (GetThis.JniTypeName, GenerateJavaPeer=false)]
	public class GetThis : JavaObject
	{
		internal const string JniTypeName = "net/dot/jni/test/GetThis";

		bool _isDisposed;

		readonly static JniPeerMembers _members = new JniPeerMembers (JniTypeName, typeof (GetThis));

		public override JniPeerMembers JniPeerMembers {
			get {return _members;}
		}

		public GetThis ()
		{
		}

		public GetThis This {
			get {
				// SAFETY: getThis takes no arguments; this owns the receiver peer.
				var o   = unsafe (_members.InstanceMethods.InvokeNonvirtualObjectMethod ("getThis.()Lnet/dot/jni/test/GetThis;", this, null));
				return JniEnvironment.Runtime.ValueManager.GetValue<GetThis> (ref o, JniObjectReferenceOptions.CopyAndDispose);
			}
		}

		protected override void Dispose (bool disposing)
		{
			if (_isDisposed) {
				return;
			}
			_isDisposed = true;
			if (disposing) {
				var t = This;
				if (t != this) {
					throw new InvalidOperationException ("SHOULD NOT BE REACHED");
				}
			}
			base.Dispose (disposing);
		}
	}
}


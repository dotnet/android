using System;

namespace Android.Runtime {

	public class RaiseThrowableEventArgs : EventArgs {

		public RaiseThrowableEventArgs (Exception e)
		{
			Exception = e;
		}

		public Exception Exception {get; private set;}

		bool handled;
		public bool Handled {
			get {return handled;}
			set {
				if (value)
					handled = value;
			}
		}
	}
}


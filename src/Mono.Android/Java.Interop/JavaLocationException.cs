using System;

namespace Java.Interop {

	internal class JavaLocationException : Exception {

		string stackTrace;

		public JavaLocationException (string stackTrace)
		{
			this.stackTrace = stackTrace;
		}

		public override string StackTrace {
			get {
				return stackTrace;
			}
		}
	}
}


using System;

using Android.Runtime;

namespace Javax.Microedition.Khronos.Egl {

	partial class EGLContext {

		public static IEGL11 EGL11 {
			get {
				return EGL.JavaCast<IEGL11> ();
			}
		}
	}
}


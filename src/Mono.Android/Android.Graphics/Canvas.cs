using System;
using Android.Runtime;

namespace Android.Graphics {

	partial class Canvas {

#if ANDROID_11
		[Obsolete ("This method does not exist in API-11+.", error:true)]
		public Canvas (Javax.Microedition.Khronos.Opengles.IGL gl)
			: base (IntPtr.Zero, JniHandleOwnership.DoNotTransfer)
		{
			throw new NotSupportedException ("The Canvas(Javax.Microedition.Khronos.Opengles.IGL) constructor is not supported on API-11+.");
		}
#endif
	}
}

using System;
using System.Runtime.InteropServices;

namespace Java.Interop
{
	public abstract class JniMethodInfo
	{
		public      IntPtr  ID      {get; private set;}

		internal    bool    IsValid {
			get {return ID != IntPtr.Zero;}
		}

		internal JniMethodInfo (IntPtr methodID)
		{
			ID  = methodID;
		}

		public override string ToString ()
		{
			return string.Format ("{0}(0x{1})", GetType ().FullName, ID.ToString ("x"));
		}
	}
}


using System;

namespace Java.Interop
{
	public abstract class JniFieldInfo
	{
		public      IntPtr      ID      {get; private set;}

		internal    bool        IsValid {
			get {return ID != IntPtr.Zero;}
		}

		internal JniFieldInfo (IntPtr fieldID)
		{
			ID  = fieldID;
		}

		public override string ToString ()
		{
			return string.Format ("{0}(0x{1})", GetType ().FullName, ID.ToString ("x"));
		}
	}
}


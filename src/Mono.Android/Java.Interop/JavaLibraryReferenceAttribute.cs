using System;

namespace Java.Interop {

	[AttributeUsage (AttributeTargets.Assembly, AllowMultiple = true)]
	[Obsolete ("This attribute is no longer supported.", error: true)]
	public class JavaLibraryReferenceAttribute : Android.ReferenceFilesAttribute
	{
#pragma warning disable RS0022 // Constructor make noninheritable base class inheritable
		public JavaLibraryReferenceAttribute (string filename)
#pragma warning restore RS0022 // Constructor make noninheritable base class inheritable
		{
			LibraryFileName = filename;
		}

		public string   LibraryFileName {get; private set;}
	}
}

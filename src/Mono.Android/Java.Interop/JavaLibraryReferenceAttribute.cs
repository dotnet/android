using System;

namespace Java.Interop {

	[AttributeUsage (AttributeTargets.Assembly, AllowMultiple = true)]
	[Obsolete ("This attribute is not longer supported.", error: true)]
	public class JavaLibraryReferenceAttribute : Android.ReferenceFilesAttribute
	{
		public JavaLibraryReferenceAttribute (string filename)
		{
			LibraryFileName = filename;
		}

		public string   LibraryFileName {get; private set;}
	}
}

using System;

namespace Java.Interop {

	[AttributeUsage (AttributeTargets.Assembly, AllowMultiple = true)]
	public class JavaLibraryReferenceAttribute : Android.ReferenceFilesAttribute
	{
		public JavaLibraryReferenceAttribute (string filename)
		{
			LibraryFileName = filename;
		}

		public string   LibraryFileName {get; private set;}
	}
}

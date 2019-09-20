using System;

namespace Java.Interop {

	[AttributeUsage (AttributeTargets.Assembly, AllowMultiple = true)]
	[Obsolete ("This attribute is deprecated and will be removed in a future release.")]
	public class JavaLibraryReferenceAttribute : Android.ReferenceFilesAttribute
	{
		public JavaLibraryReferenceAttribute (string filename)
		{
			LibraryFileName = filename;
		}

		public string   LibraryFileName {get; private set;}
	}
}

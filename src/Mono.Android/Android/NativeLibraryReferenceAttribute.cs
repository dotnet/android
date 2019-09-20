using System;

namespace Android {

	[AttributeUsage (AttributeTargets.Assembly, AllowMultiple = true)]
	[Obsolete ("This attribute is deprecated and will be removed in a future release.")]
	sealed public class NativeLibraryReferenceAttribute : ReferenceFilesAttribute
	{
		public NativeLibraryReferenceAttribute (string filename)
		{
			LibraryFileName = filename;
		}

		public string   LibraryFileName {get; private set;}
	}
}

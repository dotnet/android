using System;

namespace Android {

	[AttributeUsage (AttributeTargets.Assembly, AllowMultiple = true)]
	[Obsolete ("This attribute is no longer supported.", error: true)]
	public class IncludeAndroidResourcesFromAttribute : ReferenceFilesAttribute
	{
#pragma warning disable RS0022 // Constructor make noninheritable base class inheritable
		public IncludeAndroidResourcesFromAttribute (string path)
#pragma warning restore RS0022 // Constructor make noninheritable base class inheritable
		{
			ResourceDirectory = path;
		}

		public string   ResourceDirectory {get; private set;}
	}
}

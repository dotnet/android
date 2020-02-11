using System;

namespace Java.Interop
{
	[Serializable]
	[AttributeUsage (AttributeTargets.Assembly,
			AllowMultiple=true,
			Inherited=false)]
	[Obsolete ("This attribute is deprecated and will be removed in a future release. Use the @(AndroidExternalJavaLibrary) MSBuild item group instead.")]
	public class DoNotPackageAttribute : Attribute
	{
		public DoNotPackageAttribute (string jarFile)
		{
			JarFile = jarFile;
		}
		
		public string JarFile { get; set; }
	}
}


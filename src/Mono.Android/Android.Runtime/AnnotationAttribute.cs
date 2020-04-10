using System;

namespace Android.Runtime
{
	public class AnnotationAttribute : Attribute
	{
		public AnnotationAttribute (string javaName)
		{
			JavaName = javaName;
		}

		public string JavaName { get; private set; }
	}
}


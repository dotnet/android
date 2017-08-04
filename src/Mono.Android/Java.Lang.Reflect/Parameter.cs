#if ANDROID_26
using System;

namespace Java.Lang.Reflect
{
	public partial class Parameter
	{
		// This is a workaround addition for missing default interface method support in C#.
		// (And yes, this is the only class that didn't exist before 26)
		// FIXME: remove this once we got DIM support in Roslyn/mcs.
		public bool IsAnnotationPresent (Java.Lang.Class annotationClass)
		{
			// http://tools.oesf.biz/android-7.1.1_r1.0/xref/libcore/ojluni/src/main/java/java/lang/reflect/AnnotatedElement.java
			return GetAnnotation (annotationClass) != null;
		}
	}
}
#endif

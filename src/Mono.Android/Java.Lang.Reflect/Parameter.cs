#if ANDROID_26
using System;

namespace Java.Lang.Reflect
{
	public partial class Parameter
	{
		// Keep this class member for source compatibility. Default interface methods
		// are only callable through an IAnnotatedElement reference.
		public bool IsAnnotationPresent (Java.Lang.Class annotationClass)
		{
			// http://tools.oesf.biz/android-7.1.1_r1.0/xref/libcore/ojluni/src/main/java/java/lang/reflect/AnnotatedElement.java
			return GetAnnotation (annotationClass) != null;
		}
	}
}
#endif

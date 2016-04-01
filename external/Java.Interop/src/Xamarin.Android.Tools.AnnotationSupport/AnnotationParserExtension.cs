using System;
using System.Collections.Generic;

namespace Xamarin.AndroidTools.AnnotationSupport
{
	public abstract class AnnotationParserExtension
	{
		protected internal abstract void OnAnnotationsParsed (IEnumerable<AnnotatedItem> anns);
	}
}


using System;
using System.Collections.Generic;
using System.Linq;

namespace Xamarin.AndroidTools.AnnotationSupport
{
	public class AnnotationObject
	{
		List<object> extensions = new List<object> ();

		public T GetExtension<T> ()
		{
			foreach (var e in extensions)
				if (e is T)
					return (T) e;
			return default (T);
		}

		public void SetExtension<T> (T obj)
		{
			if (extensions.Any (o => o is T))
				throw new InvalidOperationException ("There is already extension of type " + typeof (T));
			extensions.Add ((object) obj);
		}
	}
}


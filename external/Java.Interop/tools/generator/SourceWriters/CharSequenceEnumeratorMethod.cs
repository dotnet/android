using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.SourceWriter;

namespace generator.SourceWriters
{
	public class CharSequenceEnumeratorMethod : MethodWriter
	{
		// System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator ()
		// {
		// 	return GetEnumerator ();
		// }
		public CharSequenceEnumeratorMethod ()
		{
			Name = "System.Collections.IEnumerable.GetEnumerator";
			ReturnType = new TypeReferenceWriter ("System.Collections.IEnumerator");

			Body.Add ("return GetEnumerator ();");
		}		
	}

	public class CharSequenceGenericEnumeratorMethod : MethodWriter
	{
		// public System.Collections.Generic.IEnumerator<char> GetEnumerator ()
		// {
		// 	for (int i = 0; i < Length(); i++)
		// 		yield return CharAt (i);
		// }
		public CharSequenceGenericEnumeratorMethod ()
		{
			Name = "GetEnumerator";
			ReturnType = new TypeReferenceWriter ("System.Collections.Generic.IEnumerator<char>");

			IsPublic = true;

			Body.Add ("for (int i = 0; i < Length (); i++)");
			Body.Add ("\tyield return CharAt (i);");
		}
	}
}

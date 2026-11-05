using System;
using System.Collections.Generic;
using System.Text;

namespace Xamarin.SourceWriter
{
	public class TypeReferenceWriter
	{
		public string Namespace { get; set; }
		public string Name { get; set; }
		public bool Nullable { get; set; }
		
		// These purposely create new instances, as they are not immutable.
		// For example you may intend to make an instance null, but if there
		// was only one, you would make them all null.
		public static TypeReferenceWriter Bool => new TypeReferenceWriter ("bool");
		public static TypeReferenceWriter Delegate => new TypeReferenceWriter ("Delegate");
		public static TypeReferenceWriter IntPtr => new TypeReferenceWriter ("IntPtr");
		public static TypeReferenceWriter Float => new TypeReferenceWriter ("float");
		public static TypeReferenceWriter Object => new TypeReferenceWriter ("object");
		public static TypeReferenceWriter Void => new TypeReferenceWriter ("void");

		public TypeReferenceWriter (string name)
		{
			var index = name.LastIndexOf ('.');

			if (index >= 0) {
				Namespace = name.Substring (0, index);
				Name = name.Substring (index + 1);
			} else {
				Name = name;
			}
		}

		public TypeReferenceWriter (string ns, string name)
		{
			Namespace = ns;
			Name = name;
		}

		public virtual void WriteTypeReference (CodeWriter writer)
		{
			if (Namespace.HasValue ())
				writer.Write ($"{Namespace}.{Name}{NullableOperator} ");
			else
				writer.Write ($"{Name}{NullableOperator} ");
		}

		string NullableOperator => Nullable ? "?" : string.Empty;
	}
}

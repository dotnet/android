using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Xamarin.Android.Tools.Bytecode
{
	public sealed class Annotation
	{
		public ConstantPool ConstantPool { get; }

		ushort typeIndex;
		public string Type => ((ConstantPoolUtf8Item) ConstantPool [typeIndex]).Value;

		public IList<KeyValuePair<string, AnnotationElementValue?>>
			Values { get; } = new List<KeyValuePair<string, AnnotationElementValue?>> ();

		public Annotation (ConstantPool constantPool, Stream stream)
		{
			ConstantPool = constantPool;
			typeIndex = stream.ReadNetworkUInt16 ();

			var count = stream.ReadNetworkUInt16 ();
			for (int i = 0; i < count; ++i) {
				var elementNameIndex = stream.ReadNetworkUInt16 ();
				var elementName = ((ConstantPoolUtf8Item) ConstantPool [elementNameIndex]).Value;
				var elementValue = AnnotationElementValue.Create (constantPool, stream);
				Values.Add (new KeyValuePair<string, AnnotationElementValue?> (elementName, elementValue));
			}
		}

		public override string ToString ()
		{
			var values = new StringBuilder ("{");
			if (Values.Count > 0) {
				Append (Values [0]);
			}
			for (int i = 1; i < Values.Count; ++i) {
				values.Append (", ");
				Append (Values [i]);
			}
			values.Append ("}");
			return $"Annotation('{Type}', {values})";

			void Append (KeyValuePair<string, AnnotationElementValue?> value)
			{
				values.Append (value.Key).Append (": ");
				values.Append (value.Value);
			}
		}
	}

	public sealed class ParameterAnnotation
	{
		public int ParameterIndex { get; }
		public IList<Annotation> Annotations { get; } = new List<Annotation> ();
		public ConstantPool ConstantPool { get; }

		public ParameterAnnotation (ConstantPool constantPool, Stream stream, int index)
		{
			ConstantPool = constantPool;

			ParameterIndex = index;

			var ann_count = stream.ReadNetworkUInt16 ();

			for (var i = 0; i < ann_count; ++i) {
				var a = new Annotation (constantPool, stream);
				Annotations.Add (a);
			}
		}

		public override string ToString ()
		{
			var annotations = string.Join (", ", Annotations.Select (v => v.ToString ()));
			return $"Parameter{ParameterIndex}({annotations})";
		}
	}
}

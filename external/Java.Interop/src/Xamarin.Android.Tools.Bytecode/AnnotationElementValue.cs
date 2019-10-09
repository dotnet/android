using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Xamarin.Android.Tools.Bytecode
{
	// https://docs.oracle.com/javase/specs/jvms/se7/html/jvms-4.html#jvms-4.7.16.1
	public abstract class AnnotationElementValue
	{
		public virtual string ToEncodedString () => ToString ();

		public static AnnotationElementValue Create (ConstantPool constantPool, Stream stream)
		{
			var tag = stream.ReadNetworkByte ();

			switch (tag) {
				case (byte) 'e': {
						var typeNameIndex = stream.ReadNetworkUInt16 ();
						var constNameIndex = stream.ReadNetworkUInt16 ();

						var typeName = ((ConstantPoolUtf8Item) constantPool [typeNameIndex]).Value;
						var constName = ((ConstantPoolUtf8Item) constantPool [constNameIndex]).Value;

						return new AnnotationElementEnum { TypeName = typeName, ConstantName = constName };
					}
				case (byte) 'c': {
						var classInfoIndex = stream.ReadNetworkUInt16 ();

						return new AnnotationElementClassInfo { ClassInfo = ((ConstantPoolUtf8Item) constantPool [classInfoIndex]).Value };
					}
				case (byte) '@': {
						return new AnnotationElementAnnotation { Annotation = new Annotation (constantPool, stream) };
					}
				case (byte) '[': {
						var numValues = stream.ReadNetworkUInt16 ();

						var values = new List<AnnotationElementValue> ();

						for (var i = 0; i < numValues; i++)
							values.Add (Create (constantPool, stream));

						return new AnnotationElementArray { Values = values.ToArray () };
					}
				case (byte) 'B':
				case (byte) 'C':
				case (byte) 'D':
				case (byte) 'F':
				case (byte) 'I':
				case (byte) 'J':
				case (byte) 'S':
				case (byte) 's':
				case (byte) 'Z': {
						var constValueIndex = stream.ReadNetworkUInt16 ();
						var poolItem = constantPool [constValueIndex];
						var value = poolItem.ToString ();

						if (poolItem is ConstantPoolDoubleItem d)
							value = d.Value.ToString ();
						else if (poolItem is ConstantPoolFloatItem f)
							value = f.Value.ToString ();
						else if (poolItem is ConstantPoolIntegerItem i)
							value = i.Value.ToString ();
						else if (poolItem is ConstantPoolLongItem l)
							value = l.Value.ToString ();
						else if (poolItem is ConstantPoolStringItem s)
							return new AnnotationStringElementConstant { Value = s.StringData.Value.ToString () };
						else if (poolItem is ConstantPoolUtf8Item u)
							return new AnnotationStringElementConstant { Value = u.Value.ToString () };

						return new AnnotationElementConstant { Value = value };
					}
			}

			return null;
		}
	}

	public class AnnotationElementEnum : AnnotationElementValue
	{
		public string TypeName { get; set; }
		public string ConstantName { get; set; }

		public override string ToString () => $"Enum({TypeName}.{ConstantName})";
	}

	public class AnnotationElementClassInfo : AnnotationElementValue
	{
		public string ClassInfo { get; set; }

		public override string ToString () => ClassInfo;
	}

	public class AnnotationElementAnnotation : AnnotationElementValue
	{
		public Annotation Annotation { get; set; }

		public override string ToString () => Annotation.ToString ();
	}

	public class AnnotationElementArray : AnnotationElementValue
	{
		public AnnotationElementValue[] Values { get; set; }

		public override string ToString () => $"[{string.Join (", ", Values.Select (v => v.ToString ()))}]";

		public override string ToEncodedString () => $"[{string.Join (", ", Values.Select (v => v.ToEncodedString ()))}]";
	}

	public class AnnotationElementConstant : AnnotationElementValue
	{
		public string Value { get; set; }

		public override string ToString () => Value;
	}

	public class AnnotationStringElementConstant : AnnotationElementConstant
	{
		public override string ToString () => $"\"{Value}\"";

		public override string ToEncodedString ()
		{
			return $"\"{Convert.ToBase64String (Encoding.UTF8.GetBytes (Value))}\"";
		}
	}
}

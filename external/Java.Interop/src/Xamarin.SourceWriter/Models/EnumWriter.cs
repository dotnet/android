using System;
using System.Collections.Generic;

namespace Xamarin.SourceWriter
{
	public class EnumWriter : ISourceWriter
	{
		Visibility visibility;

		public string Name { get; set; }
		public bool IsPublic { get => visibility.HasFlag (Visibility.Public); set => visibility = value ? Visibility.Public : Visibility.Default; }
		public bool IsInternal { get => visibility.HasFlag (Visibility.Internal); set => visibility = value ? Visibility.Internal : Visibility.Default; }
		public bool IsPrivate { get => visibility.HasFlag (Visibility.Private); set => visibility = value ? Visibility.Private : Visibility.Default; }
		public bool IsProtected { get => visibility.HasFlag (Visibility.Protected); set => visibility = value ? Visibility.Protected : Visibility.Default; }
		public List<string> Comments { get; } = new List<string> ();
		public List<AttributeWriter> Attributes { get; } = new List<AttributeWriter> ();
		public List<EnumMemberWriter> Members { get; } = new List<EnumMemberWriter> ();

		public int Priority { get; set; }

		public void Write (CodeWriter writer)
		{
			WriteComments (writer);
			WriteAttributes (writer);
			WriteSignature (writer);
			WriteMembers (writer);
			WriteTypeClose (writer);
		}

		public virtual void WriteComments (CodeWriter writer)
		{
			foreach (var c in Comments)
				writer.WriteLine (c);
		}

		public virtual void WriteAttributes (CodeWriter writer)
		{
			foreach (var att in Attributes)
				att.WriteAttribute (writer);
		}

		public virtual void WriteSignature (CodeWriter writer)
		{
			if (IsPublic)
				writer.Write ("public ");
			if (IsProtected)
				writer.Write ("protected ");
			if (IsInternal)
				writer.Write ("internal ");
			if (IsPrivate)
				writer.Write ("private ");

			writer.Write ("enum ");
			writer.WriteLine (Name + " ");

			writer.WriteLine ("{");
			writer.Indent ();
		}

		public virtual void WriteMembers (CodeWriter writer)
		{
			foreach (var member in Members) {
				member.Write (writer);
				writer.WriteLine ();
			}
		}

		public virtual void WriteTypeClose (CodeWriter writer)
		{
			writer.Unindent ();
			writer.WriteLine ("}");
		}
	}
}

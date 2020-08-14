using System;
using System.Collections.Generic;
using System.Text;

namespace Xamarin.SourceWriter
{
	public class EventWriter : ISourceWriter
	{
		Visibility visibility;

		public string Name { get; set; }
		public TypeReferenceWriter EventType { get; set; }
		public List<string> Comments { get; } = new List<string> ();
		public List<AttributeWriter> Attributes { get; } = new List<AttributeWriter> ();
		public bool IsPublic { get => visibility.HasFlag (Visibility.Public); set => visibility |= Visibility.Public; }
		public bool UseExplicitPrivateKeyword { get; set; }
		public bool IsInternal { get => visibility.HasFlag (Visibility.Internal); set => visibility |= Visibility.Internal; }
		public List<string> AddBody { get; } = new List<string> ();
		public List<string> RemoveBody { get; } = new List<string> ();
		public bool IsStatic { get; set; }
		public bool IsProtected { get => visibility.HasFlag (Visibility.Protected); set => visibility |= Visibility.Protected; }
		public bool IsPrivate { get => visibility == Visibility.Private; set => visibility = value ? Visibility.Private : Visibility.Default; }
		public bool IsOverride { get; set; }
		public bool HasAdd { get; set; }
		public bool HasRemove { get; set; }
		public bool IsShadow { get; set; }
		public bool IsAutoProperty { get; set; }
		public bool IsAbstract { get; set; }
		public bool IsVirtual { get; set; }
		public bool IsUnsafe { get; set; }
		public List<string> GetterComments { get; } = new List<string> ();
		public List<string> SetterComments { get; } = new List<string> ();
		public List<AttributeWriter> GetterAttributes { get; } = new List<AttributeWriter> ();
		public List<AttributeWriter> SetterAttributes { get; } = new List<AttributeWriter> ();
		public int Priority { get; set; }

		public void SetVisibility (string visibility)
		{
			switch (visibility?.ToLowerInvariant ()) {
				case "public":
					IsPublic = true;
					break;
				case "internal":
					IsInternal = true;
					break;
				case "protected":
					IsProtected = true;
					break;
				case "private":
					IsPrivate = true;
					break;
			}
		}

		public virtual void Write (CodeWriter writer)
		{
			WriteComments (writer);
			WriteAttributes (writer);
			WriteSignature (writer);
		}

		public virtual void WriteComments (CodeWriter writer)
		{
			foreach (var c in Comments)
				writer.WriteLine (c);
		}

		public virtual void WriteAddComments (CodeWriter writer)
		{
			foreach (var c in GetterComments)
				writer.WriteLine (c);
		}

		public virtual void WriteRemoveComments (CodeWriter writer)
		{
			foreach (var c in SetterComments)
				writer.WriteLine (c);
		}

		public virtual void WriteAttributes (CodeWriter writer)
		{
			foreach (var att in Attributes)
				att.WriteAttribute (writer);
		}

		public virtual void WriteAddAttributes (CodeWriter writer)
		{
			foreach (var att in GetterAttributes)
				att.WriteAttribute (writer);
		}

		public virtual void WriteRemoveAttributes (CodeWriter writer)
		{
			foreach (var att in SetterAttributes)
				att.WriteAttribute (writer);
		}

		public virtual void WriteSignature (CodeWriter writer)
		{
			if (IsPublic)
				writer.Write ("public ");
			if (IsInternal)
				writer.Write ("internal ");
			if (IsProtected)
				writer.Write ("protected ");
			if (visibility == Visibility.Private && UseExplicitPrivateKeyword)
				writer.Write ("private ");

			if (IsOverride)
				writer.Write ("override ");

			if (IsStatic)
				writer.Write ("static ");

			if (IsShadow)
				writer.Write ("new ");

			if (IsAbstract)
				writer.Write ("abstract ");
			if (IsVirtual)
				writer.Write ("virtual ");

			if (IsUnsafe)
				writer.Write ("unsafe ");

			writer.Write ("event ");

			WriteEventType (writer);
			writer.Write (Name);

			WriteBody (writer);
		}

		protected virtual void WriteBody (CodeWriter writer)
		{
			if (!HasAdd && !HasRemove) {
				writer.WriteLine (";");
				return;
			}

			writer.Write (" ");

			if (IsAutoProperty || IsAbstract) {
				WriteAutomaticEventBody (writer);
				return;
			}

			writer.WriteLine ("{");

			writer.Indent ();

			WriteAdd (writer);
			WriteRemove (writer);

			writer.Unindent ();

			writer.WriteLine ("}");
		}

		protected virtual void WriteAutomaticEventBody (CodeWriter writer)
		{
			writer.Write ("{ ");

			if (HasAdd) {
				WriteAddComments (writer);
				WriteAddAttributes (writer);
				writer.Write ("add; ");
			}

			if (HasRemove) {
				WriteRemoveComments (writer);
				WriteRemoveAttributes (writer);
				writer.Write ("remove; ");
			}

			writer.WriteLine ("}");
		}

		protected virtual void WriteAdd (CodeWriter writer)
		{
			if (HasAdd) {
				WriteAddComments (writer);
				WriteAddAttributes (writer);

				if (AddBody.Count == 1)
					writer.WriteLine ("add { " + AddBody [0] + " }");
				else {
					writer.WriteLine ("add {");
					writer.Indent ();

					WriteAddBody (writer);

					writer.Unindent ();
					writer.WriteLine ("}");
				}
			}
		}

		protected virtual void WriteAddBody (CodeWriter writer)
		{
			foreach (var b in AddBody)
				writer.WriteLine (b);
		}

		protected virtual void WriteRemove (CodeWriter writer)
		{
			if (HasRemove) {
				WriteRemoveComments (writer);
				WriteRemoveAttributes (writer);

				if (RemoveBody.Count == 1)
					writer.WriteLine ("remove { " + RemoveBody [0] + " }");
				else {
					writer.WriteLine ("remove {");
					writer.Indent ();

					WriteRemoveBody (writer);

					writer.Unindent ();
					writer.WriteLine ("}");
				}
			}
		}

		protected virtual void WriteEventType (CodeWriter writer)
		{
			EventType.WriteTypeReference (writer);
		}

		protected virtual void WriteRemoveBody (CodeWriter writer)
		{
			foreach (var b in RemoveBody)
				writer.WriteLine (b);
		}
	}
}

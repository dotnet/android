using System;
using System.Collections.Generic;
using System.Text;

namespace Xamarin.SourceWriter
{
	public class PropertyWriter : ISourceWriter
	{
		Visibility visibility;

		public string Name { get; set; }
		public List<MethodParameterWriter> Parameters { get; } = new List<MethodParameterWriter> ();
		public TypeReferenceWriter PropertyType { get; set; }
		public List<string> Comments { get; } = new List<string> ();
		public List<AttributeWriter> Attributes { get; } = new List<AttributeWriter> ();
		public bool IsPublic { get => visibility.HasFlag (Visibility.Public); set => visibility |= Visibility.Public; }
		public bool UseExplicitPrivateKeyword { get; set; }
		public bool IsInternal { get => visibility.HasFlag (Visibility.Internal); set => visibility |= Visibility.Internal; }
		public List<string> GetBody { get; } = new List<string> ();
		public List<string> SetBody { get; } = new List<string> ();
		public bool IsStatic { get; set; }
		public bool IsProtected { get => visibility.HasFlag (Visibility.Protected); set => visibility |= Visibility.Protected; }
		public bool IsPrivate { get => visibility == Visibility.Private; set => visibility = value ? Visibility.Private : Visibility.Default; }
		public bool IsOverride { get; set; }
		public bool HasGet { get; set; }
		public bool HasSet { get; set; }
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
		public string ExplicitInterfaceImplementation { get; set; }

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

		public virtual void WriteGetterComments (CodeWriter writer)
		{
			foreach (var c in GetterComments)
				writer.WriteLine (c);
		}

		public virtual void WriteSetterComments (CodeWriter writer)
		{
			foreach (var c in SetterComments)
				writer.WriteLine (c);
		}

		public virtual void WriteAttributes (CodeWriter writer)
		{
			foreach (var att in Attributes)
				att.WriteAttribute (writer);
		}

		public virtual void WriteGetterAttributes (CodeWriter writer)
		{
			foreach (var att in GetterAttributes)
				att.WriteAttribute (writer);
		}

		public virtual void WriteSetterAttributes (CodeWriter writer)
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

			WritePropertyType (writer);

			if (ExplicitInterfaceImplementation.HasValue ())
				writer.Write (ExplicitInterfaceImplementation + ".");

			writer.Write (Name + " ");

			WriteBody (writer);
		}

		protected virtual void WriteBody (CodeWriter writer)
		{
			if (IsAutoProperty || IsAbstract) {
				WriteAutomaticPropertyBody (writer);
				return;
			}

			writer.WriteLine ("{");

			writer.Indent ();

			WriteGetter (writer);
			WriteSetter (writer);

			writer.Unindent ();

			writer.WriteLine ("}");
		}

		protected virtual void WriteAutomaticPropertyBody (CodeWriter writer)
		{
			writer.Write ("{ ");

			if (HasGet) {
				WriteGetterComments (writer);
				WriteGetterAttributes (writer);
				writer.Write ("get; ");
			}

			if (HasSet) {
				WriteSetterComments (writer);
				WriteSetterAttributes (writer);
				writer.Write ("set; ");
			}

			writer.WriteLine ("}");
		}

		protected virtual void WriteGetter (CodeWriter writer)
		{
			if (HasGet) {
				WriteGetterComments (writer);
				WriteGetterAttributes (writer);

				if (GetBody.Count == 1)
					writer.WriteLine ("get { " + GetBody [0] + " }");
				else {
					writer.WriteLine ("get {");
					writer.Indent ();

					WriteGetterBody (writer);

					writer.Unindent ();
					writer.WriteLine ("}");
				}
			}
		}

		protected virtual void WriteGetterBody (CodeWriter writer)
		{
			foreach (var b in GetBody)
				writer.WriteLine (b);
		}

		protected virtual void WriteSetter (CodeWriter writer)
		{
			if (HasSet) {
				WriteSetterComments (writer);
				WriteSetterAttributes (writer);

				if (SetBody.Count == 1)
					writer.WriteLine ("set { " + SetBody [0] + " }");
				else {
					writer.WriteLine ("set {");
					writer.Indent ();

					WriteSetterBody (writer);

					writer.Unindent ();
					writer.WriteLine ("}");
				}
			}
		}

		protected virtual void WriteSetterBody (CodeWriter writer)
		{
			foreach (var b in SetBody)
				writer.WriteLine (b);
		}

		protected virtual void WritePropertyType (CodeWriter writer)
		{
			PropertyType.WriteTypeReference (writer);
		}

		protected virtual void WriteConstructorBaseCall (CodeWriter writer) { }
	}
}

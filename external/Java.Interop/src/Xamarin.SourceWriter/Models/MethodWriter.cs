using System;
using System.Collections.Generic;
using System.Text;

namespace Xamarin.SourceWriter
{
	public class MethodWriter : ISourceWriter, ITakeParameters
	{
		Visibility visibility;

		public string Name { get; set; }
		public List<MethodParameterWriter> Parameters { get; } = new List<MethodParameterWriter> ();
		public TypeReferenceWriter ReturnType { get; set; }
		public List<string> Comments { get; } = new List<string> ();
		public List<AttributeWriter> Attributes { get; } = new List<AttributeWriter> ();
		public bool IsPublic { get => visibility == Visibility.Public; set => visibility = value ? Visibility.Public : Visibility.Default; }
		public bool UseExplicitPrivateKeyword { get; set; }
		public bool IsInternal { get => visibility == Visibility.Internal; set => visibility = value ? Visibility.Internal : Visibility.Default; }
		public List<string> Body { get; set; } = new List<string> ();
		public bool IsSealed { get; set; }
		public bool IsStatic { get; set; }
		public bool IsPrivate { get => visibility == Visibility.Private; set => visibility = value ? Visibility.Private : Visibility.Default; }
		public bool IsProtected { get => visibility == Visibility.Protected; set => visibility = value ? Visibility.Protected : Visibility.Default; }
		public bool IsOverride { get; set; }
		public bool IsUnsafe { get; set; }
		public bool IsVirtual { get; set; }
		public bool IsShadow { get; set; }
		public bool IsAbstract { get; set; }
		public int Priority { get; set; }
		public bool IsDeclaration { get; set; }

		public string ExplicitInterfaceImplementation { get; set; }
		public bool NewFirst { get; set; }           // TODO: Temporary to match unit tests

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

		public virtual void WriteAttributes (CodeWriter writer)
		{
			foreach (var att in Attributes)
				att.WriteAttribute (writer);
		}

		public virtual void WriteSignature (CodeWriter writer)
		{
			if (IsShadow && NewFirst)
				writer.Write ("new ");

			if (IsPublic)
				writer.Write ("public ");
			if (IsInternal)
				writer.Write ("internal ");
			if (IsProtected)
				writer.Write ("protected ");
			if (IsPrivate)
				writer.Write ("private ");

			if (IsShadow && !NewFirst)
				writer.Write ("new ");

			if (IsOverride)
				writer.Write ("override ");
			else if (IsVirtual)
				writer.Write ("virtual ");
			else if (IsAbstract)
				writer.Write ("abstract ");

			if (IsSealed)
				writer.Write ("sealed ");

			if (IsStatic)
				writer.Write ("static ");

			if (IsUnsafe)
				writer.Write ("unsafe ");

			WriteReturnType (writer);

			if (ExplicitInterfaceImplementation.HasValue ())
				writer.Write (ExplicitInterfaceImplementation + ".");

			writer.Write (Name + " ");
			writer.Write ("(");

			WriteParameters (writer);

			writer.Write (")");

			if (IsAbstract || IsDeclaration) {
				writer.WriteLine (";");
				return;
			}

			WriteConstructorBaseCall (writer);

			writer.WriteLine ();
			writer.WriteLine ("{");
			writer.Indent ();

			WriteBody (writer);

			writer.Unindent ();

			writer.WriteLine ("}");
		}

		protected virtual void WriteBody (CodeWriter writer)
		{
			foreach (var s in Body)
				writer.WriteLine (s);
		}

		protected virtual void WriteParameters (CodeWriter writer)
		{
			for (var i = 0; i < Parameters.Count; i++) {
				var p = Parameters [i];
				p.WriteParameter (writer);

				if (i < Parameters.Count - 1)
					writer.Write (", ");
			}
		}

		protected virtual void WriteReturnType (CodeWriter writer)
		{
			ReturnType.WriteTypeReference (writer);
		}

		protected virtual void WriteConstructorBaseCall (CodeWriter writer) { }
	}
}

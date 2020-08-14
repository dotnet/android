using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Xamarin.SourceWriter
{
	public abstract class TypeWriter : ISourceWriter
	{
		Visibility visibility;
		int current_priority = 1;

		public string Name { get; set; }
		public string Inherits { get; set; }
		public List<string> Implements { get; } = new List<string> ();
		public bool IsPartial { get; set; }
		public bool IsPublic { get => visibility.HasFlag (Visibility.Public); set => visibility = value ? Visibility.Public : Visibility.Default; }
		public bool IsAbstract { get; set; }
		public bool IsInternal { get => visibility.HasFlag (Visibility.Internal); set => visibility = value ? Visibility.Internal : Visibility.Default; }
		public bool IsShadow { get; set; }
		public bool IsSealed { get; set; }
		public bool IsStatic { get; set; }
		public bool IsPrivate { get => visibility.HasFlag (Visibility.Private); set => visibility = value ? Visibility.Private : Visibility.Default; }
		public bool IsProtected { get => visibility.HasFlag (Visibility.Protected); set => visibility = value ? Visibility.Protected : Visibility.Default; }
		public ObservableCollection<MethodWriter> Methods { get; } = new ObservableCollection<MethodWriter> ();
		public List<string> Comments { get; } = new List<string> ();
		public List<AttributeWriter> Attributes { get; } = new List<AttributeWriter> ();
		public ObservableCollection<EventWriter> Events { get; } = new ObservableCollection<EventWriter> ();
		public ObservableCollection<FieldWriter> Fields { get; } = new ObservableCollection<FieldWriter> ();
		public ObservableCollection<PropertyWriter> Properties { get; } = new ObservableCollection<PropertyWriter> ();
		public ObservableCollection<CommentWriter> InlineComments { get; } = new ObservableCollection<CommentWriter> ();
		public ObservableCollection<DelegateWriter> Delegates { get; } = new ObservableCollection<DelegateWriter> ();
		public int Priority { get; set; }
		public int GetNextPriority () => current_priority++;
		public bool UsePriorityOrder { get; set; }

		public ObservableCollection<TypeWriter> NestedTypes { get; } = new ObservableCollection<TypeWriter> ();

		protected TypeWriter ()
		{
			Methods.CollectionChanged += MemberAdded;
			Events.CollectionChanged += MemberAdded;
			Fields.CollectionChanged += MemberAdded;
			Properties.CollectionChanged += MemberAdded;
			InlineComments.CollectionChanged += MemberAdded;
			Delegates.CollectionChanged += MemberAdded;
			NestedTypes.CollectionChanged += MemberAdded;
		}

		protected void MemberAdded (object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
		{
			foreach (var member in e.NewItems.OfType<ISourceWriter> ())
				if (member.Priority == 0)
					member.Priority = GetNextPriority ();
		}

		public void SetVisibility (string visibility)
		{
			switch (visibility?.ToLowerInvariant ().Trim ()) {
				case "public":
					IsPublic = true;
					break;
				case "internal":
					IsInternal = true;
					break;
				case "protected":
					IsProtected = true;
					break;
				case "protected internal":
					this.visibility = Visibility.Protected | Visibility.Internal;
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

			if (IsShadow)
				writer.Write ("new ");

			if (IsStatic)
				writer.Write ("static ");

			if (IsAbstract)
				writer.Write ("abstract ");

			if (IsSealed)
				writer.Write ("sealed ");

			if (IsPartial)
				writer.Write ("partial ");

			writer.Write (this is InterfaceWriter ? "interface " : "class ");
			writer.Write (Name + " ");

			if (Inherits.HasValue () || Implements.Count > 0)
				writer.Write (": ");

			if (Inherits.HasValue ()) {
				writer.Write (Inherits);

				if (Implements.Count > 0)
					writer.Write (",");

				writer.Write (" ");
			}

			if (Implements.Count > 0)
				writer.Write (string.Join (", ", Implements) + " ");

			writer.WriteLine ("{");
			writer.Indent ();
		}

		public virtual void WriteMembers (CodeWriter writer)
		{
			if (UsePriorityOrder) {
				WriteMembersByPriority (writer);
				return;
			}

			if (Fields.Count > 0) {
				writer.WriteLine ();
				WriteFields (writer);
			}

			WriteConstructors (writer);

			writer.WriteLine ();
			WriteEvents (writer);
			writer.WriteLine ();
			WriteDelegates (writer);
			writer.WriteLine ();
			WriteProperties (writer);
			writer.WriteLine ();
			WriteMethods (writer);
		}

		public void AddInlineComment (string comment)
		{
			InlineComments.Add (new CommentWriter (comment));
		}

		public virtual void WriteMembersByPriority (CodeWriter writer)
		{
			var members = Fields.Cast<ISourceWriter> ().Concat (Properties).Concat (Methods).Concat (NestedTypes).Concat (Events).Concat (InlineComments).Concat (Delegates);

			if (this is ClassWriter klass)
				members = members.Concat (klass.Constructors);

			foreach (var member in members.OrderBy (p => p.Priority)) {
				member.Write (writer);
				writer.WriteLine ();
			}
		}

		public virtual void WriteConstructors (CodeWriter writer) { }

		public virtual void WriteEvents (CodeWriter writer)
		{
			foreach (var ev in Events) {
				ev.Write (writer);
				writer.WriteLine ();
			}
		}

		public virtual void WriteFields (CodeWriter writer)
		{
			foreach (var field in Fields) {
				field.Write (writer);
				writer.WriteLine ();
			}
		}

		public virtual void WriteMethods (CodeWriter writer)
		{
			foreach (var method in Methods) {
				method.Write (writer);
				writer.WriteLine ();
			}
		}

		public virtual void WriteProperties (CodeWriter writer)
		{
			foreach (var prop in Properties) {
				prop.Write (writer);
				writer.WriteLine ();
			}
		}

		public virtual void WriteDelegates (CodeWriter writer)
		{
			foreach (var del in Delegates) {
				del.Write (writer);
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

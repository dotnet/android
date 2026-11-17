using System.Collections.ObjectModel;

namespace Xamarin.SourceWriter;

public class ClassWriter : TypeWriter
{
	public ObservableCollection<ConstructorWriter> Constructors { get; } = new ObservableCollection<ConstructorWriter> ();

	public ClassWriter ()
	{
		Constructors.CollectionChanged += MemberAdded;
	}

	public override void WriteConstructors (CodeWriter writer)
	{
		foreach (var ctor in Constructors) {
			ctor.Write (writer);
			writer.WriteLine ();
		}
	}
}

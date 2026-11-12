using System;

namespace Xamarin.AndroidTools.AnnotationSupport
{
	public abstract class ManagedTypeFinderDefault : ManagedTypeFinder
	{
		public ManagedTypeFinderDefault ()
		{
			Extensions.Add (new DefinitionManagedTypeFinderExtension (this));
			Extensions.Add (new PermissionManagedTypeFinderExtension (this));
		}
	}
}


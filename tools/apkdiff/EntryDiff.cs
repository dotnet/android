using System;

namespace apkdiff {
	public abstract class EntryDiff {

		public abstract string Name { get; }

		public static EntryDiff ForExtension (string extension)
		{
			switch (extension) {
			case ".dll":
				return new AssemblyDiff ();
			case ".so":
				return new SharedLibraryDiff ();
			}

			return null;
		}

		public abstract void Compare (string file, string other, string padding = null);
	}
}

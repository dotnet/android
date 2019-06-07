using System;

namespace Xamarin.Android.Prepare
{
	abstract class ThirdPartyNotice : AppObject
	{
		public abstract string LicenseText { get; }
		public abstract string LicenseFile { get; }
		public abstract string Name        { get; }
		public abstract Uri    SourceUrl   { get; }

		public virtual bool Include (bool includeExternalDeps, bool includeBuildDeps) => true;

		public void EnsureValid ()
		{
			bool haveText = !String.IsNullOrEmpty (LicenseText);
			bool haveFile = !String.IsNullOrEmpty (LicenseFile);

			if (haveText && haveFile)
				throw new InvalidOperationException ($"Only one of LicenseText or LicenseFile properties can be set ({this})");

			if (!haveText && !haveFile)
				throw new InvalidOperationException ($"One of LicenseText or LicenseFile properties must be set ({this})");

			if (String.IsNullOrEmpty (Name))
				throw new InvalidOperationException ($"The Name property must be set ({this})");

			if (SourceUrl == null)
				throw new InvalidOperationException ($"The SourceUrl property must be set ({this})");
		}
	}
}

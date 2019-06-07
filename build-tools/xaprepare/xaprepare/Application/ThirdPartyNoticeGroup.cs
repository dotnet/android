using System;
using System.Collections.Generic;

namespace Xamarin.Android.Prepare
{
	abstract class ThirdPartyNoticeGroup : ThirdPartyNotice
	{
		static readonly Uri url            = new Uri ("http://not.an/item");

		public override string LicenseText => "not a license item";
		public override string LicenseFile => null;
		public override string Name        => "license item group";
		public override Uri    SourceUrl   => url;

		public override bool Include (bool includeExternalDeps, bool includeBuildDeps) => ShouldInclude (includeExternalDeps, includeBuildDeps);

		public abstract List<ThirdPartyNotice> Notices { get; }
		protected abstract bool ShouldInclude (bool includeExternalDeps, bool includeBuildDeps);
	}
}

using System;
namespace Xamarin.Installer.AndroidSDK
{
	class AndroidAddonDependencyInfo
	{
		public string ApiLevel { get; set; }
		public string NameId { get; set; }
		public string NameDisplay { get; set; }
		public string VendorId { get; set; }
		public string VendorDisplay { get; set; }
		public bool Unsure { get; set; }
	}
}


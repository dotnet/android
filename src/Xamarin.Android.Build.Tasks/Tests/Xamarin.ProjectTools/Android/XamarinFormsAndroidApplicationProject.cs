using System;
using Microsoft.Build.Construction;

namespace Xamarin.ProjectTools {
	public class XamarinFormsAndroidApplicationProject : XamarinAndroidApplicationProject {
		public XamarinFormsAndroidApplicationProject (string debugConfigurationName = "Debug", string releaseConfigurationName = "Release")
			: base ( debugConfigurationName, releaseConfigurationName )
		{
			Packages.Add ( KnownPackages.XamarinForms_3_0_0_561731 );
			Packages.Add ( KnownPackages.Android_Arch_Core_Common_26_1_0 );
			Packages.Add ( KnownPackages.Android_Arch_Lifecycle_Common_26_1_0 );
			Packages.Add ( KnownPackages.Android_Arch_Lifecycle_Runtime_26_1_0 );
			Packages.Add ( KnownPackages.AndroidSupportV4_27_0_2_1 );
			Packages.Add ( KnownPackages.SupportCompat_27_0_2_1 );
			Packages.Add ( KnownPackages.SupportCoreUI_27_0_2_1 );
			Packages.Add ( KnownPackages.SupportCoreUtils_27_0_2_1 );
			Packages.Add ( KnownPackages.SupportDesign_27_0_2_1 );
			Packages.Add ( KnownPackages.SupportFragment_27_0_2_1 );
			Packages.Add ( KnownPackages.SupportMediaCompat_27_0_2_1 );
			Packages.Add ( KnownPackages.SupportV7AppCompat_27_0_2_1 );
			Packages.Add ( KnownPackages.SupportV7CardView_27_0_2_1 );
			Packages.Add ( KnownPackages.SupportV7MediaRouter_27_0_2_1 );
			Packages.Add ( KnownPackages.SupportV7RecyclerView_27_0_2_1 );

			AndroidResources.Add ( new AndroidItem.AndroidResource ( "Resources\\layout\\Tabbar.axml" ) {
				TextContent = () => {
					return "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<android.support.design.widget.TabLayout xmlns:android=\"http://schemas.android.com/apk/res/android\" xmlns:app=\"http://schemas.android.com/apk/res-auto\" android:id=\"@+id/sliding_tabs\" android:background=\"?attr/colorPrimary\" android:theme=\"@style/ThemeOverlay.AppCompat.Dark.ActionBar\" app:tabIndicatorColor=\"@android:color/white\" app:tabGravity=\"fill\" app:tabMode=\"fixed\" />";
				}
			} );
		}
	}
}

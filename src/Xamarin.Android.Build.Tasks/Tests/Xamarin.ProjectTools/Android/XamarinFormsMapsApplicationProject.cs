using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace Xamarin.ProjectTools
{
	public class XamarinFormsMapsApplicationProject : XamarinFormsAndroidApplicationProject
	{
		static readonly string MainPageMaps_xaml;

		static XamarinFormsMapsApplicationProject ()
		{
			using (var sr = new StreamReader (typeof (XamarinAndroidApplicationProject).Assembly.GetManifestResourceStream ("Xamarin.ProjectTools.Resources.Forms.MainPageMaps.xaml")))
				MainPageMaps_xaml = sr.ReadToEnd ();
		}

		public XamarinFormsMapsApplicationProject ([CallerMemberName] string packageName = "")
			: base (packageName: packageName)
		{
			PackageReferences.Add (KnownPackages.XamarinFormsMaps_5_0_0_2515);
			PackageReferences.Add (KnownPackages.Xamarin_GooglePlayServices_Base);
			PackageReferences.Add (KnownPackages.Xamarin_GooglePlayServices_Basement);
			PackageReferences.Add (KnownPackages.Xamarin_GooglePlayServices_Maps);
			PackageReferences.Add (KnownPackages.Xamarin_GooglePlayServices_Tasks);
			PackageReferences.Add (KnownPackages.Xamarin_Build_Download);

			MainActivity = MainActivity.Replace ("//${AFTER_FORMS_INIT}", "Xamarin.FormsMaps.Init (this, savedInstanceState);");
			//NOTE: API_KEY metadata just has to *exist*
			AndroidManifest = AndroidManifest.Replace ("</application>", "<meta-data android:name=\"com.google.android.maps.v2.API_KEY\" android:value=\"\" /></application>");
			// From https://github.com/xamarin/GooglePlayServicesComponents/blob/fc057c754e04d3e719d8c111d03d60eb2467b9ce/source/com.google.android.gms/play-services-basement/buildtasks.tests/google-services.json
			OtherBuildItems.Add (new BuildItem ("GoogleServicesJson", "google-services.json") {
				Encoding = new System.Text.UTF8Encoding (encoderShouldEmitUTF8Identifier: false),
				TextContent = () =>
@"{
  ""project_info"": {
    ""project_number"": ""1041063143217"",
    ""firebase_url"": ""https://white-cedar-97320.firebaseio.com"",
    ""project_id"": ""white-cedar-97320"",
    ""storage_bucket"": ""white-cedar-97320.appspot.com""
  },
  ""client"": [
    {
      ""client_info"": {
        ""mobilesdk_app_id"": ""1:1041063143217:android:ffbe6976403db935"",
        ""android_client_info"": {
          ""package_name"": """ + packageName + @"""
        }
      },
      ""oauth_client"": [
        {
          ""client_id"": ""1041063143217-rve97omgqivvs3qcne1ljso137k3t6po.apps.googleusercontent.com"",
          ""client_type"": 1,
          ""android_info"": {
            ""package_name"": """ + packageName + @""",
            ""certificate_hash"": ""84949BBD3F34C8290A55AC9B66AD0A701EBA67AC""
          }
        },
        {
          ""client_id"": ""1041063143217-hu5u4dnv8dkj19i4tpi6piv97kd2k9i0.apps.googleusercontent.com"",
          ""client_type"": 3
        },
        {
          ""client_id"": ""1041063143217-n82odtjjgs9g2qnh1t470mdhj086id9f.apps.googleusercontent.com"",
          ""client_type"": 3
        }
      ],
      ""api_key"": [
        {
          ""current_key"": ""NOT_A_REAL_KEY""
        }
      ],
      ""services"": {
        ""analytics_service"": {
          ""status"": 2,
          ""analytics_property"": {
            ""tracking_id"": ""UA-6465612-26""
          }
        },
        ""appinvite_service"": {
          ""status"": 2,
          ""other_platform_oauth_client"": [
            {
              ""client_id"": ""1041063143217-hu5u4dnv8dkj19i4tpi6piv97kd2k9i0.apps.googleusercontent.com"",
              ""client_type"": 3
            },
            {
              ""client_id"": ""1041063143217-rdc97s7jssl1k29c83b6oci04sihqkdi.apps.googleusercontent.com"",
              ""client_type"": 2,
              ""ios_info"": {
                ""bundle_id"": ""com.xamarin.googleios.collectallthestars""
              }
            }
          ]
        },
        ""ads_service"": {
          ""status"": 2
        }
      }
    }
  ],
  ""configuration_version"": ""1""
}"
			});
		}

		protected override string MainPageXaml () => ProcessSourceTemplate (MainPageMaps_xaml);
	}
}

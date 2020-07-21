using System;
using System.IO;

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

		public XamarinFormsMapsApplicationProject ()
		{
			if (Builder.UseDotNet) {
				PackageReferences.Add (KnownPackages.XamarinFormsMaps_4_7_0_1142);
			} else {
				PackageReferences.Add (KnownPackages.XamarinFormsMaps_4_0_0_425677);
			}
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
          ""package_name"": ""com.xamarin.sample""
        }
      },
      ""oauth_client"": [
        {
          ""client_id"": ""1041063143217-rve97omgqivvs3qcne1ljso137k3t6po.apps.googleusercontent.com"",
          ""client_type"": 1,
          ""android_info"": {
            ""package_name"": ""com.xamarin.sample"",
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
          ""current_key"": ""AIzaSyCfJp9rrUEaA07vdoGvGQgJqm0Fa9cJGiw""
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

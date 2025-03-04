using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks;
public class AndroidNativeAOTDebug : AndroidTask
{
    public override string TaskPrefix => "ANAD";

    // What do you need to do. 
    // 1. Install and run up the LLdb server.
    // 2. Start the app.
    // 3. Get the process Id.
    public override bool RunTask ()
	{
        return !Log.HasLoggedErrors;
    }
}
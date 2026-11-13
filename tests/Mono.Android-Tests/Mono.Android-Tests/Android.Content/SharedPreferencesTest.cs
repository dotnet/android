using System;
using Android.App;
using Android.Content;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Android.ContentTests;

[TestClass]
public class SharedPreferencesTest
{
    const string Name = "testpreferences";
    const int Count = 1000;

    ISharedPreferences GetPreferences (string name = Name) =>
        Application.Context.GetSharedPreferences (name, FileCreationMode.Private);

    [TestMethod]
    public void PutAndGetManyValues ()
    {
        // Each overlapping Apply snapshots the map; batch the values to avoid retaining growing copies.
        using (var prefs = GetPreferences ()) {
            using var editor = prefs.Edit ();
            for (int i = 0; i < Count; i++) {
                editor.PutString ("key" + i, "value" + i);
            }
            editor.Apply ();
        }

        for (int i = 0; i < Count; i++) {
            using var prefs = GetPreferences ();
            Assert.AreEqual ("value" + i, prefs.GetString ("key" + i, null));
        }
    }

    // Regression for https://github.com/dotnet/android/pull/9136.
    // NOTE: test case on API 23 can trigger:
    // art/runtime/indirect_reference_table.cc:115] JNI ERROR (app bug): local reference table overflow
    [TestMethod]
    public void RepeatedEditorsDoNotLeakReferences ()
    {
        for (int i = 0; i < Count; i++) {
            using var prefs = GetPreferences (Name + "-editors");
            using var editor = prefs.Edit ();
            editor.PutString ("key", "value" + i);
            editor.Apply ();
            Assert.AreEqual ("value" + i, prefs.GetString ("key", null));
        }
    }
}

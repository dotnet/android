using System;
using Android.App;
using Android.Content;
using NUnit.Framework;

namespace Android.ContentTests;

[TestFixture]
public class SharedPreferencesTest
{
    const string Name = "testpreferences";
    const int Count = 1000;

    ISharedPreferences GetPreferences () =>
        Application.Context.GetSharedPreferences (Name, FileCreationMode.Private);

    // NOTE: test case on API 23 can trigger:
    // art/runtime/indirect_reference_table.cc:115] JNI ERROR (app bug): local reference table overflow 
    [Test]
    public void PutAndGetManyValues ()
    {
        for (int i = 0; i < Count; i++) {
            using var prefs = GetPreferences ();
            using var editor = prefs.Edit ();
            editor.PutString ("key" + i, "value" + i);
            editor.Apply ();
        }

        for (int i = 0; i < Count; i++) {
            using var prefs = GetPreferences ();
            Assert.AreEqual ("value" + i, prefs.GetString ("key" + i, null));
        }
    }
}

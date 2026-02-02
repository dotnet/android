using Android.App;
using Android.OS;
using Android.Util;
using Android.Runtime;
using Java.Util;
using System;
using System.Collections.Generic;

namespace NewTypeMapPoc;

[Activity(Label = "@string/app_name", MainLauncher = true)]
public class MainActivity : Activity
{
    const string TAG = "TypeMapPoc";

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        SetContentView(Resource.Layout.activity_main);

        Log.Info(TAG, "=== Trimmable TypeMap IList/Dictionary Tests ===");
        
        try
        {
            TestJavaListString();
            TestJavaListInteger();
            TestJavaListFromJniHandle();
            TestJavaSet();
            TestJavaDictionary();
            TestArrayOfStrings();
            TestArrayOfIntegers();
            TestMixedObjectArray();
            
            Log.Info(TAG, "=== All tests passed! ===");
        }
        catch (Exception ex)
        {
            Log.Error(TAG, $"Test failed: {ex}");
        }
    }

    void TestJavaListString()
    {
        Log.Info(TAG, "--- TestJavaListString ---");
        
        // Create a JavaList<string> and add items
        var list = new JavaList<string>();
        list.Add("Hello");
        list.Add("World");
        list.Add("Trimmable TypeMap");
        
        Log.Info(TAG, $"Created JavaList<string> with {list.Count} items");
        
        // Iterate and log
        foreach (var item in list)
        {
            Log.Info(TAG, $"  Item: {item}");
        }
        
        // Verify count
        if (list.Count != 3)
            throw new Exception($"Expected 3 items, got {list.Count}");
        
        // Verify indexer
        if (list[0] != "Hello")
            throw new Exception($"Expected 'Hello', got '{list[0]}'");
        
        Log.Info(TAG, "TestJavaListString PASSED");
    }

    void TestJavaListInteger()
    {
        Log.Info(TAG, "--- TestJavaListInteger ---");
        
        // Create a JavaList<Java.Lang.Integer> 
        var list = new JavaList<Java.Lang.Integer>();
        list.Add(new Java.Lang.Integer(42));
        list.Add(new Java.Lang.Integer(100));
        list.Add(new Java.Lang.Integer(-1));
        
        Log.Info(TAG, $"Created JavaList<Java.Lang.Integer> with {list.Count} items");
        
        // Verify
        if (list.Count != 3)
            throw new Exception($"Expected 3 items, got {list.Count}");
        
        var first = list[0];
        if (first.IntValue() != 42)
            throw new Exception($"Expected 42, got {first.IntValue()}");
        
        Log.Info(TAG, "TestJavaListInteger PASSED");
    }

    void TestJavaListFromJniHandle()
    {
        Log.Info(TAG, "--- TestJavaListFromJniHandle ---");
        
        // Create a Java ArrayList directly
        var javaArrayList = new ArrayList();
        javaArrayList.Add("From");
        javaArrayList.Add("JNI");
        javaArrayList.Add("Handle");
        
        // Get its handle and create a JavaList<string> wrapper
        var handle = javaArrayList.Handle;
        var wrappedList = new JavaList<string>(handle, JniHandleOwnership.DoNotTransfer);
        
        Log.Info(TAG, $"Wrapped ArrayList with {wrappedList.Count} items");
        
        // Verify items
        if (wrappedList.Count != 3)
            throw new Exception($"Expected 3 items, got {wrappedList.Count}");
        
        if (wrappedList[1] != "JNI")
            throw new Exception($"Expected 'JNI', got '{wrappedList[1]}'");
        
        // Modify through wrapper
        wrappedList.Add("Added");
        
        // Verify original Java list was modified
        if (javaArrayList.Size() != 4)
            throw new Exception($"Expected 4 items in original, got {javaArrayList.Size()}");
        
        Log.Info(TAG, "TestJavaListFromJniHandle PASSED");
    }

    void TestJavaSet()
    {
        Log.Info(TAG, "--- TestJavaSet ---");
        
        var set = new JavaSet<string>();
        set.Add("Apple");
        set.Add("Banana");
        set.Add("Cherry");
        set.Add("Apple"); // Duplicate - should not increase count
        
        Log.Info(TAG, $"Created JavaSet<string> with {set.Count} items");
        
        // Sets don't allow duplicates
        if (set.Count != 3)
            throw new Exception($"Expected 3 items (no duplicates), got {set.Count}");
        
        if (!set.Contains("Banana"))
            throw new Exception("Set should contain 'Banana'");
        
        Log.Info(TAG, "TestJavaSet PASSED");
    }

    void TestJavaDictionary()
    {
        Log.Info(TAG, "--- TestJavaDictionary ---");
        
        var dict = new JavaDictionary<string, Java.Lang.Integer>();
        dict["one"] = new Java.Lang.Integer(1);
        dict["two"] = new Java.Lang.Integer(2);
        dict["three"] = new Java.Lang.Integer(3);
        
        Log.Info(TAG, $"Created JavaDictionary<string, Integer> with {dict.Count} entries");
        
        // Verify count
        if (dict.Count != 3)
            throw new Exception($"Expected 3 entries, got {dict.Count}");
        
        // Verify lookup
        var value = dict["two"];
        if (value.IntValue() != 2)
            throw new Exception($"Expected 2, got {value.IntValue()}");
        
        // Verify ContainsKey
        if (!dict.ContainsKey("three"))
            throw new Exception("Dictionary should contain key 'three'");
        
        Log.Info(TAG, "TestJavaDictionary PASSED");
    }

    void TestArrayOfStrings()
    {
        Log.Info(TAG, "--- TestArrayOfStrings ---");
        
        // Create managed array and convert to Java
        string[] managedArray = { "Alpha", "Beta", "Gamma" };
        
        // Use JNIEnv to create Java array
        var javaArrayHandle = JNIEnv.NewArray(managedArray);
        
        Log.Info(TAG, $"Created Java String[] with handle {javaArrayHandle:X}");
        
        // Get the class name
        var className = JNIEnv.GetClassNameFromInstance(javaArrayHandle);
        Log.Info(TAG, $"Java array class: {className}");
        
        // Expected: [Ljava/lang/String;
        if (!className.Contains("String"))
            throw new Exception($"Expected String array, got {className}");
        
        // Convert back to managed array
        var roundTripped = JNIEnv.GetArray<string>(javaArrayHandle);
        
        if (roundTripped.Length != 3)
            throw new Exception($"Expected 3 items, got {roundTripped.Length}");
        
        if (roundTripped[1] != "Beta")
            throw new Exception($"Expected 'Beta', got '{roundTripped[1]}'");
        
        JNIEnv.DeleteLocalRef(javaArrayHandle);
        
        Log.Info(TAG, "TestArrayOfStrings PASSED");
    }

    void TestArrayOfIntegers()
    {
        Log.Info(TAG, "--- TestArrayOfIntegers ---");
        
        // Test primitive int array
        int[] primitiveArray = { 10, 20, 30, 40, 50 };
        var javaArrayHandle = JNIEnv.NewArray(primitiveArray);
        
        Log.Info(TAG, $"Created Java int[] with handle {javaArrayHandle:X}");
        
        var className = JNIEnv.GetClassNameFromInstance(javaArrayHandle);
        Log.Info(TAG, $"Java array class: {className}");
        
        // Expected: [I
        if (className != "[I")
            throw new Exception($"Expected '[I', got {className}");
        
        // Convert back
        var roundTripped = JNIEnv.GetArray<int>(javaArrayHandle);
        
        if (roundTripped.Length != 5)
            throw new Exception($"Expected 5 items, got {roundTripped.Length}");
        
        if (roundTripped[2] != 30)
            throw new Exception($"Expected 30, got {roundTripped[2]}");
        
        JNIEnv.DeleteLocalRef(javaArrayHandle);
        
        Log.Info(TAG, "TestArrayOfIntegers PASSED");
    }

    void TestMixedObjectArray()
    {
        Log.Info(TAG, "--- TestMixedObjectArray ---");
        
        // Create an Object[] with mixed types
        Java.Lang.Object[] mixedArray = {
            new Java.Lang.String("A string"),
            new Java.Lang.Integer(42),
            this // Activity is a Java.Lang.Object subclass
        };
        
        var javaArrayHandle = JNIEnv.NewArray(mixedArray);
        
        Log.Info(TAG, $"Created Java Object[] with handle {javaArrayHandle:X}");
        
        var className = JNIEnv.GetClassNameFromInstance(javaArrayHandle);
        Log.Info(TAG, $"Java array class: {className}");
        
        // Expected: [Ljava/lang/Object;
        if (!className.Contains("Object"))
            throw new Exception($"Expected Object array, got {className}");
        
        // Read back the elements - they should resolve to their actual types
        var element0Handle = JNIEnv.GetObjectArrayElement(javaArrayHandle, 0);
        var element0Class = JNIEnv.GetClassNameFromInstance(element0Handle);
        Log.Info(TAG, $"Element 0 class: {element0Class}");
        
        var element1Handle = JNIEnv.GetObjectArrayElement(javaArrayHandle, 1);
        var element1Class = JNIEnv.GetClassNameFromInstance(element1Handle);
        Log.Info(TAG, $"Element 1 class: {element1Class}");
        
        var element2Handle = JNIEnv.GetObjectArrayElement(javaArrayHandle, 2);
        var element2Class = JNIEnv.GetClassNameFromInstance(element2Handle);
        Log.Info(TAG, $"Element 2 class: {element2Class}");
        
        // The Activity element should have a JCW class name
        if (!element2Class.Contains("MainActivity"))
            Log.Warn(TAG, $"Expected MainActivity JCW, got {element2Class}");
        
        JNIEnv.DeleteLocalRef(element0Handle);
        JNIEnv.DeleteLocalRef(element1Handle);
        JNIEnv.DeleteLocalRef(element2Handle);
        JNIEnv.DeleteLocalRef(javaArrayHandle);
        
        Log.Info(TAG, "TestMixedObjectArray PASSED");
    }
}
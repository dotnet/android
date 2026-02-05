using Android.App;
using Android.OS;
using Android.Widget;
using Android.Views;
using Android.Content;
using Android.Util;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Java.Lang;
using Java.Util;

namespace NativeAotStressTest;

[Activity(Label = "@string/app_name", MainLauncher = true)]
public class MainActivity : Activity
{
    TextView? _statusText;
    TextView? _resultsText;
    System.Text.StringBuilder? _log;
    
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        SetContentView(Resource.Layout.activity_main);
        
        _log = new System.Text.StringBuilder();
        _statusText = FindViewById<TextView>(Resource.Id.statusText);
        _resultsText = FindViewById<TextView>(Resource.Id.resultsText);
        
        var runButton = FindViewById<Button>(Resource.Id.runAllTests);
        runButton!.Click += async (s, e) => await RunAllTestsAsync();
        
        Log.Info("StressTest", "MainActivity created");
    }
    
    async Task RunAllTestsAsync()
    {
        Log.Info("StressTest", "RunAllTestsAsync entered");
        if (_log == null) {
            _log = new System.Text.StringBuilder();
        }
        _log.Clear();
        Log.Info("StressTest", "Log cleared");
        if (_statusText != null) {
            _statusText.Text = "Running tests...";
        }
        Log.Info("StressTest", "Starting tests...");
        
        try {
            // Run non-UI tests on background thread
            await Task.Run(() => {
                RunTest("1. IList<T> marshalling", TestListMarshalling);
                RunTest("2. IDictionary<K,V> marshalling", TestDictionaryMarshalling);
                RunTest("3. Interface callbacks", TestInterfaceCallbacks);
                RunTest("4. Abstract class activation", TestAbstractClassActivation);
                RunTest("5. Java.Util collections", TestJavaUtilCollections);
                RunTest("6. Parcelable", TestParcelable);
                RunTest("7. Async Java callbacks", TestAsyncJavaCallbacks);
                RunTest("8. Exception handling", TestExceptionHandling);
                RunTest("9. String interop", TestStringInterop);
                RunTest("10. Array marshalling", TestArrayMarshalling);
                RunTest("11. Object identity", TestObjectIdentity);
        });
        } catch (System.Exception ex) {
            Log.Error("StressTest", $"Background task failed: {ex}");
            _log?.AppendLine($"✗ Background tests failed: {ex.Message}");
        }
        
        // Run UI tests on UI thread
        RunTest("12. Event handlers (Click)", TestEventHandlers);
        RunTest("13. Nested types", TestNestedTypes);
        RunTest("14. Generic collections", TestGenericCollections);
        RunTest("15. Handler/Looper", TestHandlerLooper);
        
        if (_statusText != null) {
            _statusText.Text = "Tests completed!";
        }
        if (_resultsText != null) {
            _resultsText.Text = _log?.ToString() ?? "";
        }
        Log.Info("StressTest", "All tests finished");
    }
    
    void RunTest(string name, Action test)
    {
        Log.Info("StressTest", $"Starting: {name}");
        try {
            test();
            _log?.AppendLine($"✓ {name}");
            Log.Info("StressTest", $"PASS: {name}");
        } catch (System.Exception ex) {
            _log?.AppendLine($"✗ {name}: {ex.Message}");
            Log.Error("StressTest", $"FAIL: {name}: {ex}");
        }
    }
    
    // Test 1: Event handlers (uses Implementor types)
    void TestEventHandlers()
    {
        var button = new Button(this);
        bool clicked = false;
        button.Click += (s, e) => clicked = true;
        button.PerformClick();
        if (!clicked) throw new System.Exception("Click event not fired");
    }
    
    // Test 2: IList<T> marshalling
    void TestListMarshalling()
    {
        var javaList = new Java.Util.ArrayList();
        javaList.Add("one");
        javaList.Add("two");
        javaList.Add("three");
        
        // Convert to IList<string>
        var list = new Android.Runtime.JavaList<string>(javaList.Handle, Android.Runtime.JniHandleOwnership.DoNotTransfer);
        if (list.Count != 3) throw new System.Exception($"Expected 3, got {list.Count}");
        if (list[0] != "one") throw new System.Exception($"Expected 'one', got '{list[0]}'");
    }
    
    // Test 3: IDictionary<K,V> marshalling  
    void TestDictionaryMarshalling()
    {
        var javaMap = new Java.Util.HashMap();
        javaMap.Put("key1", "value1");
        javaMap.Put("key2", "value2");
        
        var dict = new Android.Runtime.JavaDictionary<string, string>(javaMap.Handle, Android.Runtime.JniHandleOwnership.DoNotTransfer);
        if (dict.Count != 2) throw new System.Exception($"Expected 2, got {dict.Count}");
        if (dict["key1"] != "value1") throw new System.Exception($"Expected 'value1', got '{dict["key1"]}'");
    }
    
    // Test 4: Interface callbacks (Runnable, Callable)
    void TestInterfaceCallbacks()
    {
        bool ran = false;
        var runnable = new Java.Lang.Runnable(() => ran = true);
        runnable.Run();
        if (!ran) throw new System.Exception("Runnable did not run");
        
        // Test Callable<T>
        var callable = new MyCallable("test-result");
        var result = callable.Call();
        if (result?.ToString() != "test-result") throw new System.Exception($"Callable returned wrong value");
    }
    
    // Test 5: Abstract class activation (invoker types)
    void TestAbstractClassActivation()
    {
        // AsyncTask is abstract - tests invoker creation
        var inputStream = new Java.IO.ByteArrayInputStream(new byte[] { 1, 2, 3 });
        int b = inputStream.Read();
        if (b != 1) throw new System.Exception($"Expected 1, got {b}");
        inputStream.Close();
    }
    
    // Test 6: Nested types
    void TestNestedTypes()
    {
        // Build.VERSION_CODES is a nested class
        int sdk = (int)Android.OS.Build.VERSION.SdkInt;
        if (sdk < 24) throw new System.Exception($"SDK too low: {sdk}");
        
        // DialogInterface.IOnClickListener is a nested interface
        var dialog = new AlertDialog.Builder(this);
        dialog.SetTitle("Test");
        dialog.SetPositiveButton("OK", (IDialogInterfaceOnClickListener?)null);
    }
    
    // Test 7: Generic collections with Java peer types
    void TestGenericCollections()
    {
        var views = new List<View>();
        views.Add(new TextView(this));
        views.Add(new Button(this));
        views.Add(new EditText(this));
        
        if (views.Count != 3) throw new System.Exception($"Expected 3 views");
        if (views[1] is not Button) throw new System.Exception("Second view should be Button");
    }
    
    // Test 8: Java.Util collections
    void TestJavaUtilCollections()
    {
        var arrayList = new Java.Util.ArrayList();
        arrayList.Add(Java.Lang.Integer.ValueOf(1));
        arrayList.Add(Java.Lang.Integer.ValueOf(2));
        
        var iterator = arrayList.Iterator();
        int sum = 0;
        while (iterator.HasNext) {
            var obj = iterator.Next();
            sum += ((Java.Lang.Integer)obj!).IntValue();
        }
        if (sum != 3) throw new System.Exception($"Expected sum 3, got {sum}");
    }
    
    // Test 9: Parcelable (Bundle)
    void TestParcelable()
    {
        var bundle = new Bundle();
        bundle.PutString("key", "value");
        bundle.PutInt("number", 42);
        bundle.PutBoolean("flag", true);
        
        var str = bundle.GetString("key");
        var num = bundle.GetInt("number");
        var flag = bundle.GetBoolean("flag");
        
        if (str != "value") throw new System.Exception($"Expected 'value', got '{str}'");
        if (num != 42) throw new System.Exception($"Expected 42, got {num}");
        if (!flag) throw new System.Exception("Expected true");
    }
    
    // Test 10: Handler/Looper
    void TestHandlerLooper()
    {
        var looper = Looper.MainLooper;
        if (looper == null) throw new System.Exception("MainLooper is null");
        
        var handler = new Handler(looper);
        bool posted = handler.Post(new Java.Lang.Runnable(() => { }));
        if (!posted) throw new System.Exception("Post failed");
    }
    
    // Test 11: Async Java callbacks
    void TestAsyncJavaCallbacks()
    {
        var latch = new Java.Util.Concurrent.CountDownLatch(1);
        
        var executor = Java.Util.Concurrent.Executors.NewSingleThreadExecutor();
        executor.Execute(new Java.Lang.Runnable(() => {
            latch.CountDown();
        }));
        
        bool completed = latch.Await(1000, Java.Util.Concurrent.TimeUnit.Milliseconds!);
        executor.Shutdown();
        
        if (!completed) throw new System.Exception("Latch timeout");
    }
    
    // Test 12: Exception handling across JNI
    void TestExceptionHandling()
    {
        try {
            // This should throw an exception from Java
            var file = new Java.IO.File("/nonexistent/path/that/does/not/exist");
            var fis = new Java.IO.FileInputStream(file);
            fis.Close();
            throw new System.Exception("Should have thrown FileNotFoundException");
        } catch (Java.IO.FileNotFoundException) {
            // Expected
        }
    }
    
    // Test 13: String interop
    void TestStringInterop()
    {
        var javaString = new Java.Lang.String("Hello, World!");
        string netString = javaString.ToString();
        if (netString != "Hello, World!") throw new System.Exception($"String mismatch: {netString}");
        
        // Test StringBuilder
        var sb = new Java.Lang.StringBuilder();
        sb.Append("Hello");
        sb.Append(" ");
        sb.Append("World");
        var result = sb.ToString();
        if (result != "Hello World") throw new System.Exception($"StringBuilder mismatch: {result}");
    }
    
    // Test 14: Array marshalling
    void TestArrayMarshalling()
    {
        // Primitive array
        byte[] bytes = { 1, 2, 3, 4, 5 };
        var bais = new Java.IO.ByteArrayInputStream(bytes);
        byte[] buffer = new byte[5];
        bais.Read(buffer, 0, 5);
        bais.Close();
        
        for (int i = 0; i < 5; i++) {
            if (buffer[i] != bytes[i]) throw new System.Exception($"Byte mismatch at {i}");
        }
        
        // Object array
        var strings = new Java.Lang.String[] {
            new Java.Lang.String("a"),
            new Java.Lang.String("b"),
            new Java.Lang.String("c")
        };
        if (strings.Length != 3) throw new System.Exception("Array length mismatch");
    }
    
    // Test 15: Object identity (same Java object = same .NET wrapper)
    void TestObjectIdentity()
    {
        var list = new Java.Util.ArrayList();
        var obj = Java.Lang.Integer.ValueOf(42);
        list.Add(obj);
        
        var retrieved = list.Get(0);
        // Both should wrap the same Java object
        if (obj!.Handle != retrieved!.Handle) throw new System.Exception("Handle mismatch - identity not preserved");
    }
}

// Custom Callable implementation for Test 4
class MyCallable : Java.Lang.Object, Java.Util.Concurrent.ICallable
{
    string _result;
    
    public MyCallable(string result)
    {
        _result = result;
    }
    
    public Java.Lang.Object? Call()
    {
        return new Java.Lang.String(_result);
    }
}

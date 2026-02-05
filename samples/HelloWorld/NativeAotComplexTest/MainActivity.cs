using Android.App;
using Android.Content;
using Android.Database;
using Android.Database.Sqlite;
using Android.Graphics;
using Android.Hardware;
using Android.Media;
using Android.Net;
using Android.OS;
using Android.Runtime;
using Android.Text;
using Android.Util;
using Android.Views;
using Android.Webkit;
using Android.Widget;
using Java.Interop;
using Java.IO;
using Java.Lang;
using Java.Lang.Reflect;
using Java.Net;
using Java.Util;
using Java.Util.Concurrent;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NativeAotComplexTest;

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
        
        Log.Info("ComplexTest", "MainActivity created");
        
        // Auto-run tests if launched with --es autorun true
        if (Intent?.GetBooleanExtra("autorun", false) == true) {
            Log.Info("ComplexTest", "Auto-run mode detected");
            _ = RunAllTestsAsync();
        }
    }
    
    async Task RunAllTestsAsync()
    {
        Log.Info("ComplexTest", "Starting complex tests...");
        _log?.Clear();
        if (_statusText != null) _statusText.Text = "Running tests...";
        
        try {
            await Task.Run(() => {
                // Handler/Looper patterns
                RunTest("1. Handler postDelayed", TestHandlerPostDelayed);
                RunTest("2. HandlerThread creation", TestHandlerThread);
                
                // Media APIs
                RunTest("3. MediaPlayer creation", TestMediaPlayerCreation);
                
                // Complex callbacks
                RunTest("4. AsyncTask pattern", TestAsyncTaskPattern);
                RunTest("5. CountDownLatch", TestCountDownLatch);
                
                // Reflection patterns
                RunTest("6. Method.invoke", TestMethodInvoke);
                RunTest("7. Constructor.newInstance", TestConstructorNewInstance);
                RunTest("8. Field access", TestFieldAccess);
            });
        } catch (System.Exception ex) {
            Log.Error("ComplexTest", $"Background error: {ex}");
            _log?.AppendLine($"✗ Background error: {ex.Message}");
        }
        
        // UI thread tests
        RunTest("9. WebView JavaScript", TestWebViewJavaScript);
        RunTest("10. BaseAdapter subclass", TestBaseAdapterSubclass);
        RunTest("11. TextWatcher", TestTextWatcher);
        RunTest("12. GestureDetector", TestGestureDetector);
        RunTest("12. ScaleGestureDetector", TestScaleGestureDetector);
        RunTest("13. PopupWindow", TestPopupWindow);
        RunTest("14. TabHost patterns", TestTabHostPatterns);
        RunTest("15. Sensor callbacks", TestSensorCallbacks);
        RunTest("16. Network callback", TestNetworkCallback);
        RunTest("17. Custom Parcelable", TestCustomParcelable);
        RunTest("18. Bundle with objects", TestBundleWithObjects);
        RunTest("19. ClipboardManager", TestClipboardManager);
        RunTest("20. VibrationEffect", TestVibrationEffect);
        RunTest("21. Notification builder", TestNotificationBuilder);
        
        // Advanced array and collection tests
        RunTest("22. Primitive int array", TestPrimitiveIntArray);
        RunTest("23. Primitive byte array", TestPrimitiveByteArray);
        RunTest("24. 2D int array", Test2DIntArray);
        RunTest("25. Object array", TestObjectArray);
        RunTest("26. String array return", TestStringArrayReturn);
        RunTest("27. IDictionary marshalling", TestIDictionaryMarshalling);
        RunTest("28. Nested collections", TestNestedCollections);
        
        // Enum and exception tests
        RunTest("29. Java enum marshalling", TestJavaEnumMarshalling);
        RunTest("30. Exception marshalling", TestExceptionMarshalling);
        
        // Advanced Java interop
        RunTest("31. Java.Lang.Class usage", TestJavaLangClass);
        // Note: [Export] is NOT supported in NativeAOT - it requires dynamic code generation
        // RunTest("32. Export attribute", TestExportAttribute);
        RunTest("32. Fragment lifecycle", TestFragmentLifecycle);
        RunTest("33. MultiChoiceMode listener", TestRecyclerViewAdapter);
        
        if (_statusText != null) _statusText.Text = "Tests completed!";
        if (_resultsText != null) _resultsText.Text = _log?.ToString() ?? "";
        Log.Info("ComplexTest", "All complex tests finished");
    }
    
    void RunTest(string name, Action test)
    {
        Log.Info("ComplexTest", $"Starting: {name}");
        try {
            test();
            _log?.AppendLine($"✓ {name}");
            Log.Info("ComplexTest", $"PASS: {name}");
        } catch (System.Exception ex) {
            _log?.AppendLine($"✗ {name}: {ex.Message}");
            Log.Error("ComplexTest", $"FAIL: {name}: {ex}");
        }
    }
    
    // Test 1: Handler postDelayed
    void TestHandlerPostDelayed()
    {
        var handler = new Handler(Looper.MainLooper!);
        bool executed = false;
        var runnable = new Java.Lang.Runnable(() => executed = true);
        
        handler.PostDelayed(runnable, 1);
        // We can't wait here, just test creation
        if (handler.Looper == null)
            throw new System.Exception("Handler has no looper");
    }
    
    // Test 2: HandlerThread creation
    void TestHandlerThread()
    {
        using var handlerThread = new HandlerThread("TestThread");
        handlerThread.Start();
        
        var looper = handlerThread.Looper;
        if (looper == null) throw new System.Exception("HandlerThread has no looper");
        
        var handler = new Handler(looper);
        if (handler.Looper != looper)
            throw new System.Exception("Handler looper mismatch");
        
        handlerThread.Quit();
    }
    
    // Test 3: MediaPlayer creation
    void TestMediaPlayerCreation()
    {
        using var player = new MediaPlayer();
        if (player == null) throw new System.Exception("MediaPlayer is null");
        
        // Test listener attachment
        player.SetOnCompletionListener(new TestOnCompletionListener());
        player.SetOnErrorListener(new TestOnErrorListener());
        player.SetOnPreparedListener(new TestOnPreparedListener());
    }
    
    // Test 4: AsyncTask-like pattern (using ExecutorService)
    void TestAsyncTaskPattern()
    {
        var executor = Executors.NewSingleThreadExecutor();
        if (executor == null) throw new System.Exception("Executor is null");
        
        var latch = new CountDownLatch(1);
        executor.Execute(new Java.Lang.Runnable(() => {
            // Simulate work
            latch.CountDown();
        }));
        
        bool completed = latch.Await(1000, TimeUnit.Milliseconds!);
        executor.Shutdown();
        
        if (!completed) throw new System.Exception("Task did not complete");
    }
    
    // Test 5: CountDownLatch
    void TestCountDownLatch()
    {
        var latch = new CountDownLatch(3);
        for (int i = 0; i < 3; i++) {
            latch.CountDown();
        }
        
        if (latch.Count != 0)
            throw new System.Exception($"Latch count should be 0, got {latch.Count}");
    }
    
    // Test 6: Method.invoke
    void TestMethodInvoke()
    {
        var str = new Java.Lang.String("hello");
        var clazz = str.Class;
        var method = clazz.GetMethod("length");
        
        var result = method?.Invoke(str);
        if (result == null) throw new System.Exception("Method.invoke returned null");
        
        int length = ((Java.Lang.Integer)result).IntValue();
        if (length != 5) throw new System.Exception($"Expected 5, got {length}");
    }
    
    // Test 7: Constructor.newInstance
    void TestConstructorNewInstance()
    {
        var clazz = Java.Lang.Class.ForName("java.lang.StringBuilder");
        var ctor = clazz?.GetConstructor();
        
        var obj = ctor?.NewInstance();
        if (obj == null) throw new System.Exception("Constructor.newInstance returned null");
        
        if (!(obj is Java.Lang.StringBuilder))
            throw new System.Exception("Created wrong type");
    }
    
    // Test 8: Field access via reflection
    void TestFieldAccess()
    {
        var clazz = Java.Lang.Class.ForName("java.lang.Integer");
        var field = clazz?.GetField("MAX_VALUE");
        
        var value = field?.Get(null);
        if (value == null) throw new System.Exception("Field.get returned null");
        
        int maxValue = ((Java.Lang.Integer)value).IntValue();
        if (maxValue != int.MaxValue)
            throw new System.Exception($"Wrong MAX_VALUE: {maxValue}");
    }
    
    // Test 9: WebView JavaScript interface
    void TestWebViewJavaScript()
    {
        using var webView = new WebView(this);
        webView.Settings.JavaScriptEnabled = true;
        
        // Add JavaScript interface
        var jsInterface = new TestJavaScriptInterface();
        webView.AddJavascriptInterface(jsInterface, "Android");
        
        // Just test creation, don't load content
        if (webView.Settings == null)
            throw new System.Exception("WebView settings is null");
    }
    
    // Test 10: BaseAdapter subclass
    void TestBaseAdapterSubclass()
    {
        using var adapter = new TestBaseAdapter(this);
        
        if (adapter.Count != 5)
            throw new System.Exception($"Expected 5 items, got {adapter.Count}");
        
        var item = adapter.GetItem(0);
        if (item == null) throw new System.Exception("GetItem returned null");
    }
    
    // Test 11: TextWatcher
    void TestTextWatcher()
    {
        Log.Info("ComplexTest", "TextWatcher: Creating EditText");
        using var editText = new EditText(this);
        Log.Info("ComplexTest", "TextWatcher: Creating TestTextWatcher");
        var watcher = new TestTextWatcher();
        Log.Info("ComplexTest", "TextWatcher: Adding listener");
        editText.AddTextChangedListener(watcher);
        
        Log.Info("ComplexTest", "TextWatcher: Setting text - this will trigger callbacks");
        editText.Text = "test";
        Log.Info("ComplexTest", "TextWatcher: Text set successfully");
        
        // Just verify no crash
        editText.RemoveTextChangedListener(watcher);
        Log.Info("ComplexTest", "TextWatcher: Test complete");
    }
    
    // Test 12: GestureDetector
    void TestGestureDetector()
    {
        var listener = new TestGestureListener();
        using var detector = new GestureDetector(this, listener);
        
        if (detector == null)
            throw new System.Exception("GestureDetector is null");
    }
    
    // Test 13: ScaleGestureDetector
    void TestScaleGestureDetector()
    {
        var listener = new TestScaleGestureListener();
        using var detector = new ScaleGestureDetector(this, listener);
        
        if (detector == null)
            throw new System.Exception("ScaleGestureDetector is null");
    }
    
    // Test 14: PopupWindow
    void TestPopupWindow()
    {
        using var content = new TextView(this);
        content.Text = "Popup";
        
        using var popup = new PopupWindow(content, 100, 100);
        popup.Focusable = true;
        
        // Don't show, just test creation
        if (!popup.Focusable)
            throw new System.Exception("PopupWindow not focusable");
    }
    
    // Test 15: TabHost-like patterns (using TabLayout concepts)
    void TestTabHostPatterns()
    {
        // Test FrameLayout switching pattern (simplified TabHost)
        using var container = new FrameLayout(this);
        using var view1 = new TextView(this) { Text = "Tab1" };
        using var view2 = new TextView(this) { Text = "Tab2" };
        
        container.AddView(view1);
        container.AddView(view2);
        
        view1.Visibility = ViewStates.Visible;
        view2.Visibility = ViewStates.Gone;
        
        if (container.ChildCount != 2)
            throw new System.Exception($"Expected 2 children, got {container.ChildCount}");
    }
    
    // Test 16: Sensor callbacks
    void TestSensorCallbacks()
    {
        var sensorManager = (SensorManager?)GetSystemService(SensorService);
        if (sensorManager == null) throw new System.Exception("SensorManager is null");
        
        var accelerometer = sensorManager.GetDefaultSensor(SensorType.Accelerometer);
        // Accelerometer might not be available on all devices/emulators
        
        var listener = new TestSensorEventListener();
        // Just test listener creation, don't actually register
    }
    
    // Test 17: Network callback
    void TestNetworkCallback()
    {
        var connectivityManager = (ConnectivityManager?)GetSystemService(ConnectivityService);
        if (connectivityManager == null) throw new System.Exception("ConnectivityManager is null");
        
        var callback = new TestNetworkCallback();
        var request = new NetworkRequest.Builder()
            .AddCapability(NetCapability.Internet)
            .Build();
        
        // Just test creation, don't register (requires permissions)
        if (request == null) throw new System.Exception("NetworkRequest is null");
    }
    
    // Test 18: Custom Parcelable
    void TestCustomParcelable()
    {
        var original = new TestParcelable("test data", 42);
        
        using var parcel = Parcel.Obtain();
        if (parcel == null) throw new System.Exception("Parcel.Obtain returned null");
        
        original.WriteToParcel(parcel, ParcelableWriteFlags.None);
        parcel.SetDataPosition(0);
        
        // Read back
        var data = parcel.ReadString();
        var value = parcel.ReadInt();
        
        if (data != "test data" || value != 42)
            throw new System.Exception($"Parcel data mismatch: {data}, {value}");
    }
    
    // Test 19: Bundle with various object types
    void TestBundleWithObjects()
    {
        var bundle = new Bundle();
        bundle.PutString("string", "test");
        bundle.PutInt("int", 42);
        bundle.PutBoolean("bool", true);
        bundle.PutFloat("float", 3.14f);
        bundle.PutIntArray("intArray", new[] { 1, 2, 3 });
        
        if (bundle.GetString("string") != "test")
            throw new System.Exception("String mismatch");
        if (bundle.GetInt("int") != 42)
            throw new System.Exception("Int mismatch");
        
        var intArray = bundle.GetIntArray("intArray");
        if (intArray == null || intArray.Length != 3)
            throw new System.Exception("IntArray mismatch");
    }
    
    // Test 20: ClipboardManager
    void TestClipboardManager()
    {
        var clipboard = (Android.Content.ClipboardManager?)GetSystemService(ClipboardService);
        if (clipboard == null) throw new System.Exception("ClipboardManager is null");
        
        var clip = ClipData.NewPlainText("test", "test data");
        clipboard.PrimaryClip = clip;
        
        var retrieved = clipboard.PrimaryClip;
        if (retrieved == null || retrieved.ItemCount == 0)
            throw new System.Exception("Clipboard data not set");
    }
    
    // Test 21: VibrationEffect (API 26+)
    void TestVibrationEffect()
    {
        var vibrator = (Vibrator?)GetSystemService(VibratorService);
        if (vibrator == null) throw new System.Exception("Vibrator is null");
        
        // Create but don't vibrate
        var effect = VibrationEffect.CreateOneShot(100, VibrationEffect.DefaultAmplitude);
        if (effect == null) throw new System.Exception("VibrationEffect is null");
    }
    
    // Test 22: Notification builder
    void TestNotificationBuilder()
    {
        var channelId = "test_channel";
        var manager = (NotificationManager?)GetSystemService(NotificationService);
        if (manager == null) throw new System.Exception("NotificationManager is null");
        
        // Create channel (API 26+)
        var channel = new NotificationChannel(channelId, "Test", NotificationImportance.Default);
        manager.CreateNotificationChannel(channel);
        
        // Build notification
        var builder = new Notification.Builder(this, channelId)
            .SetContentTitle("Test")
            .SetContentText("Test notification")
            .SetSmallIcon(Android.Resource.Drawable.IcDialogInfo);
        
        var notification = builder.Build();
        if (notification == null) throw new System.Exception("Notification is null");
    }
    
    // Test 22: Primitive int array
    void TestPrimitiveIntArray()
    {
        // Create Java int array and pass to Bundle
        int[] intArray = { 1, 2, 3, 4, 5 };
        var bundle = new Bundle();
        bundle.PutIntArray("ints", intArray);
        
        // Read back
        var retrieved = bundle.GetIntArray("ints");
        if (retrieved == null || retrieved.Length != 5)
            throw new System.Exception($"Int array mismatch: {retrieved?.Length ?? -1}");
        if (retrieved[0] != 1 || retrieved[4] != 5)
            throw new System.Exception("Int array values mismatch");
        Log.Info("ComplexTest", $"Int array: [{string.Join(", ", retrieved)}]");
    }
    
    // Test 23: Primitive byte array
    void TestPrimitiveByteArray()
    {
        // String to bytes and back
        string original = "Hello NativeAOT!";
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(original);
        
        // Put in Bundle
        var bundle = new Bundle();
        bundle.PutByteArray("bytes", bytes);
        
        // Read back
        var retrieved = bundle.GetByteArray("bytes");
        if (retrieved == null || retrieved.Length != bytes.Length)
            throw new System.Exception("Byte array length mismatch");
        
        string decoded = System.Text.Encoding.UTF8.GetString(retrieved);
        if (decoded != original)
            throw new System.Exception($"Byte array value mismatch: {decoded}");
        Log.Info("ComplexTest", $"Byte array round-trip: {decoded}");
    }
    
    // Test 24: 2D int array (array of arrays)
    void Test2DIntArray()
    {
        // Create 2D array via Bundle (simpler than Java reflection)
        // Bundle supports int[][] via getSerializable
        int[][] array2d = new int[][] {
            new int[] { 0, 1, 2, 3 },
            new int[] { 4, 5, 6, 7 },
            new int[] { 8, 9, 10, 11 }
        };
        
        // Put into Bundle via Java ArrayList of int arrays
        var bundle = new Bundle();
        var arrayList = new Java.Util.ArrayList();
        foreach (var row in array2d) {
            // Wrap each int[] and add to ArrayList
            var intArray = new int[row.Length];
            System.Array.Copy(row, intArray, row.Length);
            bundle.PutIntArray($"row_{arrayList.Size()}", intArray);
            arrayList.Add(new Java.Lang.Integer(arrayList.Size()));
        }
        bundle.PutInt("rowCount", array2d.Length);
        
        // Read back
        int rowCount = bundle.GetInt("rowCount");
        if (rowCount != 3) throw new System.Exception($"Row count mismatch: {rowCount}");
        
        var row1 = bundle.GetIntArray("row_1");
        if (row1 == null || row1.Length != 4)
            throw new System.Exception($"Row 1 length mismatch");
        if (row1[2] != 6) throw new System.Exception($"2D array value mismatch: expected 6, got {row1[2]}");
        
        Log.Info("ComplexTest", $"2D array[1][2] = {row1[2]}");
    }
    
    // Test 25: Object array
    void TestObjectArray()
    {
        // Create Java Object array
        var strings = new Java.Lang.Object[] {
            new Java.Lang.String("First"),
            new Java.Lang.String("Second"),
            new Java.Lang.String("Third")
        };
        
        // Use ArrayList to store and retrieve
        var list = new Java.Util.ArrayList();
        foreach (var s in strings) {
            list.Add(s);
        }
        
        // Convert back to array
        var array = list.ToArray();
        if (array == null || array.Length != 3)
            throw new System.Exception($"Object array mismatch: {array?.Length ?? -1}");
        
        string? first = array[0]?.ToString();
        if (first != "First") throw new System.Exception($"First element mismatch: {first}");
        Log.Info("ComplexTest", $"Object array: {array.Length} elements, first={first}");
    }
    
    // Test 26: String array return from Java
    void TestStringArrayReturn()
    {
        // Get locale's available locales (returns String[] for display names)
        var locales = Java.Util.Locale.GetAvailableLocales();
        if (locales == null || locales.Length == 0)
            throw new System.Exception("No locales returned");
        
        // Get display names (returns strings)
        var displayName = locales[0]?.DisplayName;
        Log.Info("ComplexTest", $"First locale: {displayName}, total: {locales.Length}");
    }
    
    // Test 27: IDictionary marshalling
    void TestIDictionaryMarshalling()
    {
        // Create a HashMap and use it as IDictionary
        var hashMap = new Java.Util.HashMap();
        hashMap.Put(new Java.Lang.String("key1"), new Java.Lang.Integer(100));
        hashMap.Put(new Java.Lang.String("key2"), new Java.Lang.Integer(200));
        hashMap.Put(new Java.Lang.String("key3"), new Java.Lang.Integer(300));
        
        // Check size directly on HashMap
        int size = hashMap.Size();
        if (size != 3)
            throw new System.Exception($"HashMap size mismatch: {size}");
        
        // Get value back
        var value = hashMap.Get(new Java.Lang.String("key2"));
        if (value == null || ((Java.Lang.Integer)value).IntValue() != 200)
            throw new System.Exception("HashMap value mismatch");
        
        Log.Info("ComplexTest", $"HashMap: {size} entries, key2={value}");
    }
    
    // Test 28: Nested collections (List of Lists)
    void TestNestedCollections()
    {
        // Create ArrayList of ArrayLists
        var outer = new Java.Util.ArrayList();
        
        for (int i = 0; i < 3; i++) {
            var inner = new Java.Util.ArrayList();
            for (int j = 0; j < 4; j++) {
                inner.Add(new Java.Lang.Integer(i * 10 + j));
            }
            outer.Add(inner);
        }
        
        // Access nested element
        var innerList = (Java.Util.ArrayList?)outer.Get(1);
        if (innerList == null) throw new System.Exception("Inner list is null");
        
        var element = (Java.Lang.Integer?)innerList.Get(2);
        if (element == null || element.IntValue() != 12)
            throw new System.Exception($"Nested value mismatch: expected 12, got {element?.IntValue()}");
        
        Log.Info("ComplexTest", $"Nested list[1][2] = {element}");
    }
    
    // Test 29: Java enum marshalling
    void TestJavaEnumMarshalling()
    {
        // Test Thread.State enum
        var thread = new Java.Lang.Thread();
        var state = thread.GetState();
        if (state == null) throw new System.Exception("Thread state is null");
        
        Log.Info("ComplexTest", $"Thread state: {state.Name()}");
        
        // Test enum values() and valueOf()
        var states = Java.Lang.Thread.State.Values();
        if (states == null || states.Length == 0)
            throw new System.Exception("Thread.State.Values() returned empty");
        
        var newState = Java.Lang.Thread.State.ValueOf("NEW");
        if (newState == null) throw new System.Exception("ValueOf returned null");
        Log.Info("ComplexTest", $"Enum values: {states.Length}, valueOf(NEW)={newState.Name()}");
    }
    
    // Test 30: Exception marshalling (Java -> .NET)
    void TestExceptionMarshalling()
    {
        try {
            // This should throw NumberFormatException
            Java.Lang.Integer.ParseInt("not a number");
            throw new System.Exception("Should have thrown");
        } catch (Java.Lang.NumberFormatException ex) {
            Log.Info("ComplexTest", $"Caught Java exception: {ex.GetType().Name}");
        }
        
        // Test creating Java exception from .NET
        var javaEx = new Java.Lang.IllegalArgumentException("Test exception from .NET");
        if (javaEx.Message != "Test exception from .NET")
            throw new System.Exception("Exception message mismatch");
        Log.Info("ComplexTest", $"Created Java exception: {javaEx.Message}");
    }
    
    // Test 31: Java.Lang.Class usage
    void TestJavaLangClass()
    {
        // Get class objects via different methods
        var stringClass = Java.Lang.Class.FromType(typeof(Java.Lang.String));
        if (stringClass == null) throw new System.Exception("String class is null");
        
        var integerClass = Java.Lang.Class.ForName("java.lang.Integer");
        if (integerClass == null) throw new System.Exception("Integer class is null");
        
        // Check class properties
        var name = stringClass.Name;
        var simpleName = stringClass.SimpleName;
        Log.Info("ComplexTest", $"String class: {name}, simple: {simpleName}");
        
        // Check isAssignableFrom
        var objectClass = Java.Lang.Class.FromType(typeof(Java.Lang.Object));
        bool isAssignable = objectClass?.IsAssignableFrom(stringClass) ?? false;
        if (!isAssignable) throw new System.Exception("Object should be assignable from String");
        Log.Info("ComplexTest", $"IsAssignableFrom works: {isAssignable}");
    }
    
    // Test 32: Export attribute
    void TestExportAttribute()
    {
        // Create instance with [Export] method
        var exported = new TestExportedClass();
        
        // Call via Java reflection to test [Export] works
        var javaClass = exported.Class;
        var method = javaClass.GetMethod("exportedMethod", null);
        if (method == null) throw new System.Exception("Exported method not found");
        
        var result = method.Invoke(exported, null);
        if (result == null || ((Java.Lang.Integer)result).IntValue() != 42)
            throw new System.Exception("Exported method returned wrong value");
        
        Log.Info("ComplexTest", $"[Export] method works, returned: {result}");
    }
    
    // Test 33: Fragment lifecycle (basic)
    void TestFragmentLifecycle()
    {
        // We can't fully test fragments without FragmentManager, but we can test creation
        var fragment = new TestFragment();
        
        // Verify bundle arguments work
        var args = new Bundle();
        args.PutString("key", "value");
        fragment.Arguments = args;
        
        var retrievedArgs = fragment.Arguments;
        if (retrievedArgs == null || retrievedArgs.GetString("key") != "value")
            throw new System.Exception("Fragment arguments mismatch");
        
        Log.Info("ComplexTest", "Fragment created with arguments");
    }
    
    // Test 34: Cursor adapter pattern (similar to RecyclerView but simpler)
    void TestRecyclerViewAdapter()
    {
        // Test AbsListView.IMultiChoiceModeListener (callback interface pattern)
        var listener = new TestMultiChoiceModeListener();
        
        // Verify the listener can be created and callbacks are defined
        Log.Info("ComplexTest", "MultiChoiceModeListener created");
    }
}

// MediaPlayer listeners
public class TestOnCompletionListener : Java.Lang.Object, MediaPlayer.IOnCompletionListener
{
    public void OnCompletion(MediaPlayer? mp) => Log.Info("ComplexTest", "OnCompletion");
}

public class TestOnErrorListener : Java.Lang.Object, MediaPlayer.IOnErrorListener
{
    public bool OnError(MediaPlayer? mp, MediaError what, int extra)
    {
        Log.Error("ComplexTest", $"OnError: {what}");
        return true;
    }
}

public class TestOnPreparedListener : Java.Lang.Object, MediaPlayer.IOnPreparedListener
{
    public void OnPrepared(MediaPlayer? mp) => Log.Info("ComplexTest", "OnPrepared");
}

// JavaScript interface
public class TestJavaScriptInterface : Java.Lang.Object
{
    [JavascriptInterface]
    public void ShowToast(string message)
    {
        Log.Info("ComplexTest", $"JS called: {message}");
    }
}

// BaseAdapter subclass
public class TestBaseAdapter : BaseAdapter
{
    readonly Context _context;
    readonly string[] _items = { "A", "B", "C", "D", "E" };
    
    public TestBaseAdapter(Context context) => _context = context;
    
    public override int Count => _items.Length;
    
    public override Java.Lang.Object? GetItem(int position) => _items[position];
    
    public override long GetItemId(int position) => position;
    
    public override View? GetView(int position, View? convertView, ViewGroup? parent)
    {
        var textView = convertView as TextView ?? new TextView(_context);
        textView.Text = _items[position];
        return textView;
    }
}

// TextWatcher
public class TestTextWatcher : Java.Lang.Object, ITextWatcher
{
    public void AfterTextChanged(IEditable? s) 
    {
        Log.Info("ComplexTest", $"AfterTextChanged called, s={s?.GetType().Name ?? "null"}");
    }
    
    public void BeforeTextChanged(Java.Lang.ICharSequence? s, int start, int count, int after) 
    {
        Log.Info("ComplexTest", $"BeforeTextChanged called, s={s?.GetType().Name ?? "null"}, start={start}, count={count}, after={after}");
    }
    
    public void OnTextChanged(Java.Lang.ICharSequence? s, int start, int before, int count) 
    {
        Log.Info("ComplexTest", $"OnTextChanged called, s={s?.GetType().Name ?? "null"}, start={start}, before={before}, count={count}");
    }
}

// GestureDetector listener
public class TestGestureListener : Java.Lang.Object, GestureDetector.IOnGestureListener
{
    public bool OnDown(MotionEvent? e) => true;
    public bool OnFling(MotionEvent? e1, MotionEvent? e2, float velocityX, float velocityY) => false;
    public void OnLongPress(MotionEvent? e) { }
    public bool OnScroll(MotionEvent? e1, MotionEvent? e2, float distanceX, float distanceY) => false;
    public void OnShowPress(MotionEvent? e) { }
    public bool OnSingleTapUp(MotionEvent? e) => false;
}

// ScaleGestureDetector listener
public class TestScaleGestureListener : Java.Lang.Object, ScaleGestureDetector.IOnScaleGestureListener
{
    public bool OnScale(ScaleGestureDetector? detector) => false;
    public bool OnScaleBegin(ScaleGestureDetector? detector) => true;
    public void OnScaleEnd(ScaleGestureDetector? detector) { }
}

// SensorEventListener
public class TestSensorEventListener : Java.Lang.Object, ISensorEventListener
{
    public void OnAccuracyChanged(Sensor? sensor, SensorStatus accuracy) { }
    public void OnSensorChanged(SensorEvent? e) { }
}

// NetworkCallback
public class TestNetworkCallback : ConnectivityManager.NetworkCallback
{
    public override void OnAvailable(Network? network)
    {
        Log.Info("ComplexTest", "Network available");
    }
    
    public override void OnLost(Network? network)
    {
        Log.Info("ComplexTest", "Network lost");
    }
}

// Custom Parcelable
public class TestParcelable : Java.Lang.Object, IParcelable
{
    public string Data { get; }
    public int Value { get; }
    
    public TestParcelable(string data, int value)
    {
        Data = data;
        Value = value;
    }
    
    public int DescribeContents() => 0;
    
    public void WriteToParcel(Parcel? dest, ParcelableWriteFlags flags)
    {
        dest?.WriteString(Data);
        dest?.WriteInt(Value);
    }
}

// Class with [Export] attribute
public class TestExportedClass : Java.Lang.Object
{
    [Export("exportedMethod")]
    public int ExportedMethod()
    {
        Log.Info("ComplexTest", "Exported method called!");
        return 42;
    }
}

// Test Fragment
public class TestFragment : Android.App.Fragment
{
    public override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Log.Info("ComplexTest", "TestFragment.OnCreate");
    }
    
    public override View? OnCreateView(Android.Views.LayoutInflater? inflater, ViewGroup? container, Bundle? savedInstanceState)
    {
        return new TextView(Activity) { Text = "Test Fragment" };
    }
}

// MultiChoiceMode listener (complex callback interface pattern)
public class TestMultiChoiceModeListener : Java.Lang.Object, AbsListView.IMultiChoiceModeListener
{
    public bool OnActionItemClicked(ActionMode? mode, IMenuItem? item) => false;
    public bool OnCreateActionMode(ActionMode? mode, IMenu? menu) => true;
    public void OnDestroyActionMode(ActionMode? mode) { }
    public bool OnPrepareActionMode(ActionMode? mode, IMenu? menu) => false;
    public void OnItemCheckedStateChanged(ActionMode? mode, int position, long id, bool @checked) { }
}

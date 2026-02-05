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

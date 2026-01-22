- [X] TypeMapAttributeTypeMap
    - [X] initialize a static field `static TypeMapAttributeTypeMap s_TypeMap` during initialization so we don't need to do it in `GetFunctionPointer` over and over again and avoid any unnecessary null checks -- GetFunctionPointer is a hot path
    - [X] no reflection fallbacks, crash the app if necessary!
    - [X] try-catch might not be necessary. any exception throw here should crash the app anyway. if the individual `GetFunctionPointer` calls don't throw (we generate them), then let's just add enough logging to verify that the method DID NOT RETURN IntPtr.Zero. Also, let's avoid throwing in those methods and make sure they return ZERO when invalid index.
    - [X] TryCreateInstance is still using reflection, this should also be replaced with pre-generated factory methods so there is no need for unconstrained runtime reflection!
    - [X] I don't really like the `unsafe` in the ITypeMap interface, I suggest we move the _string_ marshalling to JNIEnvInit and make the interface accept `string`
- [X] JNIEnvInit
    - [X] let's add the UCO for `GetFunctionPointer` here and let it redirect to the typemap the same way we do it for PropageteUnhandledException. The `ITypeMap` interface should have a method called `GetFunctionPointer(...)` that this will just call on the global typemap. the TypeMapAttributeTypeMap won't have the UCO attribute and it won't be static anymore, good!
    - [X] we should init it anyway. in Mono, we would just ignore the out parameter and bind to the old way of doing things. we will use the same out fnptr for nativeaot too later.
    - [X] I thought we could directly set `get_function_pointer` value after `JNIEnvInit.Initialize` from the out param of args and there should really be no need for the `typemap_init` method. would that be viable? would it break some encapsluation?
- [X] GenerateTypeMapAttributesStep
    - [X] I would prefer merging bunch of WriteLine into a single one with `"""` C# string code blocks
    - [X] you said "When MainActivity loads, RegisterNativeMembers is called but does nothing." -- why is it called? do we call it from generated Java code? let's not please.
    - [X] I want to completely comment out the RegisterNativeMembers codepaths from the PoC. We MIGHT need it for some scenarios, but I'm not convinced yet.
    - [X] GenerateJcwJavaFile appears to be disabled - we MUST use custom Java codegen for 2 reasons:
        - we want to be completely independent of the existing codegen which might have unexpected sideeffects
        - we WANT to call constructors through java native methods with `get_function_pointer` resolution of a special UCO which calls the constructors, as described in the spec
    - [X] `xamarin_typemap_init` method should be renamed, let's not use the `xamarin` prefix in new code. also, why is this declared in LLVM IR and not in C++ code?
    - [X] implement "// TODO: Implement proper JI constructor support"
    - [X] if ctor for `GenerateCreateInstanceMethod` is not found, we should definitely have logging and possibly throw exception -- with the exception of STATIC classes, I don't think there's a scenario where there isn't a ctor? maybe we should still generate the method, but when called, it would throw an exception?
- [X] JavaPeerProxy
    - [X] do we need TargetType in JavaPeerProxy?
    - [X] if we need TargetType, does it need DynamicallyAccessedMembers? if not, drop it + drop it from the TypeMap class
- [ ] I see this runtime crash:
    ```
    --------- beginning of crash
    01-22 09:54:59.566 18667 18667 E AndroidRuntime: FATAL EXCEPTION: main
    01-22 09:54:59.566 18667 18667 E AndroidRuntime: Process: com.xamarin.android.helloworld, PID: 18667
    01-22 09:54:59.566 18667 18667 E AndroidRuntime: java.lang.UnsatisfiedLinkError: No implementation found for void mono.android.TypeManager.n_activate(java.lang.String, java.lang.String, java.lang.Object, java.lang.Object[]) (tried Java_mono_android_TypeManager_n_1activate and Java_mono_android_TypeManager_n_1activate__Ljava_lang_String_2Ljava_lang_String_2Ljava_lang_Object_2_3Ljava_lang_Object_2)
    01-22 09:54:59.566 18667 18667 E AndroidRuntime:        at mono.android.TypeManager.n_activate(Native Method)
    01-22 09:54:59.566 18667 18667 E AndroidRuntime:        at mono.android.TypeManager.Activate(TypeManager.java:7)
    01-22 09:54:59.566 18667 18667 E AndroidRuntime:        at example.MainActivity.<init>(MainActivity.java:23)
    01-22 09:54:59.566 18667 18667 E AndroidRuntime:        at java.lang.Class.newInstance(Native Method)
    01-22 09:54:59.566 18667 18667 E AndroidRuntime:        at android.app.AppComponentFactory.instantiateActivity(AppComponentFactory.java:95)
    01-22 09:54:59.566 18667 18667 E AndroidRuntime:        at android.app.Instrumentation.newActivity(Instrumentation.java:1339)
    01-22 09:54:59.566 18667 18667 E AndroidRuntime:        at android.app.ActivityThread.performLaunchActivity(ActivityThread.java:3538)
    01-22 09:54:59.566 18667 18667 E AndroidRuntime:        at android.app.ActivityThread.handleLaunchActivity(ActivityThread.java:3782)
    01-22 09:54:59.566 18667 18667 E AndroidRuntime:        at android.app.servertransaction.LaunchActivityItem.execute(LaunchActivityItem.java:101)
    01-22 09:54:59.566 18667 18667 E AndroidRuntime:        at android.app.servertransaction.TransactionExecutor.executeCallbacks(TransactionExecutor.java:138)
    01-22 09:54:59.566 18667 18667 E AndroidRuntime:        at android.app.servertransaction.TransactionExecutor.execute(TransactionExecutor.java:95)
    01-22 09:54:59.566 18667 18667 E AndroidRuntime:        at android.app.ActivityThread$H.handleMessage(ActivityThread.java:2307)
    01-22 09:54:59.566 18667 18667 E AndroidRuntime:        at android.os.Handler.dispatchMessage(Handler.java:106)
    01-22 09:54:59.566 18667 18667 E AndroidRuntime:        at android.os.Looper.loopOnce(Looper.java:201)
    01-22 09:54:59.566 18667 18667 E AndroidRuntime:        at android.os.Looper.loop(Looper.java:288)
    01-22 09:54:59.566 18667 18667 E AndroidRuntime:        at android.app.ActivityThread.main(ActivityThread.java:7924)
    01-22 09:54:59.566 18667 18667 E AndroidRuntime:        at java.lang.reflect.Method.invoke(Native Method)
    01-22 09:54:59.566 18667 18667 E AndroidRuntime:        at com.android.internal.os.RuntimeInit$MethodAndArgsCaller.run(RuntimeInit.java:548)
    01-22 09:54:59.566 18667 18667 E AndroidRuntime:        at com.android.internal.os.ZygoteInit.main(ZygoteInit.java:936)
    ```
    - it appears that the TypeManager.java class is included in the app even though we're using the new typemap. this type manager class is not compatible with our changes

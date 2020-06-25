#### Wrong AndroidManifest.xml packaged to the APK while using binding library

   * [GitHub 4812][0]: We were overwriting the AndroidManifest.xml file
    in the apk with ones from Support Libraries. This manifests itself
    with the following error

    ```
    Failed to parse APK info: failed to parse AndroidManifest.xml, error: %!s()
deploy failed, error: failed to get apk infos, output: W/ResourceType( 5266): Bad XML block:
    ```


 [0]: https://github.com/xamarin/xamarin-android/pull/4812
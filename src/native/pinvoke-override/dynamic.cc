#include <dlfcn.h>

#include "internal-pinvokes.hh"
#include "logger.hh"
#include "search.hh"
#include "timing.hh"
#include "timing-internal.hh"

#define PINVOKE_OVERRIDE_INLINE [[gnu::noinline]]
#include "pinvoke-override-api-impl.hh"

using namespace xamarin::android;
using namespace xamarin::android::internal;

// TODO: these should be shared with the "static" dispatch code.  They currently reside in `pinvoke-tables.include`
//       which is not used by the "dynamic" dispatch
#if INTPTR_MAX == INT64_MAX
constexpr hash_t java_interop_library_hash = 0x54568ec36068e6b6;
constexpr hash_t xa_internal_api_library_hash = 0x43fd1b21148361b2;
constexpr hash_t system_native_library_hash = 0x4cd7bd0032e920e1;
constexpr hash_t system_io_compression_native_library_hash = 0x9190f4cb761b1d3c;
constexpr hash_t system_security_cryptography_native_android_library_hash = 0x1848c0093f0afd8;
constexpr hash_t system_globalization_native_library_hash = 0x28b5c8fca080abd5;
#else
constexpr hash_t java_interop_library_hash = 0x6e36e350;
constexpr hash_t xa_internal_api_library_hash = 0x13c9bd62;
constexpr hash_t system_native_library_hash = 0x5b9ade60;
constexpr hash_t system_io_compression_native_library_hash = 0xafe3142c;
constexpr hash_t system_security_cryptography_native_android_library_hash = 0x93625cd;
constexpr hash_t system_globalization_native_library_hash = 0xa66f1e5a;
#endif

extern "C" {
	// Fake prototypes, just to get symbol names

	void GlobalizationNative_GetLocaleInfoInt          ();
	void GlobalizationNative_ToAscii                   ();
	void GlobalizationNative_GetSortKey                ();
	void GlobalizationNative_InitOrdinalCasingPage     ();
	void GlobalizationNative_GetCalendars              ();
	void GlobalizationNative_GetLocaleInfoString       ();
	void GlobalizationNative_GetICUVersion             ();
	void GlobalizationNative_StartsWith                ();
	void GlobalizationNative_GetLocaleName             ();
	void GlobalizationNative_IsNormalized              ();
	void GlobalizationNative_GetTimeZoneDisplayName    ();
	void GlobalizationNative_IndexOf                   ();
	void GlobalizationNative_NormalizeString           ();
	void GlobalizationNative_GetSortVersion            ();
	void GlobalizationNative_IanaIdToWindowsId         ();
	void GlobalizationNative_ToUnicode                 ();
	void GlobalizationNative_ChangeCaseTurkish         ();
	void GlobalizationNative_GetCalendarInfo           ();
	void GlobalizationNative_WindowsIdToIanaId         ();
	void GlobalizationNative_GetLocaleTimeFormat       ();
	void GlobalizationNative_GetLatestJapaneseEra      ();
	void GlobalizationNative_ChangeCase                ();
	void GlobalizationNative_EndsWith                  ();
	void GlobalizationNative_GetSortHandle             ();
	void GlobalizationNative_LoadICU                   ();
	void GlobalizationNative_CompareString             ();
	void GlobalizationNative_InitICUFunctions          ();
	void GlobalizationNative_IsPredefinedLocale        ();
	void GlobalizationNative_GetDefaultLocaleName      ();
	void GlobalizationNative_LastIndexOf               ();
	void GlobalizationNative_GetJapaneseEraStartDate   ();
	void GlobalizationNative_GetLocales                ();
	void GlobalizationNative_EnumCalendarInfo          ();
	void GlobalizationNative_GetLocaleInfoGroupingSizes();
	void GlobalizationNative_ChangeCaseInvariant       ();
	void GlobalizationNative_CloseSortHandle           ();

	void SystemNative_Bind                                          ();
	void SystemNative_TryGetIPPacketInformation                     ();
	void SystemNative_Receive                                       ();
	void SystemNative_Abort                                         ();
	void SystemNative_SetPosixSignalHandler                         ();
	void SystemNative_GetEstimatedTcpConnectionCount                ();
	void SystemNative_LockFileRegion                                ();
	void SystemNative_MSync                                         ();
	void SystemNative_INotifyInit                                   ();
	void SystemNative_GetUInt64OSThreadId                           ();
	void SystemNative_SetRLimit                                     ();
	void SystemNative_GetMaximumAddressSize                         ();
	void SystemNative_PathConf                                      ();
	void SystemNative_LowLevelMonitor_Acquire                       ();
	void SystemNative_Read                                          ();
	void SystemNative_SetSendTimeout                                ();
	void SystemNative_Dup                                           ();
	void SystemNative_GetEUid                                       ();
	void SystemNative_Log                                           ();
	void SystemNative_CreateThread                                  ();
	void SystemNative_SetTerminalInvalidationHandler                ();
	void SystemNative_FcntlSetPipeSz                                ();
	void SystemNative_SetSockOpt                                    ();
	void SystemNative_RealPath                                      ();
	void SystemNative_GetWindowSize                                 ();
	void SystemNative_MkDir                                         ();
	void SystemNative_CreateSocketEventPort                         ();
	void SystemNative_TryChangeSocketEventRegistration              ();
	void SystemNative_GetSystemTimeAsTicks                          ();
	void SystemNative_SNPrintF_1S                                   ();
	void SystemNative_LowLevelMonitor_TimedWait                     ();
	void SystemNative_ConfigureTerminalForChildProcess              ();
	void SystemNative_GetGroupName                                  ();
	void SystemNative_GetRawSockOpt                                 ();
	void SystemNative_FStat                                         ();
	void SystemNative_GetActiveTcpConnectionInfos                   ();
	void SystemNative_GetAddressFamily                              ();
	void SystemNative_CloseSocketEventPort                          ();
	void SystemNative_LChflagsCanSetHiddenFlag                      ();
	void SystemNative_ReadDirR                                      ();
	void SystemNative_FAllocate                                     ();
	void SystemNative_Connectx                                      ();
	void SystemNative_PReadV                                        ();
	void SystemNative_HandleNonCanceledPosixSignal                  ();
	void SystemNative_CanGetHiddenFlag                              ();
	void SystemNative_Close                                         ();
	void SystemNative_Pipe                                          ();
	void SystemNative_SendFile                                      ();
	void SystemNative_Stat                                          ();
	void SystemNative_GetIPv6Address                                ();
	void SystemNative_LowLevelMonitor_Wait                          ();
	void SystemNative_LStat                                         ();
	void SystemNative_ConvertErrorPalToPlatform                     ();
	void SystemNative_GetErrNo                                      ();
	void SystemNative_CreateSocketEventBuffer                       ();
	void SystemNative_INotifyAddWatch                               ();
	void SystemNative_GetIPv4Address                                ();
	void SystemNative_CreateNetworkChangeListenerSocket             ();
	void SystemNative_SchedGetAffinity                              ();
	void SystemNative_FLock                                         ();
	void SystemNative_AlignedRealloc                                ();
	void SystemNative_InitializeTerminalAndSignalHandling           ();
	void SystemNative_MProtect                                      ();
	void SystemNative_GetRLimit                                     ();
	void SystemNative_Unlink                                        ();
	void SystemNative_DrainAutoreleasePool                          ();
	void SystemNative_GetIPv6MulticastOption                        ();
	void SystemNative_LowLevelMonitor_Destroy                       ();
	void SystemNative_ShmUnlink                                     ();
	void SystemNative_GetSocketErrorOption                          ();
	void SystemNative_EnablePosixSignalHandling                     ();
	void SystemNative_StrErrorR                                     ();
	void SystemNative_RmDir                                         ();
	void SystemNative_SetIPv4MulticastOption                        ();
	void SystemNative_SNPrintF                                      ();
	void SystemNative_ReadLink                                      ();
	void SystemNative_Accept                                        ();
	void SystemNative_FChflags                                      ();
	void SystemNative_Disconnect                                    ();
	void SystemNative_SetEUid                                       ();
	void SystemNative_FUTimens                                      ();
	void SystemNative_GetLingerOption                               ();
	void SystemNative_FreeHostEntry                                 ();
	void SystemNative_GetFormatInfoForMountPoint                    ();
	void SystemNative_AlignedAlloc                                  ();
	void SystemNative_SysLog                                        ();
	void SystemNative_Write                                         ();
	void SystemNative_GetOSArchitecture                             ();
	void SystemNative_AlignedFree                                   ();
	void SystemNative_SetAddressFamily                              ();
	void SystemNative_SetIPv6Address                                ();
	void SystemNative_LowLevelMonitor_Create                        ();
	void SystemNative_GetUnixRelease                                ();
	void SystemNative_LChflags                                      ();
	void SystemNative_SetPriority                                   ();
	void SystemNative_GetSocketAddressSizes                         ();
	void SystemNative_GetPeerID                                     ();
	void SystemNative_GetUnixVersion                                ();
	void SystemNative_SysConf                                       ();
	void SystemNative_GetDeviceIdentifiers                          ();
	void SystemNative_GetProcessPath                                ();
	void SystemNative_SetDelayedSigChildConsoleConfigurationHandler ();
	void SystemNative_GetEnv                                        ();
	void SystemNative_GetActiveUdpListeners                         ();
	void SystemNative_InterfaceNameToIndex                          ();
	void SystemNative_FTruncate                                     ();
	void SystemNative_GetControlCharacters                          ();
	void SystemNative_GetPort                                       ();
	void SystemNative_Exit                                          ();
	void SystemNative_InitializeConsoleBeforeRead                   ();
	void SystemNative_GetReadDirRBufferSize                         ();
	void SystemNative_SchedSetAffinity                              ();
	void SystemNative_GetNativeIPInterfaceStatistics                ();
	void SystemNative_GetSignalForBreak                             ();
	void SystemNative_PWriteV                                       ();
	void SystemNative_FreeEnviron                                   ();
	void SystemNative_GetHostName                                   ();
	void SystemNative_FcntlSetFD                                    ();
	void SystemNative_Realloc                                       ();
	void SystemNative_PlatformSupportsDualModeIPv4PacketInfo        ();
	void SystemNative_GetSockOpt                                    ();
	void SystemNative_GetLoadLibraryError                           ();
	void SystemNative_Link                                          ();
	void SystemNative_FSync                                         ();
	void SystemNative_Malloc                                        ();
	void SystemNative_ReceiveSocketError                            ();
	void SystemNative_ReadStdin                                     ();
	void SystemNative_DisablePosixSignalHandling                    ();
	void SystemNative_Connect                                       ();
	void SystemNative_GetAllMountPoints                             ();
	void SystemNative_LoadLibrary                                   ();
	void SystemNative_GetTimeZoneData                               ();
	void SystemNative_GetDomainSocketSizes                          ();
	void SystemNative_MUnmap                                        ();
	void SystemNative_MkNod                                         ();
	void SystemNative_WaitForSocketEvents                           ();
	void SystemNative_GetPlatformSignalNumber                       ();
	void SystemNative_FcntlSetIsNonBlocking                         ();
	void SystemNative_SetKeypadXmit                                 ();
	void SystemNative_MAdvise                                       ();
	void SystemNative_MkdTemp                                       ();
	void SystemNative_FChMod                                        ();
	void SystemNative_OpenDir                                       ();
	void SystemNative_WaitPidExitedNoHang                           ();
	void SystemNative_FcntlGetPipeSz                                ();
	void SystemNative_GetTimestamp                                  ();
	void SystemNative_SchedGetCpu                                   ();
	void SystemNative_GetPwNamR                                     ();
	void SystemNative_ShmOpen                                       ();
	void SystemNative_GetSid                                        ();
	void SystemNative_MksTemps                                      ();
	void SystemNative_GetBytesAvailable                             ();
	void SystemNative_GetIPv4MulticastOption                        ();
	void SystemNative_SetSignalForBreak                             ();
	void SystemNative_FcntlCanGetSetPipeSz                          ();
	void SystemNative_MkFifo                                        ();
	void SystemNative_GetNetworkInterfaces                          ();
	void SystemNative_ChDir                                         ();
	void SystemNative_IsATty                                        ();
	void SystemNative_INotifyRemoveWatch                            ();
	void SystemNative_MMap                                          ();
	void SystemNative_GetNumRoutes                                  ();
	void SystemNative_GetGroups                                     ();
	void SystemNative_StdinReady                                    ();
	void SystemNative_GetCwd                                        ();
	void SystemNative_Shutdown                                      ();
	void SystemNative_GetCryptographicallySecureRandomBytes         ();
	void SystemNative_UTimensat                                     ();
	void SystemNative_CopyFile                                      ();
	void SystemNative_CloseDir                                      ();
	void SystemNative_Rename                                        ();
	void SystemNative_MapTcpState                                   ();
	void SystemNative_SetIPv6MulticastOption                        ();
	void SystemNative_GetHostEntryForName                           ();
	void SystemNative_Access                                        ();
	void SystemNative_GetGroupList                                  ();
	void SystemNative_LogError                                      ();
	void SystemNative_Socket                                        ();
	void SystemNative_GetSockName                                   ();
	void SystemNative_SetRawSockOpt                                 ();
	void SystemNative_ReadEvents                                    ();
	void SystemNative_GetPwUidR                                     ();
	void SystemNative_Sync                                          ();
	void SystemNative_Calloc                                        ();
	void SystemNative_GetBootTimeTicks                              ();
	void SystemNative_SymLink                                       ();
	void SystemNative_FcntlGetFD                                    ();
	void SystemNative_GetNameInfo                                   ();
	void SystemNative_Send                                          ();
	void SystemNative_EnumerateInterfaceAddresses                   ();
	void SystemNative_GetPriority                                   ();
	void SystemNative_iOSSupportVersion                             ();
	void SystemNative_LowLevelMonitor_Release                       ();
	void SystemNative_GetEGid                                       ();
	void SystemNative_GetSocketType                                 ();
	void SystemNative_RegisterForSigChld                            ();
	void SystemNative_GetCpuUtilization                             ();
	void SystemNative_ForkAndExecProcess                            ();
	void SystemNative_ChMod                                         ();
	void SystemNative_FreeSocketEventBuffer                         ();
	void SystemNative_UninitializeConsoleAfterRead                  ();
	void SystemNative_GetControlMessageBufferSize                   ();
	void SystemNative_ReceiveMessage                                ();
	void SystemNative_Kill                                          ();
	void SystemNative_GetEnviron                                    ();
	void SystemNative_SearchPath                                    ();
	void SystemNative_ConvertErrorPlatformToPal                     ();
	void SystemNative_LSeek                                         ();
	void SystemNative_SetPort                                       ();
	void SystemNative_GetDefaultTimeZone                            ();
	void SystemNative_PRead                                         ();
	void SystemNative_GetDomainName                                 ();
	void SystemNative_GetIPv4GlobalStatistics                       ();
	void SystemNative_GetIcmpv4GlobalStatistics                     ();
	void SystemNative_GetEstimatedUdpListenerCount                  ();
	void SystemNative_LowLevelMonitor_Signal_Release                ();
	void SystemNative_SetLingerOption                               ();
	void SystemNative_Open                                          ();
	void SystemNative_GetAtOutOfBandMark                            ();
	void SystemNative_ReadProcessStatusInfo                         ();
	void SystemNative_CreateAutoreleasePool                         ();
	void SystemNative_FcntlGetIsNonBlocking                         ();
	void SystemNative_SendMessage                                   ();
	void SystemNative_SetErrNo                                      ();
	void SystemNative_TryGetUInt32OSThreadId                        ();
	void SystemNative_Listen                                        ();
	void SystemNative_GetNonCryptographicallySecureRandomBytes      ();
	void SystemNative_SetIPv4Address                                ();
	void SystemNative_GetProcAddress                                ();
	void SystemNative_FreeLibrary                                   ();
	void SystemNative_PWrite                                        ();
	void SystemNative_SetReceiveTimeout                             ();
	void SystemNative_Poll                                          ();
	void SystemNative_GetTcpGlobalStatistics                        ();
	void SystemNative_GetDefaultSearchOrderPseudoHandle             ();
	void SystemNative_GetIcmpv6GlobalStatistics                     ();
	void SystemNative_SearchPath_TempDirectory                      ();
	void SystemNative_SNPrintF_1I                                   ();
	void SystemNative_EnumerateGatewayAddressesForInterface         ();
	void SystemNative_Free                                          ();
	void SystemNative_GetUdpGlobalStatistics                        ();
	void SystemNative_WaitIdAnyExitedNoHangNoWait                   ();
	void SystemNative_PosixFAdvise                                  ();
	void SystemNative_GetPid                                        ();
	void SystemNative_Sysctl                                        ();
	void SystemNative_GetPeerName                                   ();
	void SystemNative_GetSpaceInfoForMountPoint                     ();
	void SystemNative_GetFileSystemType                             ();

	void AndroidCryptoNative_EcKeyCreateByExplicitParameters              ();
    void AndroidCryptoNative_X509GetCertificateForPrivateKeyEntry         ();
    void AndroidCryptoNative_EcKeyUpRef                                   ();
    void AndroidCryptoNative_X509GetContentType                           ();
    void AndroidCryptoNative_Aes256Gcm                                    ();
    void AndroidCryptoNative_RsaSize                                      ();
    void AndroidCryptoNative_Aes256Ecb                                    ();
    void AndroidCryptoNative_SetRsaParameters                             ();
    void AndroidCryptoNative_Aes192Cbc                                    ();
    void AndroidCryptoNative_SSLGetSupportedProtocols                     ();
    void AndroidCryptoNative_EcKeyCreateByOid                             ();
    void AndroidCryptoNative_SSLStreamGetCipherSuite                      ();
    void AndroidCryptoNative_SSLStreamCreate                              ();
    void AndroidCryptoNative_EcDsaVerify                                  ();
    void AndroidCryptoNative_X509ChainGetCertificateCount                 ();
    void AndroidCryptoNative_CipherDestroy                                ();
    void AndroidCryptoNative_X509StoreDeleteEntry                         ();
    void AndroidCryptoNative_DsaSizeSignature                             ();
    void AndroidCryptoNative_SSLStreamVerifyHostname                      ();
    void AndroidCryptoNative_Aes128Cbc                                    ();
    void AndroidCryptoNative_ChaCha20Poly1305                             ();
    void AndroidCryptoNative_X509PublicKey                                ();
    void AndroidCryptoNative_EcKeyGetCurveName                            ();
    void AndroidCryptoNative_X509ChainDestroyContext                      ();
    void AndroidCryptoNative_X509ChainGetErrorCount                       ();
    void AndroidCryptoNative_SSLStreamGetProtocol                         ();
    void AndroidCryptoNative_CipherUpdateAAD                              ();
    void AndroidCryptoNative_X509StoreEnumerateCertificates               ();
    void AndroidCryptoNative_EcKeyDestroy                                 ();
    void AndroidCryptoNative_DsaSign                                      ();
    void AndroidCryptoNative_X509ChainCreateContext                       ();
    void AndroidCryptoNative_X509ChainBuild                               ();
    void AndroidCryptoNative_X509StoreRemoveCertificate                   ();
    void AndroidCryptoNative_SSLStreamRelease                             ();
    void AndroidCryptoNative_SSLStreamGetPeerCertificate                  ();
    void AndroidCryptoNative_RsaGenerateKeyEx                             ();
    void AndroidCryptoNative_SSLSupportsApplicationProtocolsConfiguration ();
    void AndroidCryptoNative_SSLStreamShutdown                            ();
    void AndroidCryptoNative_Des3Cbc                                      ();
    void AndroidCryptoNative_RsaPrivateDecrypt                            ();
    void AndroidCryptoNative_CipherFinalEx                                ();
    void AndroidCryptoNative_RsaUpRef                                     ();
    void AndroidCryptoNative_RsaCreate                                    ();
    void AndroidCryptoNative_Aes192Ccm                                    ();
    void AndroidCryptoNative_SSLStreamCreateWithCertificates              ();
    void AndroidCryptoNative_EcdhDeriveKey                                ();
    void AndroidCryptoNative_Aes128Ccm                                    ();
    void AndroidCryptoNative_DsaSignatureFieldSize                        ();
    void AndroidCryptoNative_Aes128Gcm                                    ();
    void AndroidCryptoNative_Aes256Cbc                                    ();
    void AndroidCryptoNative_DsaGenerateKey                               ();
    void AndroidCryptoNative_X509StoreOpenDefault                         ();
    void AndroidCryptoNative_X509Decode                                   ();
    void AndroidCryptoNative_Aes128Cfb8                                   ();
    void AndroidCryptoNative_SSLStreamSetTargetHost                       ();
    void AndroidCryptoNative_RsaSignPrimitive                             ();
    void AndroidCryptoNative_RegisterRemoteCertificateValidationCallback  ();
    void AndroidCryptoNative_Aes192Gcm                                    ();
    void AndroidCryptoNative_X509ChainValidate                            ();
    void AndroidCryptoNative_NewGlobalReference                           ();
    void AndroidCryptoNative_Aes256Cfb128                                 ();
    void AndroidCryptoNative_Aes256Ccm                                    ();
    void AndroidCryptoNative_X509StoreAddCertificateWithPrivateKey        ();
    void AndroidCryptoNative_SSLStreamSetApplicationProtocols             ();
    void AndroidCryptoNative_SSLStreamGetApplicationProtocol              ();
    void AndroidCryptoNative_DsaKeyCreateByExplicitParameters             ();
    void AndroidCryptoNative_CipherSetKeyAndIV                            ();
    void AndroidCryptoNative_Aes192Ecb                                    ();
    void AndroidCryptoNative_SSLStreamSetEnabledProtocols                 ();
    void AndroidCryptoNative_Des3Cfb8                                     ();
    void AndroidCryptoNative_X509ChainGetErrors                           ();
    void AndroidCryptoNative_DesEcb                                       ();
    void AndroidCryptoNative_Aes192Cfb8                                   ();
    void AndroidCryptoNative_SSLStreamCreateWithKeyStorePrivateKeyEntry   ();
    void AndroidCryptoNative_SSLStreamWrite                               ();
    void AndroidCryptoNative_Pbkdf2                                       ();
    void AndroidCryptoNative_GetRsaParameters                             ();
    void AndroidCryptoNative_EcDsaSign                                    ();
    void AndroidCryptoNative_AeadCipherFinalEx                            ();
    void AndroidCryptoNative_EcDsaSize                                    ();
    void AndroidCryptoNative_DesCfb8                                      ();
    void AndroidCryptoNative_GetECCurveParameters                         ();
    void AndroidCryptoNative_X509StoreAddCertificate                      ();
    void AndroidCryptoNative_SSLStreamHandshake                           ();
    void AndroidCryptoNative_Des3Cfb64                                    ();
    void AndroidCryptoNative_DsaSizeP                                     ();
    void AndroidCryptoNative_SSLStreamGetPeerCertificates                 ();
    void AndroidCryptoNative_X509Encode                                   ();
    void AndroidCryptoNative_X509ExportPkcs7                              ();
    void AndroidCryptoNative_GetECKeyParameters                           ();
    void AndroidCryptoNative_CipherCreatePartial                          ();
    void AndroidCryptoNative_SSLStreamRead                                ();
    void AndroidCryptoNative_X509DecodeCollection                         ();
    void AndroidCryptoNative_CipherIsSupported                            ();
    void AndroidCryptoNative_X509ChainGetCertificates                     ();
    void AndroidCryptoNative_Aes256Cfb8                                   ();
    void AndroidCryptoNative_CipherReset                                  ();
    void AndroidCryptoNative_DsaVerify                                    ();
    void AndroidCryptoNative_DesCbc                                       ();
    void AndroidCryptoNative_X509StoreGetPrivateKeyEntry                  ();
    void AndroidCryptoNative_RsaDestroy                                   ();
    void AndroidCryptoNative_Aes128Cfb128                                 ();
    void AndroidCryptoNative_CipherSetNonceLength                         ();
    void AndroidCryptoNative_SSLStreamIsLocalCertificateUsed              ();
    void AndroidCryptoNative_GetDsaParameters                             ();
    void AndroidCryptoNative_X509ChainSetCustomTrustStore                 ();
    void AndroidCryptoNative_EcKeyCreateByKeyParameters                   ();
    void AndroidCryptoNative_RsaPublicEncrypt                             ();
    void AndroidCryptoNative_CipherCtxSetPadding                          ();
    void AndroidCryptoNative_GetBigNumBytes                               ();
    void AndroidCryptoNative_DeleteGlobalReference                        ();
    void AndroidCryptoNative_BigNumToBinary                               ();
    void AndroidCryptoNative_Aes128Ecb                                    ();
    void AndroidCryptoNative_Des3Ecb                                      ();
    void AndroidCryptoNative_X509StoreContainsCertificate                 ();
    void AndroidCryptoNative_X509StoreEnumerateTrustedCertificates        ();
    void AndroidCryptoNative_CipherUpdate                                 ();
    void AndroidCryptoNative_CipherCreate                                 ();
    void AndroidCryptoNative_CipherSetTagLength                           ();
    void AndroidCryptoNative_X509IsKeyStorePrivateKeyEntry                ();
    void AndroidCryptoNative_DecodeRsaSubjectPublicKeyInfo                ();
    void AndroidCryptoNative_RsaVerificationPrimitive                     ();
    void AndroidCryptoNative_Aes192Cfb128                                 ();
    void AndroidCryptoNative_EcKeyGetSize                                 ();
    void AndroidCryptoNative_SSLStreamRequestClientAuthentication         ();
    void AndroidCryptoNative_SSLStreamInitialize                          ();

	void CompressionNative_Crc32         ();
    void CompressionNative_Inflate       ();
    void CompressionNative_DeflateEnd    ();
    void CompressionNative_Deflate       ();
    void CompressionNative_InflateEnd    ();
    void CompressionNative_InflateInit2_ ();
    void CompressionNative_DeflateInit2_ ();
}

namespace {
#if INTPTR_MAX == INT64_MAX
	std::array<PinvokeEntry, 49> internal_pinvokes {{
		{0x452e23128e42f0a, "monodroid_get_log_categories", reinterpret_cast<void*>(&monodroid_get_log_categories)},
		{0xa50ce5de13bf8b5, "_monodroid_timezone_get_default_id", reinterpret_cast<void*>(&_monodroid_timezone_get_default_id)},
		{0x19055d65edfd668e, "_monodroid_get_network_interface_up_state", reinterpret_cast<void*>(&_monodroid_get_network_interface_up_state)},
		{0x2b3b0ca1d14076da, "monodroid_get_dylib", reinterpret_cast<void*>(&monodroid_get_dylib)},
		{0x2fbe68718cf2510d, "_monodroid_get_identity_hash_code", reinterpret_cast<void*>(&_monodroid_get_identity_hash_code)},
		{0x3ade4348ac8ce0fa, "_monodroid_freeifaddrs", reinterpret_cast<void*>(&_monodroid_freeifaddrs)},
		{0x3b2467e7eadd4a6a, "_monodroid_lref_log_new", reinterpret_cast<void*>(&_monodroid_lref_log_new)},
		{0x3b8097af56b5361f, "monodroid_log_traces", reinterpret_cast<void*>(&monodroid_log_traces)},
		{0x3c5532ecdab53f89, "set_world_accessable", reinterpret_cast<void*>(&set_world_accessable)},
		{0x423c8f539a2c56d2, "_monodroid_lookup_replacement_type", reinterpret_cast<void*>(&_monodroid_lookup_replacement_type)},
		{0x4b1956138764939a, "_monodroid_gref_log_new", reinterpret_cast<void*>(&_monodroid_gref_log_new)},
		{0x4d5b5b488f736058, "path_combine", reinterpret_cast<void*>(&path_combine)},
		{0x5a2614d15e2fdc2e, "monodroid_strdup_printf", reinterpret_cast<void*>(&monodroid_strdup_printf)},
		{0x5f0b4e426eff086b, "_monodroid_detect_cpu_and_architecture", reinterpret_cast<void*>(&_monodroid_detect_cpu_and_architecture)},
		{0x709af13cbfbe2e75, "monodroid_clear_gdb_wait", reinterpret_cast<void*>(&monodroid_clear_gdb_wait)},
		{0x70ae32c9a4f1ad2c, "monodroid_strsplit", reinterpret_cast<void*>(&monodroid_strsplit)},
		{0x70fc9bab8d56666d, "create_public_directory", reinterpret_cast<void*>(&create_public_directory)},
		{0x78514771a67ad724, "monodroid_strfreev", reinterpret_cast<void*>(&monodroid_strfreev)},
		{0x9099a4b95e3c3a89, "_monodroid_lref_log_delete", reinterpret_cast<void*>(&_monodroid_lref_log_delete)},
		{0x958cdb6fd9d1b67b, "monodroid_dylib_mono_new", reinterpret_cast<void*>(&monodroid_dylib_mono_new)},
		{0xa6ec846592d99536, "_monodroid_weak_gref_delete", reinterpret_cast<void*>(&_monodroid_weak_gref_delete)},
		{0xa7f58f3ee428cc6b, "_monodroid_gref_log_delete", reinterpret_cast<void*>(&_monodroid_gref_log_delete)},
		{0xae3df96dda0143bd, "_monodroid_gref_log", reinterpret_cast<void*>(&_monodroid_gref_log)},
		{0xb6222d90af401865, "_monodroid_weak_gref_get", reinterpret_cast<void*>(&_monodroid_weak_gref_get)},
		{0xb8306f71b963cd3d, "monodroid_log", reinterpret_cast<void*>(&monodroid_log)},
		{0xbc90bafd5ff9c99e, "_monodroid_get_dns_servers", reinterpret_cast<void*>(&_monodroid_get_dns_servers)},
		{0xbe5a300beec69c35, "monodroid_get_system_property", reinterpret_cast<void*>(&monodroid_get_system_property)},
		{0xbfbb924fbe190616, "monodroid_dylib_mono_free", reinterpret_cast<void*>(&monodroid_dylib_mono_free)},
		{0xc2a21d3f6c8ccc24, "_monodroid_lookup_replacement_method_info", reinterpret_cast<void*>(&_monodroid_lookup_replacement_method_info)},
		{0xc5b4690e13898fa3, "monodroid_timing_start", reinterpret_cast<void*>(&monodroid_timing_start)},
		{0xcc873ea8493d1dd5, "monodroid_embedded_assemblies_set_assemblies_prefix", reinterpret_cast<void*>(&monodroid_embedded_assemblies_set_assemblies_prefix)},
		{0xce439cfbe29dec11, "_monodroid_get_android_api_level", reinterpret_cast<void*>(&_monodroid_get_android_api_level)},
		{0xd1e121b94ea63f2e, "_monodroid_gref_get", reinterpret_cast<void*>(&_monodroid_gref_get)},
		{0xd5151b00eb33d85e, "monodroid_TypeManager_get_java_class_name", reinterpret_cast<void*>(&monodroid_TypeManager_get_java_class_name)},
		{0xda517ef392b6a888, "java_interop_free", reinterpret_cast<void*>(&java_interop_free)},
		{0xe27b9849b7e982cb, "_monodroid_max_gref_get", reinterpret_cast<void*>(&_monodroid_max_gref_get)},
		{0xe370a0d91cd63bc0, "_monodroid_getifaddrs", reinterpret_cast<void*>(&_monodroid_getifaddrs)},
		{0xe78f1161604ae672, "send_uninterrupted", reinterpret_cast<void*>(&send_uninterrupted)},
		{0xe86307aac9a2631a, "_monodroid_weak_gref_new", reinterpret_cast<void*>(&_monodroid_weak_gref_new)},
		{0xebc2c68e10075cc9, "monodroid_fopen", reinterpret_cast<void*>(&monodroid_fopen)},
		{0xee83e38e479aeff1, "_monodroid_counters_dump", reinterpret_cast<void*>(&_monodroid_counters_dump)},
		{0xf3048baf83034541, "_monodroid_gc_wait_for_bridge_processing", reinterpret_cast<void*>(&_monodroid_gc_wait_for_bridge_processing)},
		{0xf41c48df6f9be476, "monodroid_free", reinterpret_cast<void*>(&monodroid_free)},
		{0xf5a918ef520db207, "monodroid_timing_stop", reinterpret_cast<void*>(&monodroid_timing_stop)},
		{0xf5ed87b004005892, "_monodroid_get_network_interface_supports_multicast", reinterpret_cast<void*>(&_monodroid_get_network_interface_supports_multicast)},
		{0xf8798f762db15bba, "recv_uninterrupted", reinterpret_cast<void*>(&recv_uninterrupted)},
		{0xfa90326712e7e7c4, "java_interop_strdup", reinterpret_cast<void*>(&java_interop_strdup)},
		{0xfdc17c4ea8335ffd, "monodroid_get_namespaced_system_property", reinterpret_cast<void*>(&monodroid_get_namespaced_system_property)},
		{0xff010b3140f54d3f, "monodroid_dylib_mono_init", reinterpret_cast<void*>(&monodroid_dylib_mono_init)},
	}};

	std::array<PinvokeEntry, 249> system_native_pinvokes {{
    	{0xb38afc8bfe830b, "SystemNative_Bind", nullptr},
    	{0x190fe65d8736dcb, "SystemNative_TryGetIPPacketInformation", nullptr},
    	{0x1c8b86562ad5772, "SystemNative_Receive", nullptr},
    	{0x202543f28ecaf06, "SystemNative_Abort", nullptr},
    	{0x25abeafa88904a2, "SystemNative_SetPosixSignalHandler", nullptr},
    	{0x33158212a812caf, "SystemNative_GetEstimatedTcpConnectionCount", nullptr},
    	{0x3511e36d0a6c1b5, "SystemNative_LockFileRegion", nullptr},
    	{0x37b9dd562235e42, "SystemNative_MSync", nullptr},
    	{0x3a5df4793dd3230, "SystemNative_INotifyInit", nullptr},
    	{0x3d24547fa4fc31b, "SystemNative_GetUInt64OSThreadId", nullptr},
    	{0x581df5b0a00c422, "SystemNative_SetRLimit", nullptr},
    	{0x5b5ab451ff38f8e, "SystemNative_GetMaximumAddressSize", nullptr},
    	{0x6861b5336291d12, "SystemNative_PathConf", nullptr},
    	{0x6a1f4deffa02c30, "SystemNative_LowLevelMonitor_Acquire", nullptr},
    	{0x7ce8a9b967dd269, "SystemNative_Read", nullptr},
    	{0x8352ae4bba2b83b, "SystemNative_SetSendTimeout", nullptr},
    	{0x98bd27a7461321d, "SystemNative_Dup", nullptr},
    	{0xa906c14ca5834bc, "SystemNative_GetEUid", nullptr},
    	{0xac9f9c1abb62a92, "SystemNative_Log", nullptr},
    	{0xadb2441bcfcdfe9, "SystemNative_CreateThread", nullptr},
    	{0xafbf5c69d1badc0, "SystemNative_SetTerminalInvalidationHandler", nullptr},
    	{0xba897b7abe67b16, "SystemNative_FcntlSetPipeSz", nullptr},
    	{0xc305c22ce7ab8a0, "SystemNative_SetSockOpt", nullptr},
    	{0xc79e924361c15ca, "SystemNative_RealPath", nullptr},
    	{0xef8dd67e25bac53, "SystemNative_GetWindowSize", nullptr},
    	{0xfa0899cf8d00a87, "SystemNative_MkDir", nullptr},
    	{0xfe7079441ac127e, "SystemNative_CreateSocketEventPort", nullptr},
    	{0x10d733abd1fd94bb, "SystemNative_TryChangeSocketEventRegistration", nullptr},
    	{0x114b8384553f5418, "SystemNative_GetSystemTimeAsTicks", nullptr},
    	{0x119a38c3e288a233, "SystemNative_SNPrintF_1S", nullptr},
    	{0x11b6f4f0aafeda95, "SystemNative_LowLevelMonitor_TimedWait", nullptr},
    	{0x11cc73f2926d4064, "SystemNative_ConfigureTerminalForChildProcess", nullptr},
    	{0x121bc483ac26f5f8, "SystemNative_GetGroupName", nullptr},
    	{0x12d65f9f65b01497, "SystemNative_GetRawSockOpt", nullptr},
    	{0x12eaf09505dc19fd, "SystemNative_FStat", nullptr},
    	{0x13577369f5ec4b0a, "SystemNative_GetActiveTcpConnectionInfos", nullptr},
    	{0x1399413d8a7d9dd8, "SystemNative_GetAddressFamily", nullptr},
    	{0x13a1c2de7fb2519f, "SystemNative_CloseSocketEventPort", nullptr},
    	{0x146cd1dc4fb2ba58, "SystemNative_LChflagsCanSetHiddenFlag", nullptr},
    	{0x176e22ea7c580dae, "SystemNative_ReadDirR", nullptr},
    	{0x185f5d25252c3c72, "SystemNative_FAllocate", nullptr},
    	{0x18d6b5e9fec9b0dc, "SystemNative_Connectx", nullptr},
    	{0x18f7da5f584b5b59, "SystemNative_PReadV", nullptr},
    	{0x1948a0cf88329c2f, "SystemNative_HandleNonCanceledPosixSignal", nullptr},
    	{0x1ac95b02f23933cc, "SystemNative_CanGetHiddenFlag", nullptr},
    	{0x1d4dcbc06728e689, "SystemNative_Close", nullptr},
    	{0x1d6d4278ffbbab77, "SystemNative_Pipe", nullptr},
    	{0x1d8d6a688fc5bfb3, "SystemNative_SendFile", nullptr},
    	{0x1f1c61a157636aad, "SystemNative_Stat", nullptr},
    	{0x1f849e45a3014a9f, "SystemNative_GetIPv6Address", nullptr},
    	{0x1f9361fc7b624c1b, "SystemNative_LowLevelMonitor_Wait", nullptr},
    	{0x2291e0ba4e1b55b0, "SystemNative_LStat", nullptr},
    	{0x24f840f903a26ded, "SystemNative_ConvertErrorPalToPlatform", nullptr},
    	{0x24ff74e427d0626e, "SystemNative_GetErrNo", nullptr},
    	{0x254905036a0061cf, "SystemNative_CreateSocketEventBuffer", nullptr},
    	{0x255c4a2e297fd9f5, "SystemNative_INotifyAddWatch", nullptr},
    	{0x27f3d9266af2b315, "SystemNative_GetIPv4Address", nullptr},
    	{0x2925953889c48cab, "SystemNative_CreateNetworkChangeListenerSocket", nullptr},
    	{0x2a49948ae20571cb, "SystemNative_SchedGetAffinity", nullptr},
    	{0x2c8da1192c5d7d2b, "SystemNative_FLock", nullptr},
    	{0x2d64b1ac218cf29e, "SystemNative_AlignedRealloc", nullptr},
    	{0x2e429d96a9fc92bd, "SystemNative_InitializeTerminalAndSignalHandling", nullptr},
    	{0x301c465c1ac0adf9, "SystemNative_MProtect", nullptr},
    	{0x3319a5483b3cc1fc, "SystemNative_GetRLimit", nullptr},
    	{0x3424ffcb69ecef57, "SystemNative_Unlink", nullptr},
    	{0x346a9bb11364833c, "SystemNative_DrainAutoreleasePool", nullptr},
    	{0x35169e67cc0f8529, "SystemNative_GetIPv6MulticastOption", nullptr},
    	{0x359205b4a10fa780, "SystemNative_LowLevelMonitor_Destroy", nullptr},
    	{0x36128eed665b1923, "SystemNative_ShmUnlink", nullptr},
    	{0x364dcf65ae63adff, "SystemNative_GetSocketErrorOption", nullptr},
    	{0x3757b327944abb54, "SystemNative_EnablePosixSignalHandling", nullptr},
    	{0x38b4bd21127ceffd, "SystemNative_StrErrorR", nullptr},
    	{0x38c7de719e8ae69d, "SystemNative_RmDir", nullptr},
    	{0x391bbbb9bbde4455, "SystemNative_SetIPv4MulticastOption", nullptr},
    	{0x3a7245f3ea476bf7, "SystemNative_SNPrintF", nullptr},
    	{0x3ae92e4198427b0d, "SystemNative_ReadLink", nullptr},
    	{0x3e0de839e6cfa6e5, "SystemNative_Accept", nullptr},
    	{0x3e7cf9a4789a31c7, "SystemNative_FChflags", nullptr},
    	{0x3f49b6278f04ae84, "SystemNative_Disconnect", nullptr},
    	{0x3fba15600bf0f229, "SystemNative_SetEUid", nullptr},
    	{0x41c1f2c9153639af, "SystemNative_FUTimens", nullptr},
    	{0x42339dd2717504d9, "SystemNative_GetLingerOption", nullptr},
    	{0x42783107bf2935ec, "SystemNative_FreeHostEntry", nullptr},
    	{0x42eb0578a9d62b78, "SystemNative_GetFormatInfoForMountPoint", nullptr},
    	{0x44ccb27979f980ce, "SystemNative_AlignedAlloc", nullptr},
    	{0x44f1a5c46033eec2, "SystemNative_SysLog", nullptr},
    	{0x483b434d7b089c7e, "SystemNative_Write", nullptr},
    	{0x484a3a445bdb14fc, "SystemNative_GetOSArchitecture", nullptr},
    	{0x4909639a9d87bdb5, "SystemNative_AlignedFree", nullptr},
    	{0x49e3ba95feb79c6c, "SystemNative_SetAddressFamily", nullptr},
    	{0x4b00795bbeea6f60, "SystemNative_SetIPv6Address", nullptr},
    	{0x4be7ceca50f3298c, "SystemNative_LowLevelMonitor_Create", nullptr},
    	{0x4bec4a1d7dfd4cf7, "SystemNative_GetUnixRelease", nullptr},
    	{0x4bfff22801b209ca, "SystemNative_LChflags", nullptr},
    	{0x4c22cc4f2b1dab26, "SystemNative_SetPriority", nullptr},
    	{0x509ff12da4e77259, "SystemNative_GetSocketAddressSizes", nullptr},
    	{0x523240c01d14ad50, "SystemNative_GetPeerID", nullptr},
    	{0x52794f1118d32f08, "SystemNative_GetUnixVersion", nullptr},
    	{0x5381564d2c06c0a3, "SystemNative_SysConf", nullptr},
    	{0x556bc89d2d4dfc85, "SystemNative_GetDeviceIdentifiers", nullptr},
    	{0x5592a052ceb4caf6, "SystemNative_GetProcessPath", nullptr},
    	{0x55fe2620f63d83d8, "SystemNative_SetDelayedSigChildConsoleConfigurationHandler", nullptr},
    	{0x574d77a68ec3e488, "SystemNative_GetEnv", nullptr},
    	{0x583db0344a1cd715, "SystemNative_GetActiveUdpListeners", nullptr},
    	{0x5908581fe73717f0, "SystemNative_InterfaceNameToIndex", nullptr},
    	{0x5a305cf2a314d6a6, "SystemNative_FTruncate", nullptr},
    	{0x5e53b688fede3216, "SystemNative_GetControlCharacters", nullptr},
    	{0x5fa62856bdbba9c0, "SystemNative_GetPort", nullptr},
    	{0x600b4418896f7808, "SystemNative_Exit", nullptr},
    	{0x6089f0c8112eb3d9, "SystemNative_InitializeConsoleBeforeRead", nullptr},
    	{0x613307e537d462db, "SystemNative_GetReadDirRBufferSize", nullptr},
    	{0x61bacd7170fd8c9b, "SystemNative_SchedSetAffinity", nullptr},
    	{0x61f3ce1b18b20d6f, "SystemNative_GetNativeIPInterfaceStatistics", nullptr},
    	{0x62351df42d842942, "SystemNative_GetSignalForBreak", nullptr},
    	{0x6393d30aceaa6df2, "SystemNative_PWriteV", nullptr},
    	{0x6448f0806bd3a338, "SystemNative_FreeEnviron", nullptr},
    	{0x650eddee76c6b8da, "SystemNative_GetHostName", nullptr},
    	{0x652badfba5d61929, "SystemNative_FcntlSetFD", nullptr},
    	{0x67d2cd86792b1d0c, "SystemNative_Realloc", nullptr},
    	{0x67e9d60481f4be06, "SystemNative_PlatformSupportsDualModeIPv4PacketInfo", nullptr},
    	{0x68df81a8fb5bf442, "SystemNative_GetSockOpt", nullptr},
    	{0x68f3fe6083c0355b, "SystemNative_GetLoadLibraryError", nullptr},
    	{0x69ad99fac0467f64, "SystemNative_Link", nullptr},
    	{0x6b9097385aa77917, "SystemNative_FSync", nullptr},
    	{0x6b9bce16ba8e845f, "SystemNative_Malloc", nullptr},
    	{0x6bc18fbbbf267e2a, "SystemNative_ReceiveSocketError", nullptr},
    	{0x6e2c1caff08e6e2d, "SystemNative_ReadStdin", nullptr},
    	{0x6ee05d5e8650e56c, "SystemNative_DisablePosixSignalHandling", nullptr},
    	{0x729afe37cdb8ae8f, "SystemNative_Connect", nullptr},
    	{0x730ae9a7469a7321, "SystemNative_GetAllMountPoints", nullptr},
    	{0x742da00b2dbf435d, "SystemNative_LoadLibrary", nullptr},
    	{0x7559feb379d38da5, "SystemNative_GetTimeZoneData", nullptr},
    	{0x7d7ee4bce74d4de9, "SystemNative_GetDomainSocketSizes", nullptr},
    	{0x7e1766c6df3ad261, "SystemNative_MUnmap", nullptr},
    	{0x7e4bdf46d4ff9f11, "SystemNative_MkNod", nullptr},
    	{0x7ec328b6ba9eab8a, "SystemNative_WaitForSocketEvents", nullptr},
    	{0x84c8a7489b37fea0, "SystemNative_GetPlatformSignalNumber", nullptr},
    	{0x8502eeba98158e79, "SystemNative_FcntlSetIsNonBlocking", nullptr},
    	{0x8530d37777969db6, "SystemNative_SetKeypadXmit", nullptr},
    	{0x85d0033bc38bb4bb, "SystemNative_MAdvise", nullptr},
    	{0x889350f209555ecb, "SystemNative_MkdTemp", nullptr},
    	{0x88a08b60b80c70cc, "SystemNative_FChMod", nullptr},
    	{0x8bcabce135063bed, "SystemNative_OpenDir", nullptr},
    	{0x8df448aee6e8fa5e, "SystemNative_WaitPidExitedNoHang", nullptr},
    	{0x8e96cb02418947cc, "SystemNative_FcntlGetPipeSz", nullptr},
    	{0x8fb6ed14ee0256bc, "SystemNative_GetTimestamp", nullptr},
    	{0x8ffe2d950d138c01, "SystemNative_SchedGetCpu", nullptr},
    	{0x93a8bec488055608, "SystemNative_GetPwNamR", nullptr},
    	{0x95a4cb8563cc6b14, "SystemNative_ShmOpen", nullptr},
    	{0x9856fa59ed936b73, "SystemNative_GetSid", nullptr},
    	{0x996ada1c038aabba, "SystemNative_MksTemps", nullptr},
    	{0x99a840c495204202, "SystemNative_GetBytesAvailable", nullptr},
    	{0x9aa9eaee3dd8b23b, "SystemNative_GetIPv4MulticastOption", nullptr},
    	{0x9aaaad33b28af82f, "SystemNative_SetSignalForBreak", nullptr},
    	{0x9c3e8b890033819a, "SystemNative_FcntlCanGetSetPipeSz", nullptr},
    	{0x9c832cd7fcbf2de0, "SystemNative_MkFifo", nullptr},
    	{0x9d2cb31282abd3d9, "SystemNative_GetNetworkInterfaces", nullptr},
    	{0x9e25ebf4f61cc299, "SystemNative_ChDir", nullptr},
    	{0x9fb01da1222e905a, "SystemNative_IsATty", nullptr},
    	{0xa1e881a63614507e, "SystemNative_INotifyRemoveWatch", nullptr},
    	{0xa2254fea4d8b6909, "SystemNative_MMap", nullptr},
    	{0xa2d7790a850024c0, "SystemNative_GetNumRoutes", nullptr},
    	{0xa302613a430248b8, "SystemNative_GetGroups", nullptr},
    	{0xa56532a23755cd87, "SystemNative_StdinReady", nullptr},
    	{0xa89ec9958d999483, "SystemNative_GetCwd", nullptr},
    	{0xa8bdc3e7ee898dfc, "SystemNative_Shutdown", nullptr},
    	{0xabdcf2f74d210f35, "SystemNative_GetCryptographicallySecureRandomBytes", nullptr},
    	{0xac11eab9d9c31b01, "SystemNative_UTimensat", nullptr},
    	{0xac7725c652a5fb5b, "SystemNative_CopyFile", nullptr},
    	{0xad228cdc4edb11d6, "SystemNative_CloseDir", nullptr},
    	{0xadc6889903a2d6f4, "SystemNative_Rename", nullptr},
    	{0xae320903718eb45d, "SystemNative_MapTcpState", nullptr},
    	{0xaf9706efc72c3904, "SystemNative_SetIPv6MulticastOption", nullptr},
    	{0xafd9f6338cdbadd4, "SystemNative_GetHostEntryForName", nullptr},
    	{0xb0b66a7145de350d, "SystemNative_Access", nullptr},
    	{0xb0e18377ed603e0b, "SystemNative_GetGroupList", nullptr},
    	{0xb361006446f560e8, "SystemNative_LogError", nullptr},
    	{0xb600c44028c1743d, "SystemNative_Socket", nullptr},
    	{0xb632e9bc6f7be0a9, "SystemNative_GetSockName", nullptr},
    	{0xb6540b73eff28747, "SystemNative_SetRawSockOpt", nullptr},
    	{0xb6ab9abf7887911f, "SystemNative_ReadEvents", nullptr},
    	{0xb73c597de01bc0b2, "SystemNative_GetPwUidR", nullptr},
    	{0xb78af5975603cd20, "SystemNative_Sync", nullptr},
    	{0xb7bbbe2c16a565c6, "SystemNative_Calloc", nullptr},
    	{0xbb3343826d504870, "SystemNative_GetBootTimeTicks", nullptr},
    	{0xbb5e970ecb6745da, "SystemNative_SymLink", nullptr},
    	{0xbbd20cce92ec2c12, "SystemNative_FcntlGetFD", nullptr},
    	{0xbcd9e53d2d288094, "SystemNative_GetNameInfo", nullptr},
    	{0xbd89ef4df5486744, "SystemNative_Send", nullptr},
    	{0xbdd3128e77381b01, "SystemNative_EnumerateInterfaceAddresses", nullptr},
    	{0xc00ebc097b776c1f, "SystemNative_GetPriority", nullptr},
    	{0xc036b23d88fad91b, "SystemNative_iOSSupportVersion", nullptr},
    	{0xc1c679eefc134d31, "SystemNative_LowLevelMonitor_Release", nullptr},
    	{0xc3c10021b10ba455, "SystemNative_GetEGid", nullptr},
    	{0xc3fe9394fe1f3f02, "SystemNative_GetSocketType", nullptr},
    	{0xc560d9947ab2a34d, "SystemNative_RegisterForSigChld", nullptr},
    	{0xc5bed971846027de, "SystemNative_GetCpuUtilization", nullptr},
    	{0xc69433678dd341ca, "SystemNative_ForkAndExecProcess", nullptr},
    	{0xc7ae1b8d93af5d73, "SystemNative_ChMod", nullptr},
    	{0xc7d536c0e7eb3fe2, "SystemNative_FreeSocketEventBuffer", nullptr},
    	{0xc87a5ee4869035c6, "SystemNative_UninitializeConsoleAfterRead", nullptr},
    	{0xc93df58ae5457bfd, "SystemNative_GetControlMessageBufferSize", nullptr},
    	{0xc956e528f995739c, "SystemNative_ReceiveMessage", nullptr},
    	{0xcaae6d345ba32c7b, "SystemNative_Kill", nullptr},
    	{0xcaec08aa13779f7f, "SystemNative_GetEnviron", nullptr},
    	{0xcaf599a20538b10b, "SystemNative_SetWindowSize", nullptr},
    	{0xcbbb90469d28cded, "SystemNative_SearchPath", nullptr},
    	{0xcc43d880192dd6ff, "SystemNative_ConvertErrorPlatformToPal", nullptr},
    	{0xcc788c0474c3e178, "SystemNative_LSeek", nullptr},
    	{0xcdcb014df9a6eae2, "SystemNative_SetPort", nullptr},
    	{0xce36e2e1a139a020, "SystemNative_GetDefaultTimeZone", nullptr},
    	{0xce6ddfe40fed99d9, "SystemNative_PRead", nullptr},
    	{0xd392d6ed5dcc111c, "SystemNative_GetDomainName", nullptr},
    	{0xd55437b16dc84f3b, "SystemNative_GetIPv4GlobalStatistics", nullptr},
    	{0xd88be8f9e9f28e90, "SystemNative_GetIcmpv4GlobalStatistics", nullptr},
    	{0xd8976692c4c68818, "SystemNative_GetEstimatedUdpListenerCount", nullptr},
    	{0xda05c57c78aa6706, "SystemNative_LowLevelMonitor_Signal_Release", nullptr},
    	{0xda38bffa1d16cdd6, "SystemNative_SetLingerOption", nullptr},
    	{0xda6b3192974ca60e, "SystemNative_Open", nullptr},
    	{0xdab5eb45815daabc, "SystemNative_GetAtOutOfBandMark", nullptr},
    	{0xdae32aac0c0d305c, "SystemNative_ReadProcessStatusInfo", nullptr},
    	{0xdbee22594fa8c585, "SystemNative_CreateAutoreleasePool", nullptr},
    	{0xdf650444c8af0763, "SystemNative_FcntlGetIsNonBlocking", nullptr},
    	{0xe0a170d2b947a8fc, "SystemNative_SendMessage", nullptr},
    	{0xe0a601fd89d9b279, "SystemNative_SetErrNo", nullptr},
    	{0xe1930d112ce74c9e, "SystemNative_TryGetUInt32OSThreadId", nullptr},
    	{0xe20c29fb8b19da7b, "SystemNative_Listen", nullptr},
    	{0xe36a157177b2db08, "SystemNative_GetNonCryptographicallySecureRandomBytes", nullptr},
    	{0xe44f737a5bebdd90, "SystemNative_SetIPv4Address", nullptr},
    	{0xe582a4a60bb74c35, "SystemNative_GetProcAddress", nullptr},
    	{0xe6838f2add787bfe, "SystemNative_FreeLibrary", nullptr},
    	{0xe73aeaf9e3a10343, "SystemNative_PWrite", nullptr},
    	{0xe78ff100d1d73d99, "SystemNative_SetReceiveTimeout", nullptr},
    	{0xe853ecfe4d402ed0, "SystemNative_Poll", nullptr},
    	{0xeaafb7963ceb9bf4, "SystemNative_GetTcpGlobalStatistics", nullptr},
    	{0xec67e4076662c2de, "SystemNative_GetDefaultSearchOrderPseudoHandle", nullptr},
    	{0xef71ee101b3ece96, "SystemNative_GetIcmpv6GlobalStatistics", nullptr},
    	{0xf0045895a9043221, "SystemNative_SearchPath_TempDirectory", nullptr},
    	{0xf0658a22dd5ede19, "SystemNative_SNPrintF_1I", nullptr},
    	{0xf0ec052da6c5fa70, "SystemNative_EnumerateGatewayAddressesForInterface", nullptr},
    	{0xf2c7fa39bf166188, "SystemNative_Free", nullptr},
    	{0xf38b47e43f352491, "SystemNative_GetUdpGlobalStatistics", nullptr},
    	{0xf6ede5d5d8729315, "SystemNative_WaitIdAnyExitedNoHangNoWait", nullptr},
    	{0xf870179a8d8d1872, "SystemNative_PosixFAdvise", nullptr},
    	{0xf8c983dd21ef9fe6, "SystemNative_GetPid", nullptr},
    	{0xfa26b86cedf66721, "SystemNative_Sysctl", nullptr},
    	{0xfb3e394cc613f202, "SystemNative_GetPeerName", nullptr},
    	{0xfbb57319454b1074, "SystemNative_GetSpaceInfoForMountPoint", nullptr},
    	{0xff28b3bec4f32a2c, "SystemNative_GetFileSystemType", nullptr},
	}};

	std::array<PinvokeEntry, 36> system_globalization_pinvokes {{
    	{0x410f8526b1edfc3, "GlobalizationNative_GetLocaleInfoInt", nullptr},
    	{0xe7e93cf9237e1f2, "GlobalizationNative_ToAscii", nullptr},
    	{0x18580a4592ed1ea6, "GlobalizationNative_GetSortKey", nullptr},
    	{0x1f72f52873ced9c9, "GlobalizationNative_InitOrdinalCasingPage", nullptr},
    	{0x2178ba302d0c5f1c, "GlobalizationNative_GetCalendars", nullptr},
    	{0x32e594690358a960, "GlobalizationNative_GetLocaleInfoString", nullptr},
    	{0x40d61d78487edb08, "GlobalizationNative_GetICUVersion", nullptr},
    	{0x4360eb8a25122eee, "GlobalizationNative_StartsWith", nullptr},
    	{0x4bd4b1c0803c8c55, "GlobalizationNative_GetLocaleName", nullptr},
    	{0x4f22643b9509cc12, "GlobalizationNative_IsNormalized", nullptr},
    	{0x507983f11ffec7a8, "GlobalizationNative_GetTimeZoneDisplayName", nullptr},
    	{0x56e982948d00f10d, "GlobalizationNative_IndexOf", nullptr},
    	{0x635327a9b09a910d, "GlobalizationNative_NormalizeString", nullptr},
    	{0x6ac3aeecfc75bfad, "GlobalizationNative_GetSortVersion", nullptr},
    	{0x77ca6a148e5a51d9, "GlobalizationNative_IanaIdToWindowsId", nullptr},
    	{0x7a4d912694906c9c, "GlobalizationNative_ToUnicode", nullptr},
    	{0x7e5fa2f70891c7fe, "GlobalizationNative_ChangeCaseTurkish", nullptr},
    	{0xa193402ff5140ac1, "GlobalizationNative_GetCalendarInfo", nullptr},
    	{0xa831a683f743e417, "GlobalizationNative_WindowsIdToIanaId", nullptr},
    	{0xac5c6a70d140a4bf, "GlobalizationNative_GetLocaleTimeFormat", nullptr},
    	{0xb81236cd1fe85cc9, "GlobalizationNative_GetLatestJapaneseEra", nullptr},
    	{0xb95350c7ec77bc72, "GlobalizationNative_ChangeCase", nullptr},
    	{0xc287daf58054a21d, "GlobalizationNative_EndsWith", nullptr},
    	{0xc8b772178f955d87, "GlobalizationNative_GetSortHandle", nullptr},
    	{0xd0899515dfe85287, "GlobalizationNative_LoadICU", nullptr},
    	{0xd185dfe303ab91dd, "GlobalizationNative_CompareString", nullptr},
    	{0xd5264d57a926edfb, "GlobalizationNative_InitICUFunctions", nullptr},
    	{0xd995e71361e6ed2e, "GlobalizationNative_IsPredefinedLocale", nullptr},
    	{0xe072da8f2d921f53, "GlobalizationNative_GetDefaultLocaleName", nullptr},
    	{0xea21aa1f2b2a671c, "GlobalizationNative_LastIndexOf", nullptr},
    	{0xee4dd111dc8d98f3, "GlobalizationNative_GetJapaneseEraStartDate", nullptr},
    	{0xf2d074e0aeca51ce, "GlobalizationNative_GetLocales", nullptr},
    	{0xf3693f3cadb9b6f4, "GlobalizationNative_EnumCalendarInfo", nullptr},
    	{0xf63fa2bfce5c4f80, "GlobalizationNative_GetLocaleInfoGroupingSizes", nullptr},
    	{0xfa21f0a127c9dce9, "GlobalizationNative_ChangeCaseInvariant", nullptr},
    	{0xfacf02f439426705, "GlobalizationNative_CloseSortHandle", nullptr},
	}};

	std::array<PinvokeEntry, 126> system_crypto_android_pinvokes {{
    	{0x375a0e90c77ca35, "AndroidCryptoNative_EcKeyCreateByExplicitParameters", nullptr},
    	{0x47302bd7e277183, "AndroidCryptoNative_X509GetCertificateForPrivateKeyEntry", nullptr},
    	{0x598db66ca39c41f, "AndroidCryptoNative_EcKeyUpRef", nullptr},
    	{0x656cac62ccc9e3c, "AndroidCryptoNative_X509GetContentType", nullptr},
    	{0x690c4347972024f, "AndroidCryptoNative_Aes256Gcm", nullptr},
    	{0x7b5579ab0499b1f, "AndroidCryptoNative_RsaSize", nullptr},
    	{0xcaba893801c6a6f, "AndroidCryptoNative_Aes256Ecb", nullptr},
    	{0xcbe6d3d22131194, "AndroidCryptoNative_SetRsaParameters", nullptr},
    	{0x1027786cdd9a3e9c, "AndroidCryptoNative_Aes192Cbc", nullptr},
    	{0x1d1bb0528d517729, "AndroidCryptoNative_SSLGetSupportedProtocols", nullptr},
    	{0x1e6228e955989698, "AndroidCryptoNative_EcKeyCreateByOid", nullptr},
    	{0x1f45ac9d3c6b1554, "AndroidCryptoNative_SSLStreamGetCipherSuite", nullptr},
    	{0x1f7d2360a1cdcbff, "AndroidCryptoNative_SSLStreamCreate", nullptr},
    	{0x218fce505a140c55, "AndroidCryptoNative_EcDsaVerify", nullptr},
    	{0x23ac2a4c4d1c744e, "AndroidCryptoNative_X509ChainGetCertificateCount", nullptr},
    	{0x267c94097a3bf1f3, "AndroidCryptoNative_CipherDestroy", nullptr},
    	{0x2b45d7cdf6e8e0c7, "AndroidCryptoNative_X509StoreDeleteEntry", nullptr},
    	{0x2c7e5e179cc917cb, "AndroidCryptoNative_DsaSizeSignature", nullptr},
    	{0x2fdcf708ff792105, "AndroidCryptoNative_SSLStreamVerifyHostname", nullptr},
    	{0x31027564deeb71b0, "AndroidCryptoNative_Aes128Cbc", nullptr},
    	{0x3f19a16a3230b551, "AndroidCryptoNative_ChaCha20Poly1305", nullptr},
    	{0x401935ffc3454bb1, "AndroidCryptoNative_X509PublicKey", nullptr},
    	{0x40bfa1211f5f6f9c, "AndroidCryptoNative_EcKeyGetCurveName", nullptr},
    	{0x41b6e7f32da99fa9, "AndroidCryptoNative_X509ChainDestroyContext", nullptr},
    	{0x41c169fb0e30a390, "AndroidCryptoNative_X509ChainGetErrorCount", nullptr},
    	{0x420718c398131a55, "AndroidCryptoNative_SSLStreamGetProtocol", nullptr},
    	{0x43741165a5ba60d5, "AndroidCryptoNative_CipherUpdateAAD", nullptr},
    	{0x4845e1c76265acc9, "AndroidCryptoNative_X509StoreEnumerateCertificates", nullptr},
    	{0x4a7272ac9d117f2d, "AndroidCryptoNative_EcKeyDestroy", nullptr},
    	{0x4d6361e5095cff36, "AndroidCryptoNative_DsaSign", nullptr},
    	{0x4d74053b37e582fa, "AndroidCryptoNative_X509ChainCreateContext", nullptr},
    	{0x501daf7e3a890220, "AndroidCryptoNative_X509ChainBuild", nullptr},
    	{0x52fc107ebdb6fcc7, "AndroidCryptoNative_X509StoreRemoveCertificate", nullptr},
    	{0x5fd29ac523ff6e3d, "AndroidCryptoNative_SSLStreamRelease", nullptr},
    	{0x5ffae3c8023a80b8, "AndroidCryptoNative_SSLStreamGetPeerCertificate", nullptr},
    	{0x648a9b317bc64fe0, "AndroidCryptoNative_RsaGenerateKeyEx", nullptr},
    	{0x66e049fe27bf91ea, "AndroidCryptoNative_SSLSupportsApplicationProtocolsConfiguration", nullptr},
    	{0x67a8868ef592a3fd, "AndroidCryptoNative_SSLStreamShutdown", nullptr},
    	{0x67a9b5bbce322f8c, "AndroidCryptoNative_Des3Cbc", nullptr},
    	{0x6a59d9242cd31785, "AndroidCryptoNative_RsaPrivateDecrypt", nullptr},
    	{0x6dbd90e9cc86310b, "AndroidCryptoNative_CipherFinalEx", nullptr},
    	{0x6dfd40c2dd0d7382, "AndroidCryptoNative_RsaUpRef", nullptr},
    	{0x6f990f1f7bc80630, "AndroidCryptoNative_RsaCreate", nullptr},
    	{0x70f907b97d3fe059, "AndroidCryptoNative_Aes192Ccm", nullptr},
    	{0x7150f0eb40797bb3, "AndroidCryptoNative_SSLStreamCreateWithCertificates", nullptr},
    	{0x7356b141407d261e, "AndroidCryptoNative_EcdhDeriveKey", nullptr},
    	{0x74ec4a8d869776ad, "AndroidCryptoNative_Aes128Ccm", nullptr},
    	{0x758dfbf057da0da0, "AndroidCryptoNative_DsaSignatureFieldSize", nullptr},
    	{0x7975d1d7029cf1a3, "AndroidCryptoNative_Aes128Gcm", nullptr},
    	{0x79f5c24afbd04af1, "AndroidCryptoNative_Aes256Cbc", nullptr},
    	{0x7a37e0d077f2dfe5, "AndroidCryptoNative_DsaGenerateKey", nullptr},
    	{0x7d5273ad530e7298, "AndroidCryptoNative_X509StoreOpenDefault", nullptr},
    	{0x7fa96d0284954375, "AndroidCryptoNative_X509Decode", nullptr},
    	{0x813bedf08c3388d4, "AndroidCryptoNative_Aes128Cfb8", nullptr},
    	{0x84cc0301870c37ce, "AndroidCryptoNative_SSLStreamSetTargetHost", nullptr},
    	{0x868e09dc7dfea364, "AndroidCryptoNative_RsaSignPrimitive", nullptr},
    	{0x870191ad244b8069, "AndroidCryptoNative_RegisterRemoteCertificateValidationCallback", nullptr},
    	{0x87019b7831c0c34c, "AndroidCryptoNative_Aes192Gcm", nullptr},
    	{0x87c447e7f873cff0, "AndroidCryptoNative_X509ChainValidate", nullptr},
    	{0x9039632237d70ae7, "AndroidCryptoNative_NewGlobalReference", nullptr},
    	{0x9161ade1206fd86e, "AndroidCryptoNative_Aes256Cfb128", nullptr},
    	{0x9167a072639a7c95, "AndroidCryptoNative_Aes256Ccm", nullptr},
    	{0x91f065ec0d3aec55, "AndroidCryptoNative_X509StoreAddCertificateWithPrivateKey", nullptr},
    	{0x95a0e2fc5c0cb49e, "AndroidCryptoNative_SSLStreamSetApplicationProtocols", nullptr},
    	{0x9991a277809ef205, "AndroidCryptoNative_SSLStreamGetApplicationProtocol", nullptr},
    	{0x9aab07f824659d3e, "AndroidCryptoNative_DsaKeyCreateByExplicitParameters", nullptr},
    	{0x9e79166979634030, "AndroidCryptoNative_CipherSetKeyAndIV", nullptr},
    	{0x9edddf30d660eff4, "AndroidCryptoNative_Aes192Ecb", nullptr},
    	{0xa308025a784497df, "AndroidCryptoNative_SSLStreamSetEnabledProtocols", nullptr},
    	{0xa56954e28eb9a9c9, "AndroidCryptoNative_Des3Cfb8", nullptr},
    	{0xa5eda72b95fe78c3, "AndroidCryptoNative_X509ChainGetErrors", nullptr},
    	{0xa93eb533acf7564d, "AndroidCryptoNative_DesEcb", nullptr},
    	{0xa961e8db31830e16, "AndroidCryptoNative_Aes192Cfb8", nullptr},
    	{0xaa8f0f87ae474ffe, "AndroidCryptoNative_SSLStreamCreateWithKeyStorePrivateKeyEntry", nullptr},
    	{0xad1a2d6575cdd4e3, "AndroidCryptoNative_SSLStreamWrite", nullptr},
    	{0xae82e9ceae24192d, "AndroidCryptoNative_Pbkdf2", nullptr},
    	{0xb0df46ff09c57741, "AndroidCryptoNative_GetRsaParameters", nullptr},
    	{0xb1c394b9992bd67d, "AndroidCryptoNative_EcDsaSign", nullptr},
    	{0xb1ff12f3bd735982, "AndroidCryptoNative_AeadCipherFinalEx", nullptr},
    	{0xb4996dd1aba38200, "AndroidCryptoNative_EcDsaSize", nullptr},
    	{0xb575ec01a7a79f8f, "AndroidCryptoNative_DesCfb8", nullptr},
    	{0xb66be1550d27bfb4, "AndroidCryptoNative_GetECCurveParameters", nullptr},
    	{0xbd5a0be2f7904089, "AndroidCryptoNative_X509StoreAddCertificate", nullptr},
    	{0xbdbbd2898347c0d1, "AndroidCryptoNative_SSLStreamHandshake", nullptr},
    	{0xc11cd661db8be230, "AndroidCryptoNative_Des3Cfb64", nullptr},
    	{0xc2d5e1c465b2f5b6, "AndroidCryptoNative_DsaSizeP", nullptr},
    	{0xc3145e336c38379b, "AndroidCryptoNative_SSLStreamGetPeerCertificates", nullptr},
    	{0xc7815e0476511544, "AndroidCryptoNative_X509Encode", nullptr},
    	{0xc8a52a8b6d96b32b, "AndroidCryptoNative_X509ExportPkcs7", nullptr},
    	{0xca48c3927c202794, "AndroidCryptoNative_GetECKeyParameters", nullptr},
    	{0xcb4bcdafdc81d116, "AndroidCryptoNative_CipherCreatePartial", nullptr},
    	{0xcc433093c073719e, "AndroidCryptoNative_SSLStreamRead", nullptr},
    	{0xce9f8a6ac705faa5, "AndroidCryptoNative_X509DecodeCollection", nullptr},
    	{0xd5c063a90ae882c1, "AndroidCryptoNative_CipherIsSupported", nullptr},
    	{0xd7d818c7640598dc, "AndroidCryptoNative_X509ChainGetCertificates", nullptr},
    	{0xd7f1a8f616897ace, "AndroidCryptoNative_Aes256Cfb8", nullptr},
    	{0xd9bd0b370726ce34, "AndroidCryptoNative_CipherReset", nullptr},
    	{0xda4898a26933f73d, "AndroidCryptoNative_DsaVerify", nullptr},
    	{0xdbb4752ed23670f0, "AndroidCryptoNative_DesCbc", nullptr},
    	{0xdc780005b0d39711, "AndroidCryptoNative_X509StoreGetPrivateKeyEntry", nullptr},
    	{0xdd4c03f06ce96e04, "AndroidCryptoNative_RsaDestroy", nullptr},
    	{0xdde06993f87d6ffc, "AndroidCryptoNative_Aes128Cfb128", nullptr},
    	{0xde1e22dd097f799c, "AndroidCryptoNative_CipherSetNonceLength", nullptr},
    	{0xde259001bf54e6f1, "AndroidCryptoNative_SSLStreamIsLocalCertificateUsed", nullptr},
    	{0xdec5c7544d2c8cb1, "AndroidCryptoNative_GetDsaParameters", nullptr},
    	{0xdfede2defd776f7e, "AndroidCryptoNative_X509ChainSetCustomTrustStore", nullptr},
    	{0xe059239741e0011a, "AndroidCryptoNative_EcKeyCreateByKeyParameters", nullptr},
    	{0xe0f34ce89fd38aef, "AndroidCryptoNative_RsaPublicEncrypt", nullptr},
    	{0xe604fca300068c0c, "AndroidCryptoNative_CipherCtxSetPadding", nullptr},
    	{0xeab45239fb3f138d, "AndroidCryptoNative_GetBigNumBytes", nullptr},
    	{0xeff5d014640ae969, "AndroidCryptoNative_DeleteGlobalReference", nullptr},
    	{0xf1577384f409ea85, "AndroidCryptoNative_BigNumToBinary", nullptr},
    	{0xf4dea312f71c5ff2, "AndroidCryptoNative_Aes128Ecb", nullptr},
    	{0xf57f81262f07542c, "AndroidCryptoNative_Des3Ecb", nullptr},
    	{0xf7b334768844b502, "AndroidCryptoNative_X509StoreContainsCertificate", nullptr},
    	{0xf85b8ffeba9b06c1, "AndroidCryptoNative_X509StoreEnumerateTrustedCertificates", nullptr},
    	{0xf96bc1e7e15e69f2, "AndroidCryptoNative_CipherUpdate", nullptr},
    	{0xf970881d4fa83e07, "AndroidCryptoNative_CipherCreate", nullptr},
    	{0xf9c3d216226b3355, "AndroidCryptoNative_CipherSetTagLength", nullptr},
    	{0xfa2669c25616a8ff, "AndroidCryptoNative_X509IsKeyStorePrivateKeyEntry", nullptr},
    	{0xfaa7766eaa2c54a5, "AndroidCryptoNative_DecodeRsaSubjectPublicKeyInfo", nullptr},
    	{0xfc0bad2b1528000f, "AndroidCryptoNative_RsaVerificationPrimitive", nullptr},
    	{0xfcdeea476953780c, "AndroidCryptoNative_Aes192Cfb128", nullptr},
    	{0xfd2cdd99f11de76c, "AndroidCryptoNative_EcKeyGetSize", nullptr},
    	{0xfd4f2784ec1c98aa, "AndroidCryptoNative_SSLStreamRequestClientAuthentication", nullptr},
    	{0xfe3dd06281f7cd1f, "AndroidCryptoNative_SSLStreamInitialize", nullptr},
	}};

	std::array<PinvokeEntry, 7> system_io_compression_pinvokes {{
    	{0x99f2ee02463000, "CompressionNative_Crc32", nullptr},
    	{0x403e1bc0b3baba84, "CompressionNative_Inflate", nullptr},
    	{0xafe3d21bbaa71464, "CompressionNative_DeflateEnd", nullptr},
    	{0xc10e411c989a9314, "CompressionNative_Deflate", nullptr},
    	{0xca001af79c0d7a8b, "CompressionNative_InflateEnd", nullptr},
    	{0xcd5d8a63493f5e38, "CompressionNative_InflateInit2_", nullptr},
    	{0xea5e6653389b924a, "CompressionNative_DeflateInit2_", nullptr},
	}};

	template<size_t NEntries> [[gnu::noinline, gnu::flatten]]
	PinvokeEntry* find_pinvoke_binary_search (std::array<PinvokeEntry, NEntries> data, hash_t hash)
	{
		auto equal = [](PinvokeEntry const& entry, hash_t key) -> bool { return entry.hash == key; };
		auto less_than = [](PinvokeEntry const& entry, hash_t key) -> bool { return entry.hash < key; };
		ssize_t idx = Search::binary_search<PinvokeEntry, equal, less_than> (hash, data.data (), NEntries);
		if (idx >= 0) {
			return &data[idx];
		}

		return nullptr;
	}
#endif
	//
	// These functions will eventually reside in the generated portion of code
	//
	void* find_system_native_entry (hash_t entrypoint_hash)
	{
//		log_debug (LOG_ASSEMBLY, "Looking up System.Native p/invoke");
#if INTPTR_MAX == INT64_MAX
		switch (entrypoint_hash) {
			case    0x38afc8bfe830b: return reinterpret_cast<void*>(&SystemNative_Bind                                          );
			case  0x190fe65d8736dcb: return reinterpret_cast<void*>(&SystemNative_TryGetIPPacketInformation                     );
			case  0x1c8b86562ad5772: return reinterpret_cast<void*>(&SystemNative_Receive                                       );
			case  0x202543f28ecaf06: return reinterpret_cast<void*>(&SystemNative_Abort                                         );
			case  0x25abeafa88904a2: return reinterpret_cast<void*>(&SystemNative_SetPosixSignalHandler                         );
			case  0x33158212a812caf: return reinterpret_cast<void*>(&SystemNative_GetEstimatedTcpConnectionCount                );
			case  0x3511e36d0a6c1b5: return reinterpret_cast<void*>(&SystemNative_LockFileRegion                                );
			case  0x37b9dd562235e42: return reinterpret_cast<void*>(&SystemNative_MSync                                         );
			case  0x3a5df4793dd3230: return reinterpret_cast<void*>(&SystemNative_INotifyInit                                   );
			case  0x3d24547fa4fc31b: return reinterpret_cast<void*>(&SystemNative_GetUInt64OSThreadId                           );
			case  0x581df5b0a00c422: return reinterpret_cast<void*>(&SystemNative_SetRLimit                                     );
			case  0x5b5ab451ff38f8e: return reinterpret_cast<void*>(&SystemNative_GetMaximumAddressSize                         );
			case  0x6861b5336291d12: return reinterpret_cast<void*>(&SystemNative_PathConf                                      );
			case  0x6a1f4deffa02c30: return reinterpret_cast<void*>(&SystemNative_LowLevelMonitor_Acquire                       );
			case  0x7ce8a9b967dd269: return reinterpret_cast<void*>(&SystemNative_Read                                          );
			case  0x8352ae4bba2b83b: return reinterpret_cast<void*>(&SystemNative_SetSendTimeout                                );
			case  0x98bd27a7461321d: return reinterpret_cast<void*>(&SystemNative_Dup                                           );
			case  0xa906c14ca5834bc: return reinterpret_cast<void*>(&SystemNative_GetEUid                                       );
			case  0xac9f9c1abb62a92: return reinterpret_cast<void*>(&SystemNative_Log                                           );
			case  0xadb2441bcfcdfe9: return reinterpret_cast<void*>(&SystemNative_CreateThread                                  );
			case  0xafbf5c69d1badc0: return reinterpret_cast<void*>(&SystemNative_SetTerminalInvalidationHandler                );
			case  0xba897b7abe67b16: return reinterpret_cast<void*>(&SystemNative_FcntlSetPipeSz                                );
			case  0xc305c22ce7ab8a0: return reinterpret_cast<void*>(&SystemNative_SetSockOpt                                    );
			case  0xc79e924361c15ca: return reinterpret_cast<void*>(&SystemNative_RealPath                                      );
			case  0xef8dd67e25bac53: return reinterpret_cast<void*>(&SystemNative_GetWindowSize                                 );
			case  0xfa0899cf8d00a87: return reinterpret_cast<void*>(&SystemNative_MkDir                                         );
			case  0xfe7079441ac127e: return reinterpret_cast<void*>(&SystemNative_CreateSocketEventPort                         );
			case 0x10d733abd1fd94bb: return reinterpret_cast<void*>(&SystemNative_TryChangeSocketEventRegistration              );
			case 0x114b8384553f5418: return reinterpret_cast<void*>(&SystemNative_GetSystemTimeAsTicks                          );
			case 0x119a38c3e288a233: return reinterpret_cast<void*>(&SystemNative_SNPrintF_1S                                   );
			case 0x11b6f4f0aafeda95: return reinterpret_cast<void*>(&SystemNative_LowLevelMonitor_TimedWait                     );
			case 0x11cc73f2926d4064: return reinterpret_cast<void*>(&SystemNative_ConfigureTerminalForChildProcess              );
			case 0x121bc483ac26f5f8: return reinterpret_cast<void*>(&SystemNative_GetGroupName                                  );
			case 0x12d65f9f65b01497: return reinterpret_cast<void*>(&SystemNative_GetRawSockOpt                                 );
			case 0x12eaf09505dc19fd: return reinterpret_cast<void*>(&SystemNative_FStat                                         );
			case 0x13577369f5ec4b0a: return reinterpret_cast<void*>(&SystemNative_GetActiveTcpConnectionInfos                   );
			case 0x1399413d8a7d9dd8: return reinterpret_cast<void*>(&SystemNative_GetAddressFamily                              );
			case 0x13a1c2de7fb2519f: return reinterpret_cast<void*>(&SystemNative_CloseSocketEventPort                          );
			case 0x146cd1dc4fb2ba58: return reinterpret_cast<void*>(&SystemNative_LChflagsCanSetHiddenFlag                      );
			case 0x176e22ea7c580dae: return reinterpret_cast<void*>(&SystemNative_ReadDirR                                      );
			case 0x185f5d25252c3c72: return reinterpret_cast<void*>(&SystemNative_FAllocate                                     );
			case 0x18d6b5e9fec9b0dc: return reinterpret_cast<void*>(&SystemNative_Connectx                                      );
			case 0x18f7da5f584b5b59: return reinterpret_cast<void*>(&SystemNative_PReadV                                        );
			case 0x1948a0cf88329c2f: return reinterpret_cast<void*>(&SystemNative_HandleNonCanceledPosixSignal                  );
			case 0x1ac95b02f23933cc: return reinterpret_cast<void*>(&SystemNative_CanGetHiddenFlag                              );
			case 0x1d4dcbc06728e689: return reinterpret_cast<void*>(&SystemNative_Close                                         );
			case 0x1d6d4278ffbbab77: return reinterpret_cast<void*>(&SystemNative_Pipe                                          );
			case 0x1d8d6a688fc5bfb3: return reinterpret_cast<void*>(&SystemNative_SendFile                                      );
			case 0x1f1c61a157636aad: return reinterpret_cast<void*>(&SystemNative_Stat                                          );
			case 0x1f849e45a3014a9f: return reinterpret_cast<void*>(&SystemNative_GetIPv6Address                                );
			case 0x1f9361fc7b624c1b: return reinterpret_cast<void*>(&SystemNative_LowLevelMonitor_Wait                          );
			case 0x2291e0ba4e1b55b0: return reinterpret_cast<void*>(&SystemNative_LStat                                         );
			case 0x24f840f903a26ded: return reinterpret_cast<void*>(&SystemNative_ConvertErrorPalToPlatform                     );
			case 0x24ff74e427d0626e: return reinterpret_cast<void*>(&SystemNative_GetErrNo                                      );
			case 0x254905036a0061cf: return reinterpret_cast<void*>(&SystemNative_CreateSocketEventBuffer                       );
			case 0x255c4a2e297fd9f5: return reinterpret_cast<void*>(&SystemNative_INotifyAddWatch                               );
			case 0x27f3d9266af2b315: return reinterpret_cast<void*>(&SystemNative_GetIPv4Address                                );
			case 0x2925953889c48cab: return reinterpret_cast<void*>(&SystemNative_CreateNetworkChangeListenerSocket             );
			case 0x2a49948ae20571cb: return reinterpret_cast<void*>(&SystemNative_SchedGetAffinity                              );
			case 0x2c8da1192c5d7d2b: return reinterpret_cast<void*>(&SystemNative_FLock                                         );
			case 0x2d64b1ac218cf29e: return reinterpret_cast<void*>(&SystemNative_AlignedRealloc                                );
			case 0x2e429d96a9fc92bd: return reinterpret_cast<void*>(&SystemNative_InitializeTerminalAndSignalHandling           );
			case 0x301c465c1ac0adf9: return reinterpret_cast<void*>(&SystemNative_MProtect                                      );
			case 0x3319a5483b3cc1fc: return reinterpret_cast<void*>(&SystemNative_GetRLimit                                     );
			case 0x3424ffcb69ecef57: return reinterpret_cast<void*>(&SystemNative_Unlink                                        );
			case 0x346a9bb11364833c: return reinterpret_cast<void*>(&SystemNative_DrainAutoreleasePool                          );
			case 0x35169e67cc0f8529: return reinterpret_cast<void*>(&SystemNative_GetIPv6MulticastOption                        );
			case 0x359205b4a10fa780: return reinterpret_cast<void*>(&SystemNative_LowLevelMonitor_Destroy                       );
			case 0x36128eed665b1923: return reinterpret_cast<void*>(&SystemNative_ShmUnlink                                     );
			case 0x364dcf65ae63adff: return reinterpret_cast<void*>(&SystemNative_GetSocketErrorOption                          );
			case 0x3757b327944abb54: return reinterpret_cast<void*>(&SystemNative_EnablePosixSignalHandling                     );
			case 0x38b4bd21127ceffd: return reinterpret_cast<void*>(&SystemNative_StrErrorR                                     );
			case 0x38c7de719e8ae69d: return reinterpret_cast<void*>(&SystemNative_RmDir                                         );
			case 0x391bbbb9bbde4455: return reinterpret_cast<void*>(&SystemNative_SetIPv4MulticastOption                        );
			case 0x3a7245f3ea476bf7: return reinterpret_cast<void*>(&SystemNative_SNPrintF                                      );
			case 0x3ae92e4198427b0d: return reinterpret_cast<void*>(&SystemNative_ReadLink                                      );
			case 0x3e0de839e6cfa6e5: return reinterpret_cast<void*>(&SystemNative_Accept                                        );
			case 0x3e7cf9a4789a31c7: return reinterpret_cast<void*>(&SystemNative_FChflags                                      );
			case 0x3f49b6278f04ae84: return reinterpret_cast<void*>(&SystemNative_Disconnect                                    );
			case 0x3fba15600bf0f229: return reinterpret_cast<void*>(&SystemNative_SetEUid                                       );
			case 0x41c1f2c9153639af: return reinterpret_cast<void*>(&SystemNative_FUTimens                                      );
			case 0x42339dd2717504d9: return reinterpret_cast<void*>(&SystemNative_GetLingerOption                               );
			case 0x42783107bf2935ec: return reinterpret_cast<void*>(&SystemNative_FreeHostEntry                                 );
			case 0x42eb0578a9d62b78: return reinterpret_cast<void*>(&SystemNative_GetFormatInfoForMountPoint                    );
			case 0x44ccb27979f980ce: return reinterpret_cast<void*>(&SystemNative_AlignedAlloc                                  );
			case 0x44f1a5c46033eec2: return reinterpret_cast<void*>(&SystemNative_SysLog                                        );
			case 0x483b434d7b089c7e: return reinterpret_cast<void*>(&SystemNative_Write                                         );
			case 0x484a3a445bdb14fc: return reinterpret_cast<void*>(&SystemNative_GetOSArchitecture                             );
			case 0x4909639a9d87bdb5: return reinterpret_cast<void*>(&SystemNative_AlignedFree                                   );
			case 0x49e3ba95feb79c6c: return reinterpret_cast<void*>(&SystemNative_SetAddressFamily                              );
			case 0x4b00795bbeea6f60: return reinterpret_cast<void*>(&SystemNative_SetIPv6Address                                );
			case 0x4be7ceca50f3298c: return reinterpret_cast<void*>(&SystemNative_LowLevelMonitor_Create                        );
			case 0x4bec4a1d7dfd4cf7: return reinterpret_cast<void*>(&SystemNative_GetUnixRelease                                );
			case 0x4bfff22801b209ca: return reinterpret_cast<void*>(&SystemNative_LChflags                                      );
			case 0x4c22cc4f2b1dab26: return reinterpret_cast<void*>(&SystemNative_SetPriority                                   );
			case 0x509ff12da4e77259: return reinterpret_cast<void*>(&SystemNative_GetSocketAddressSizes                         );
			case 0x523240c01d14ad50: return reinterpret_cast<void*>(&SystemNative_GetPeerID                                     );
			case 0x52794f1118d32f08: return reinterpret_cast<void*>(&SystemNative_GetUnixVersion                                );
			case 0x5381564d2c06c0a3: return reinterpret_cast<void*>(&SystemNative_SysConf                                       );
			case 0x556bc89d2d4dfc85: return reinterpret_cast<void*>(&SystemNative_GetDeviceIdentifiers                          );
			case 0x5592a052ceb4caf6: return reinterpret_cast<void*>(&SystemNative_GetProcessPath                                );
			case 0x55fe2620f63d83d8: return reinterpret_cast<void*>(&SystemNative_SetDelayedSigChildConsoleConfigurationHandler );
			case 0x574d77a68ec3e488: return reinterpret_cast<void*>(&SystemNative_GetEnv                                        );
			case 0x583db0344a1cd715: return reinterpret_cast<void*>(&SystemNative_GetActiveUdpListeners                         );
			case 0x5908581fe73717f0: return reinterpret_cast<void*>(&SystemNative_InterfaceNameToIndex                          );
			case 0x5a305cf2a314d6a6: return reinterpret_cast<void*>(&SystemNative_FTruncate                                     );
			case 0x5e53b688fede3216: return reinterpret_cast<void*>(&SystemNative_GetControlCharacters                          );
			case 0x5fa62856bdbba9c0: return reinterpret_cast<void*>(&SystemNative_GetPort                                       );
			case 0x600b4418896f7808: return reinterpret_cast<void*>(&SystemNative_Exit                                          );
			case 0x6089f0c8112eb3d9: return reinterpret_cast<void*>(&SystemNative_InitializeConsoleBeforeRead                   );
			case 0x613307e537d462db: return reinterpret_cast<void*>(&SystemNative_GetReadDirRBufferSize                         );
			case 0x61bacd7170fd8c9b: return reinterpret_cast<void*>(&SystemNative_SchedSetAffinity                              );
			case 0x61f3ce1b18b20d6f: return reinterpret_cast<void*>(&SystemNative_GetNativeIPInterfaceStatistics                );
			case 0x62351df42d842942: return reinterpret_cast<void*>(&SystemNative_GetSignalForBreak                             );
			case 0x6393d30aceaa6df2: return reinterpret_cast<void*>(&SystemNative_PWriteV                                       );
			case 0x6448f0806bd3a338: return reinterpret_cast<void*>(&SystemNative_FreeEnviron                                   );
			case 0x650eddee76c6b8da: return reinterpret_cast<void*>(&SystemNative_GetHostName                                   );
			case 0x652badfba5d61929: return reinterpret_cast<void*>(&SystemNative_FcntlSetFD                                    );
			case 0x67d2cd86792b1d0c: return reinterpret_cast<void*>(&SystemNative_Realloc                                       );
			case 0x67e9d60481f4be06: return reinterpret_cast<void*>(&SystemNative_PlatformSupportsDualModeIPv4PacketInfo        );
			case 0x68df81a8fb5bf442: return reinterpret_cast<void*>(&SystemNative_GetSockOpt                                    );
			case 0x68f3fe6083c0355b: return reinterpret_cast<void*>(&SystemNative_GetLoadLibraryError                           );
			case 0x69ad99fac0467f64: return reinterpret_cast<void*>(&SystemNative_Link                                          );
			case 0x6b9097385aa77917: return reinterpret_cast<void*>(&SystemNative_FSync                                         );
			case 0x6b9bce16ba8e845f: return reinterpret_cast<void*>(&SystemNative_Malloc                                        );
			case 0x6bc18fbbbf267e2a: return reinterpret_cast<void*>(&SystemNative_ReceiveSocketError                            );
			case 0x6e2c1caff08e6e2d: return reinterpret_cast<void*>(&SystemNative_ReadStdin                                     );
			case 0x6ee05d5e8650e56c: return reinterpret_cast<void*>(&SystemNative_DisablePosixSignalHandling                    );
			case 0x729afe37cdb8ae8f: return reinterpret_cast<void*>(&SystemNative_Connect                                       );
			case 0x730ae9a7469a7321: return reinterpret_cast<void*>(&SystemNative_GetAllMountPoints                             );
			case 0x742da00b2dbf435d: return reinterpret_cast<void*>(&SystemNative_LoadLibrary                                   );
			case 0x7559feb379d38da5: return reinterpret_cast<void*>(&SystemNative_GetTimeZoneData                               );
			case 0x7d7ee4bce74d4de9: return reinterpret_cast<void*>(&SystemNative_GetDomainSocketSizes                          );
			case 0x7e1766c6df3ad261: return reinterpret_cast<void*>(&SystemNative_MUnmap                                        );
			case 0x7e4bdf46d4ff9f11: return reinterpret_cast<void*>(&SystemNative_MkNod                                         );
			case 0x7ec328b6ba9eab8a: return reinterpret_cast<void*>(&SystemNative_WaitForSocketEvents                           );
			case 0x84c8a7489b37fea0: return reinterpret_cast<void*>(&SystemNative_GetPlatformSignalNumber                       );
			case 0x8502eeba98158e79: return reinterpret_cast<void*>(&SystemNative_FcntlSetIsNonBlocking                         );
			case 0x8530d37777969db6: return reinterpret_cast<void*>(&SystemNative_SetKeypadXmit                                 );
			case 0x85d0033bc38bb4bb: return reinterpret_cast<void*>(&SystemNative_MAdvise                                       );
			case 0x889350f209555ecb: return reinterpret_cast<void*>(&SystemNative_MkdTemp                                       );
			case 0x88a08b60b80c70cc: return reinterpret_cast<void*>(&SystemNative_FChMod                                        );
			case 0x8bcabce135063bed: return reinterpret_cast<void*>(&SystemNative_OpenDir                                       );
			case 0x8df448aee6e8fa5e: return reinterpret_cast<void*>(&SystemNative_WaitPidExitedNoHang                           );
			case 0x8e96cb02418947cc: return reinterpret_cast<void*>(&SystemNative_FcntlGetPipeSz                                );
			case 0x8fb6ed14ee0256bc: return reinterpret_cast<void*>(&SystemNative_GetTimestamp                                  );
			case 0x8ffe2d950d138c01: return reinterpret_cast<void*>(&SystemNative_SchedGetCpu                                   );
			case 0x93a8bec488055608: return reinterpret_cast<void*>(&SystemNative_GetPwNamR                                     );
			case 0x95a4cb8563cc6b14: return reinterpret_cast<void*>(&SystemNative_ShmOpen                                       );
			case 0x9856fa59ed936b73: return reinterpret_cast<void*>(&SystemNative_GetSid                                        );
			case 0x996ada1c038aabba: return reinterpret_cast<void*>(&SystemNative_MksTemps                                      );
			case 0x99a840c495204202: return reinterpret_cast<void*>(&SystemNative_GetBytesAvailable                             );
			case 0x9aa9eaee3dd8b23b: return reinterpret_cast<void*>(&SystemNative_GetIPv4MulticastOption                        );
			case 0x9aaaad33b28af82f: return reinterpret_cast<void*>(&SystemNative_SetSignalForBreak                             );
			case 0x9c3e8b890033819a: return reinterpret_cast<void*>(&SystemNative_FcntlCanGetSetPipeSz                          );
			case 0x9c832cd7fcbf2de0: return reinterpret_cast<void*>(&SystemNative_MkFifo                                        );
			case 0x9d2cb31282abd3d9: return reinterpret_cast<void*>(&SystemNative_GetNetworkInterfaces                          );
			case 0x9e25ebf4f61cc299: return reinterpret_cast<void*>(&SystemNative_ChDir                                         );
			case 0x9fb01da1222e905a: return reinterpret_cast<void*>(&SystemNative_IsATty                                        );
			case 0xa1e881a63614507e: return reinterpret_cast<void*>(&SystemNative_INotifyRemoveWatch                            );
			case 0xa2254fea4d8b6909: return reinterpret_cast<void*>(&SystemNative_MMap                                          );
			case 0xa2d7790a850024c0: return reinterpret_cast<void*>(&SystemNative_GetNumRoutes                                  );
			case 0xa302613a430248b8: return reinterpret_cast<void*>(&SystemNative_GetGroups                                     );
			case 0xa56532a23755cd87: return reinterpret_cast<void*>(&SystemNative_StdinReady                                    );
			case 0xa89ec9958d999483: return reinterpret_cast<void*>(&SystemNative_GetCwd                                        );
			case 0xa8bdc3e7ee898dfc: return reinterpret_cast<void*>(&SystemNative_Shutdown                                      );
			case 0xabdcf2f74d210f35: return reinterpret_cast<void*>(&SystemNative_GetCryptographicallySecureRandomBytes         );
			case 0xac11eab9d9c31b01: return reinterpret_cast<void*>(&SystemNative_UTimensat                                     );
			case 0xac7725c652a5fb5b: return reinterpret_cast<void*>(&SystemNative_CopyFile                                      );
			case 0xad228cdc4edb11d6: return reinterpret_cast<void*>(&SystemNative_CloseDir                                      );
			case 0xadc6889903a2d6f4: return reinterpret_cast<void*>(&SystemNative_Rename                                        );
			case 0xae320903718eb45d: return reinterpret_cast<void*>(&SystemNative_MapTcpState                                   );
			case 0xaf9706efc72c3904: return reinterpret_cast<void*>(&SystemNative_SetIPv6MulticastOption                        );
			case 0xafd9f6338cdbadd4: return reinterpret_cast<void*>(&SystemNative_GetHostEntryForName                           );
			case 0xb0b66a7145de350d: return reinterpret_cast<void*>(&SystemNative_Access                                        );
			case 0xb0e18377ed603e0b: return reinterpret_cast<void*>(&SystemNative_GetGroupList                                  );
			case 0xb361006446f560e8: return reinterpret_cast<void*>(&SystemNative_LogError                                      );
			case 0xb600c44028c1743d: return reinterpret_cast<void*>(&SystemNative_Socket                                        );
			case 0xb632e9bc6f7be0a9: return reinterpret_cast<void*>(&SystemNative_GetSockName                                   );
			case 0xb6540b73eff28747: return reinterpret_cast<void*>(&SystemNative_SetRawSockOpt                                 );
			case 0xb6ab9abf7887911f: return reinterpret_cast<void*>(&SystemNative_ReadEvents                                    );
			case 0xb73c597de01bc0b2: return reinterpret_cast<void*>(&SystemNative_GetPwUidR                                     );
			case 0xb78af5975603cd20: return reinterpret_cast<void*>(&SystemNative_Sync                                          );
			case 0xb7bbbe2c16a565c6: return reinterpret_cast<void*>(&SystemNative_Calloc                                        );
			case 0xbb3343826d504870: return reinterpret_cast<void*>(&SystemNative_GetBootTimeTicks                              );
			case 0xbb5e970ecb6745da: return reinterpret_cast<void*>(&SystemNative_SymLink                                       );
			case 0xbbd20cce92ec2c12: return reinterpret_cast<void*>(&SystemNative_FcntlGetFD                                    );
			case 0xbcd9e53d2d288094: return reinterpret_cast<void*>(&SystemNative_GetNameInfo                                   );
			case 0xbd89ef4df5486744: return reinterpret_cast<void*>(&SystemNative_Send                                          );
			case 0xbdd3128e77381b01: return reinterpret_cast<void*>(&SystemNative_EnumerateInterfaceAddresses                   );
			case 0xc00ebc097b776c1f: return reinterpret_cast<void*>(&SystemNative_GetPriority                                   );
			case 0xc036b23d88fad91b: return reinterpret_cast<void*>(&SystemNative_iOSSupportVersion                             );
			case 0xc1c679eefc134d31: return reinterpret_cast<void*>(&SystemNative_LowLevelMonitor_Release                       );
			case 0xc3c10021b10ba455: return reinterpret_cast<void*>(&SystemNative_GetEGid                                       );
			case 0xc3fe9394fe1f3f02: return reinterpret_cast<void*>(&SystemNative_GetSocketType                                 );
			case 0xc560d9947ab2a34d: return reinterpret_cast<void*>(&SystemNative_RegisterForSigChld                            );
			case 0xc5bed971846027de: return reinterpret_cast<void*>(&SystemNative_GetCpuUtilization                             );
			case 0xc69433678dd341ca: return reinterpret_cast<void*>(&SystemNative_ForkAndExecProcess                            );
			case 0xc7ae1b8d93af5d73: return reinterpret_cast<void*>(&SystemNative_ChMod                                         );
			case 0xc7d536c0e7eb3fe2: return reinterpret_cast<void*>(&SystemNative_FreeSocketEventBuffer                         );
			case 0xc87a5ee4869035c6: return reinterpret_cast<void*>(&SystemNative_UninitializeConsoleAfterRead                  );
			case 0xc93df58ae5457bfd: return reinterpret_cast<void*>(&SystemNative_GetControlMessageBufferSize                   );
			case 0xc956e528f995739c: return reinterpret_cast<void*>(&SystemNative_ReceiveMessage                                );
			case 0xcaae6d345ba32c7b: return reinterpret_cast<void*>(&SystemNative_Kill                                          );
			case 0xcaec08aa13779f7f: return reinterpret_cast<void*>(&SystemNative_GetEnviron                                    );
			case 0xcbbb90469d28cded: return reinterpret_cast<void*>(&SystemNative_SearchPath                                    );
			case 0xcc43d880192dd6ff: return reinterpret_cast<void*>(&SystemNative_ConvertErrorPlatformToPal                     );
			case 0xcc788c0474c3e178: return reinterpret_cast<void*>(&SystemNative_LSeek                                         );
			case 0xcdcb014df9a6eae2: return reinterpret_cast<void*>(&SystemNative_SetPort                                       );
			case 0xce36e2e1a139a020: return reinterpret_cast<void*>(&SystemNative_GetDefaultTimeZone                            );
			case 0xce6ddfe40fed99d9: return reinterpret_cast<void*>(&SystemNative_PRead                                         );
			case 0xd392d6ed5dcc111c: return reinterpret_cast<void*>(&SystemNative_GetDomainName                                 );
			case 0xd55437b16dc84f3b: return reinterpret_cast<void*>(&SystemNative_GetIPv4GlobalStatistics                       );
			case 0xd88be8f9e9f28e90: return reinterpret_cast<void*>(&SystemNative_GetIcmpv4GlobalStatistics                     );
			case 0xd8976692c4c68818: return reinterpret_cast<void*>(&SystemNative_GetEstimatedUdpListenerCount                  );
			case 0xda05c57c78aa6706: return reinterpret_cast<void*>(&SystemNative_LowLevelMonitor_Signal_Release                );
			case 0xda38bffa1d16cdd6: return reinterpret_cast<void*>(&SystemNative_SetLingerOption                               );
			case 0xda6b3192974ca60e: return reinterpret_cast<void*>(&SystemNative_Open                                          );
			case 0xdab5eb45815daabc: return reinterpret_cast<void*>(&SystemNative_GetAtOutOfBandMark                            );
			case 0xdae32aac0c0d305c: return reinterpret_cast<void*>(&SystemNative_ReadProcessStatusInfo                         );
			case 0xdbee22594fa8c585: return reinterpret_cast<void*>(&SystemNative_CreateAutoreleasePool                         );
			case 0xdf650444c8af0763: return reinterpret_cast<void*>(&SystemNative_FcntlGetIsNonBlocking                         );
			case 0xe0a170d2b947a8fc: return reinterpret_cast<void*>(&SystemNative_SendMessage                                   );
			case 0xe0a601fd89d9b279: return reinterpret_cast<void*>(&SystemNative_SetErrNo                                      );
			case 0xe1930d112ce74c9e: return reinterpret_cast<void*>(&SystemNative_TryGetUInt32OSThreadId                        );
			case 0xe20c29fb8b19da7b: return reinterpret_cast<void*>(&SystemNative_Listen                                        );
			case 0xe36a157177b2db08: return reinterpret_cast<void*>(&SystemNative_GetNonCryptographicallySecureRandomBytes      );
			case 0xe44f737a5bebdd90: return reinterpret_cast<void*>(&SystemNative_SetIPv4Address                                );
			case 0xe582a4a60bb74c35: return reinterpret_cast<void*>(&SystemNative_GetProcAddress                                );
			case 0xe6838f2add787bfe: return reinterpret_cast<void*>(&SystemNative_FreeLibrary                                   );
			case 0xe73aeaf9e3a10343: return reinterpret_cast<void*>(&SystemNative_PWrite                                        );
			case 0xe78ff100d1d73d99: return reinterpret_cast<void*>(&SystemNative_SetReceiveTimeout                             );
			case 0xe853ecfe4d402ed0: return reinterpret_cast<void*>(&SystemNative_Poll                                          );
			case 0xeaafb7963ceb9bf4: return reinterpret_cast<void*>(&SystemNative_GetTcpGlobalStatistics                        );
			case 0xec67e4076662c2de: return reinterpret_cast<void*>(&SystemNative_GetDefaultSearchOrderPseudoHandle             );
			case 0xef71ee101b3ece96: return reinterpret_cast<void*>(&SystemNative_GetIcmpv6GlobalStatistics                     );
			case 0xf0045895a9043221: return reinterpret_cast<void*>(&SystemNative_SearchPath_TempDirectory                      );
			case 0xf0658a22dd5ede19: return reinterpret_cast<void*>(&SystemNative_SNPrintF_1I                                   );
			case 0xf0ec052da6c5fa70: return reinterpret_cast<void*>(&SystemNative_EnumerateGatewayAddressesForInterface         );
			case 0xf2c7fa39bf166188: return reinterpret_cast<void*>(&SystemNative_Free                                          );
			case 0xf38b47e43f352491: return reinterpret_cast<void*>(&SystemNative_GetUdpGlobalStatistics                        );
			case 0xf6ede5d5d8729315: return reinterpret_cast<void*>(&SystemNative_WaitIdAnyExitedNoHangNoWait                   );
			case 0xf870179a8d8d1872: return reinterpret_cast<void*>(&SystemNative_PosixFAdvise                                  );
			case 0xf8c983dd21ef9fe6: return reinterpret_cast<void*>(&SystemNative_GetPid                                        );
			case 0xfa26b86cedf66721: return reinterpret_cast<void*>(&SystemNative_Sysctl                                        );
			case 0xfb3e394cc613f202: return reinterpret_cast<void*>(&SystemNative_GetPeerName                                   );
			case 0xfbb57319454b1074: return reinterpret_cast<void*>(&SystemNative_GetSpaceInfoForMountPoint                     );
			case 0xff28b3bec4f32a2c: return reinterpret_cast<void*>(&SystemNative_GetFileSystemType                             );
		}
#endif
		return nullptr;
	}

#if INTPTR_MAX == INT64_MAX
	template<void* (*linear_search)(hash_t), size_t NEntries>
	void find_entry_benchmark (const char *label, std::array<PinvokeEntry, NEntries> const& data)
	{
		log_debug (LOG_ASSEMBLY, "%s search benchmark, %zu entries", label, data.size ());

		hash_t first = data[0].hash;
		hash_t middle = data[data.size () / 2].hash;
		hash_t last = data[data.size () - 1].hash;
		size_t index;
		PinvokeEntry *entry;
		void *ptr;

		timing_period elapsed;
		auto log_elapsed = [&elapsed](const char *label) {
			timing_diff diff (elapsed);
			log_debug (LOG_ASSEMBLY, "%s; elapsed: %lis:%lu::%lu", label, diff.sec, diff.ms, diff.ns);
		};

		elapsed.mark_start ();
		entry = find_pinvoke_binary_search (data, first);
		elapsed.mark_end ();
		log_elapsed ( "BINARY: find first entry");

		elapsed.reset ();
		elapsed.mark_start ();
		entry = find_pinvoke_binary_search (data, middle);
		elapsed.mark_end ();
		log_elapsed ("BINARY: find middle entry");

		elapsed.reset ();
		elapsed.mark_start ();
		entry = find_pinvoke_binary_search (data, last);
		elapsed.mark_end ();
		log_elapsed ("BINARY: find last entry");

		elapsed.reset ();
		elapsed.mark_start ();
		ptr = linear_search (first);
		elapsed.mark_end ();
		log_elapsed ("LINEAR: find first entry");

		elapsed.reset ();
		elapsed.mark_start ();
		ptr = linear_search (middle);
		elapsed.mark_end ();
		log_elapsed ("LINEAR: find middle entry");

		elapsed.reset ();
		elapsed.mark_start ();
		ptr = linear_search (last);
		elapsed.mark_end ();
		log_elapsed ("LINEAR: find last entry");
	}

	void find_system_native_entry_benchmark ()
	{
		find_entry_benchmark<find_system_native_entry> ("System.Native", system_native_pinvokes);
	}
#endif

	void* find_internal_entry (hash_t entrypoint_hash)
	{
//		log_debug (LOG_ASSEMBLY, "Looking up internal p/invoke");

#if INTPTR_MAX == INT64_MAX
		switch (entrypoint_hash) {
			case  0x452e23128e42f0a: return reinterpret_cast<void*>(&monodroid_get_log_categories);
			case  0xa50ce5de13bf8b5: return reinterpret_cast<void*>(&_monodroid_timezone_get_default_id);
			case 0x19055d65edfd668e: return reinterpret_cast<void*>(&_monodroid_get_network_interface_up_state);
			case 0x2b3b0ca1d14076da: return reinterpret_cast<void*>(&monodroid_get_dylib);
			case 0x2fbe68718cf2510d: return reinterpret_cast<void*>(&_monodroid_get_identity_hash_code);
			case 0x3ade4348ac8ce0fa: return reinterpret_cast<void*>(&_monodroid_freeifaddrs);
			case 0x3b2467e7eadd4a6a: return reinterpret_cast<void*>(&_monodroid_lref_log_new);
			case 0x3b8097af56b5361f: return reinterpret_cast<void*>(&monodroid_log_traces);
			case 0x3c5532ecdab53f89: return reinterpret_cast<void*>(&set_world_accessable);
			case 0x423c8f539a2c56d2: return reinterpret_cast<void*>(&_monodroid_lookup_replacement_type);
			case 0x4b1956138764939a: return reinterpret_cast<void*>(&_monodroid_gref_log_new);
			case 0x4d5b5b488f736058: return reinterpret_cast<void*>(&path_combine);
			case 0x5a2614d15e2fdc2e: return reinterpret_cast<void*>(&monodroid_strdup_printf);
			case 0x5f0b4e426eff086b: return reinterpret_cast<void*>(&_monodroid_detect_cpu_and_architecture);
			case 0x709af13cbfbe2e75: return reinterpret_cast<void*>(&monodroid_clear_gdb_wait);
			case 0x70ae32c9a4f1ad2c: return reinterpret_cast<void*>(&monodroid_strsplit);
			case 0x70fc9bab8d56666d: return reinterpret_cast<void*>(&create_public_directory);
			case 0x78514771a67ad724: return reinterpret_cast<void*>(&monodroid_strfreev);
			case 0x9099a4b95e3c3a89: return reinterpret_cast<void*>(&_monodroid_lref_log_delete);
			case 0x958cdb6fd9d1b67b: return reinterpret_cast<void*>(&monodroid_dylib_mono_new);
			case 0xa6ec846592d99536: return reinterpret_cast<void*>(&_monodroid_weak_gref_delete);
			case 0xa7f58f3ee428cc6b: return reinterpret_cast<void*>(&_monodroid_gref_log_delete);
			case 0xae3df96dda0143bd: return reinterpret_cast<void*>(&_monodroid_gref_log);
			case 0xb6222d90af401865: return reinterpret_cast<void*>(&_monodroid_weak_gref_get);
			case 0xb8306f71b963cd3d: return reinterpret_cast<void*>(&monodroid_log);
			case 0xbc90bafd5ff9c99e: return reinterpret_cast<void*>(&_monodroid_get_dns_servers);
			case 0xbe5a300beec69c35: return reinterpret_cast<void*>(&monodroid_get_system_property);
			case 0xbfbb924fbe190616: return reinterpret_cast<void*>(&monodroid_dylib_mono_free);
			case 0xc2a21d3f6c8ccc24: return reinterpret_cast<void*>(&_monodroid_lookup_replacement_method_info);
			case 0xc5b4690e13898fa3: return reinterpret_cast<void*>(&monodroid_timing_start);
			case 0xcc873ea8493d1dd5: return reinterpret_cast<void*>(&monodroid_embedded_assemblies_set_assemblies_prefix);
			case 0xce439cfbe29dec11: return reinterpret_cast<void*>(&_monodroid_get_android_api_level);
			case 0xd1e121b94ea63f2e: return reinterpret_cast<void*>(&_monodroid_gref_get);
			case 0xd5151b00eb33d85e: return reinterpret_cast<void*>(&monodroid_TypeManager_get_java_class_name);
			case 0xda517ef392b6a888: return reinterpret_cast<void*>(&java_interop_free);
			case 0xe27b9849b7e982cb: return reinterpret_cast<void*>(&_monodroid_max_gref_get);
			case 0xe370a0d91cd63bc0: return reinterpret_cast<void*>(&_monodroid_getifaddrs);
			case 0xe78f1161604ae672: return reinterpret_cast<void*>(&send_uninterrupted);
			case 0xe86307aac9a2631a: return reinterpret_cast<void*>(&_monodroid_weak_gref_new);
			case 0xebc2c68e10075cc9: return reinterpret_cast<void*>(&monodroid_fopen);
			case 0xee83e38e479aeff1: return reinterpret_cast<void*>(&_monodroid_counters_dump);
			case 0xf3048baf83034541: return reinterpret_cast<void*>(&_monodroid_gc_wait_for_bridge_processing);
			case 0xf41c48df6f9be476: return reinterpret_cast<void*>(&monodroid_free);
			case 0xf5a918ef520db207: return reinterpret_cast<void*>(&monodroid_timing_stop);
			case 0xf5ed87b004005892: return reinterpret_cast<void*>(&_monodroid_get_network_interface_supports_multicast);
			case 0xf8798f762db15bba: return reinterpret_cast<void*>(&recv_uninterrupted);
			case 0xfa90326712e7e7c4: return reinterpret_cast<void*>(&java_interop_strdup);
			case 0xfdc17c4ea8335ffd: return reinterpret_cast<void*>(&monodroid_get_namespaced_system_property);
			case 0xff010b3140f54d3f: return reinterpret_cast<void*>(&monodroid_dylib_mono_init);
		}
#endif
		return nullptr;
	}

#if INTPTR_MAX == INT64_MAX
	void find_internal_benchmark ()
	{
		find_entry_benchmark<find_internal_entry> ("Internal", internal_pinvokes);
	}
#endif

	void* find_system_globalization_entry (hash_t entrypoint_hash)
	{
//		log_debug (LOG_ASSEMBLY, "Looking up System.Globalization p/invoke");
#if INTPTR_MAX == INT64_MAX
		switch (entrypoint_hash) {
			case   0x10f8526b1edfc3: return reinterpret_cast<void*>(&GlobalizationNative_GetLocaleInfoInt          );
			case  0xe7e93cf9237e1f2: return reinterpret_cast<void*>(&GlobalizationNative_ToAscii                   );
			case 0x18580a4592ed1ea6: return reinterpret_cast<void*>(&GlobalizationNative_GetSortKey                );
			case 0x1f72f52873ced9c9: return reinterpret_cast<void*>(&GlobalizationNative_InitOrdinalCasingPage     );
			case 0x2178ba302d0c5f1c: return reinterpret_cast<void*>(&GlobalizationNative_GetCalendars              );
			case 0x32e594690358a960: return reinterpret_cast<void*>(&GlobalizationNative_GetLocaleInfoString       );
			case 0x40d61d78487edb08: return reinterpret_cast<void*>(&GlobalizationNative_GetICUVersion             );
			case 0x4360eb8a25122eee: return reinterpret_cast<void*>(&GlobalizationNative_StartsWith                );
			case 0x4bd4b1c0803c8c55: return reinterpret_cast<void*>(&GlobalizationNative_GetLocaleName             );
			case 0x4f22643b9509cc12: return reinterpret_cast<void*>(&GlobalizationNative_IsNormalized              );
			case 0x507983f11ffec7a8: return reinterpret_cast<void*>(&GlobalizationNative_GetTimeZoneDisplayName    );
			case 0x56e982948d00f10d: return reinterpret_cast<void*>(&GlobalizationNative_IndexOf                   );
			case 0x635327a9b09a910d: return reinterpret_cast<void*>(&GlobalizationNative_NormalizeString           );
			case 0x6ac3aeecfc75bfad: return reinterpret_cast<void*>(&GlobalizationNative_GetSortVersion            );
			case 0x77ca6a148e5a51d9: return reinterpret_cast<void*>(&GlobalizationNative_IanaIdToWindowsId         );
			case 0x7a4d912694906c9c: return reinterpret_cast<void*>(&GlobalizationNative_ToUnicode                 );
			case 0x7e5fa2f70891c7fe: return reinterpret_cast<void*>(&GlobalizationNative_ChangeCaseTurkish         );
			case 0xa193402ff5140ac1: return reinterpret_cast<void*>(&GlobalizationNative_GetCalendarInfo           );
			case 0xa831a683f743e417: return reinterpret_cast<void*>(&GlobalizationNative_WindowsIdToIanaId         );
			case 0xac5c6a70d140a4bf: return reinterpret_cast<void*>(&GlobalizationNative_GetLocaleTimeFormat       );
			case 0xb81236cd1fe85cc9: return reinterpret_cast<void*>(&GlobalizationNative_GetLatestJapaneseEra      );
			case 0xb95350c7ec77bc72: return reinterpret_cast<void*>(&GlobalizationNative_ChangeCase                );
			case 0xc287daf58054a21d: return reinterpret_cast<void*>(&GlobalizationNative_EndsWith                  );
			case 0xc8b772178f955d87: return reinterpret_cast<void*>(&GlobalizationNative_GetSortHandle             );
			case 0xd0899515dfe85287: return reinterpret_cast<void*>(&GlobalizationNative_LoadICU                   );
			case 0xd185dfe303ab91dd: return reinterpret_cast<void*>(&GlobalizationNative_CompareString             );
			case 0xd5264d57a926edfb: return reinterpret_cast<void*>(&GlobalizationNative_InitICUFunctions          );
			case 0xd995e71361e6ed2e: return reinterpret_cast<void*>(&GlobalizationNative_IsPredefinedLocale        );
			case 0xe072da8f2d921f53: return reinterpret_cast<void*>(&GlobalizationNative_GetDefaultLocaleName      );
			case 0xea21aa1f2b2a671c: return reinterpret_cast<void*>(&GlobalizationNative_LastIndexOf               );
			case 0xee4dd111dc8d98f3: return reinterpret_cast<void*>(&GlobalizationNative_GetJapaneseEraStartDate   );
			case 0xf2d074e0aeca51ce: return reinterpret_cast<void*>(&GlobalizationNative_GetLocales                );
			case 0xf3693f3cadb9b6f4: return reinterpret_cast<void*>(&GlobalizationNative_EnumCalendarInfo          );
			case 0xf63fa2bfce5c4f80: return reinterpret_cast<void*>(&GlobalizationNative_GetLocaleInfoGroupingSizes);
			case 0xfa21f0a127c9dce9: return reinterpret_cast<void*>(&GlobalizationNative_ChangeCaseInvariant       );
			case 0xfacf02f439426705: return reinterpret_cast<void*>(&GlobalizationNative_CloseSortHandle           );
		}
#endif
		return nullptr;
	}

#if INTPTR_MAX == INT64_MAX
	void find_system_globalization_benchmark ()
	{
		find_entry_benchmark<find_system_globalization_entry> ("System.Globalization", system_globalization_pinvokes);
	}
#endif

	void* find_system_security_cryptography_entry (hash_t entrypoint_hash)
	{
//		log_debug (LOG_ASSEMBLY, "Looking up System.Security.Cryptography p/invoke");
#if INTPTR_MAX == INT64_MAX
		switch (entrypoint_hash) {
			case   0x75a0e90c77ca35: return reinterpret_cast<void*>(&AndroidCryptoNative_EcKeyCreateByExplicitParameters              );
			case  0x47302bd7e277183: return reinterpret_cast<void*>(&AndroidCryptoNative_X509GetCertificateForPrivateKeyEntry         );
			case  0x598db66ca39c41f: return reinterpret_cast<void*>(&AndroidCryptoNative_EcKeyUpRef                                   );
			case  0x656cac62ccc9e3c: return reinterpret_cast<void*>(&AndroidCryptoNative_X509GetContentType                           );
			case  0x690c4347972024f: return reinterpret_cast<void*>(&AndroidCryptoNative_Aes256Gcm                                    );
			case  0x7b5579ab0499b1f: return reinterpret_cast<void*>(&AndroidCryptoNative_RsaSize                                      );
			case  0xcaba893801c6a6f: return reinterpret_cast<void*>(&AndroidCryptoNative_Aes256Ecb                                    );
			case  0xcbe6d3d22131194: return reinterpret_cast<void*>(&AndroidCryptoNative_SetRsaParameters                             );
			case 0x1027786cdd9a3e9c: return reinterpret_cast<void*>(&AndroidCryptoNative_Aes192Cbc                                    );
			case 0x1d1bb0528d517729: return reinterpret_cast<void*>(&AndroidCryptoNative_SSLGetSupportedProtocols                     );
			case 0x1e6228e955989698: return reinterpret_cast<void*>(&AndroidCryptoNative_EcKeyCreateByOid                             );
			case 0x1f45ac9d3c6b1554: return reinterpret_cast<void*>(&AndroidCryptoNative_SSLStreamGetCipherSuite                      );
			case 0x1f7d2360a1cdcbff: return reinterpret_cast<void*>(&AndroidCryptoNative_SSLStreamCreate                              );
			case 0x218fce505a140c55: return reinterpret_cast<void*>(&AndroidCryptoNative_EcDsaVerify                                  );
			case 0x23ac2a4c4d1c744e: return reinterpret_cast<void*>(&AndroidCryptoNative_X509ChainGetCertificateCount                 );
			case 0x267c94097a3bf1f3: return reinterpret_cast<void*>(&AndroidCryptoNative_CipherDestroy                                );
			case 0x2b45d7cdf6e8e0c7: return reinterpret_cast<void*>(&AndroidCryptoNative_X509StoreDeleteEntry                         );
			case 0x2c7e5e179cc917cb: return reinterpret_cast<void*>(&AndroidCryptoNative_DsaSizeSignature                             );
			case 0x2fdcf708ff792105: return reinterpret_cast<void*>(&AndroidCryptoNative_SSLStreamVerifyHostname                      );
			case 0x31027564deeb71b0: return reinterpret_cast<void*>(&AndroidCryptoNative_Aes128Cbc                                    );
			case 0x3f19a16a3230b551: return reinterpret_cast<void*>(&AndroidCryptoNative_ChaCha20Poly1305                             );
			case 0x401935ffc3454bb1: return reinterpret_cast<void*>(&AndroidCryptoNative_X509PublicKey                                );
			case 0x40bfa1211f5f6f9c: return reinterpret_cast<void*>(&AndroidCryptoNative_EcKeyGetCurveName                            );
			case 0x41b6e7f32da99fa9: return reinterpret_cast<void*>(&AndroidCryptoNative_X509ChainDestroyContext                      );
			case 0x41c169fb0e30a390: return reinterpret_cast<void*>(&AndroidCryptoNative_X509ChainGetErrorCount                       );
			case 0x420718c398131a55: return reinterpret_cast<void*>(&AndroidCryptoNative_SSLStreamGetProtocol                         );
			case 0x43741165a5ba60d5: return reinterpret_cast<void*>(&AndroidCryptoNative_CipherUpdateAAD                              );
			case 0x4845e1c76265acc9: return reinterpret_cast<void*>(&AndroidCryptoNative_X509StoreEnumerateCertificates               );
			case 0x4a7272ac9d117f2d: return reinterpret_cast<void*>(&AndroidCryptoNative_EcKeyDestroy                                 );
			case 0x4d6361e5095cff36: return reinterpret_cast<void*>(&AndroidCryptoNative_DsaSign                                      );
			case 0x4d74053b37e582fa: return reinterpret_cast<void*>(&AndroidCryptoNative_X509ChainCreateContext                       );
			case 0x501daf7e3a890220: return reinterpret_cast<void*>(&AndroidCryptoNative_X509ChainBuild                               );
			case 0x52fc107ebdb6fcc7: return reinterpret_cast<void*>(&AndroidCryptoNative_X509StoreRemoveCertificate                   );
			case 0x5fd29ac523ff6e3d: return reinterpret_cast<void*>(&AndroidCryptoNative_SSLStreamRelease                             );
			case 0x5ffae3c8023a80b8: return reinterpret_cast<void*>(&AndroidCryptoNative_SSLStreamGetPeerCertificate                  );
			case 0x648a9b317bc64fe0: return reinterpret_cast<void*>(&AndroidCryptoNative_RsaGenerateKeyEx                             );
			case 0x66e049fe27bf91ea: return reinterpret_cast<void*>(&AndroidCryptoNative_SSLSupportsApplicationProtocolsConfiguration );
			case 0x67a8868ef592a3fd: return reinterpret_cast<void*>(&AndroidCryptoNative_SSLStreamShutdown                            );
			case 0x67a9b5bbce322f8c: return reinterpret_cast<void*>(&AndroidCryptoNative_Des3Cbc                                      );
			case 0x6a59d9242cd31785: return reinterpret_cast<void*>(&AndroidCryptoNative_RsaPrivateDecrypt                            );
			case 0x6dbd90e9cc86310b: return reinterpret_cast<void*>(&AndroidCryptoNative_CipherFinalEx                                );
			case 0x6dfd40c2dd0d7382: return reinterpret_cast<void*>(&AndroidCryptoNative_RsaUpRef                                     );
			case 0x6f990f1f7bc80630: return reinterpret_cast<void*>(&AndroidCryptoNative_RsaCreate                                    );
			case 0x70f907b97d3fe059: return reinterpret_cast<void*>(&AndroidCryptoNative_Aes192Ccm                                    );
			case 0x7150f0eb40797bb3: return reinterpret_cast<void*>(&AndroidCryptoNative_SSLStreamCreateWithCertificates              );
			case 0x7356b141407d261e: return reinterpret_cast<void*>(&AndroidCryptoNative_EcdhDeriveKey                                );
			case 0x74ec4a8d869776ad: return reinterpret_cast<void*>(&AndroidCryptoNative_Aes128Ccm                                    );
			case 0x758dfbf057da0da0: return reinterpret_cast<void*>(&AndroidCryptoNative_DsaSignatureFieldSize                        );
			case 0x7975d1d7029cf1a3: return reinterpret_cast<void*>(&AndroidCryptoNative_Aes128Gcm                                    );
			case 0x79f5c24afbd04af1: return reinterpret_cast<void*>(&AndroidCryptoNative_Aes256Cbc                                    );
			case 0x7a37e0d077f2dfe5: return reinterpret_cast<void*>(&AndroidCryptoNative_DsaGenerateKey                               );
			case 0x7d5273ad530e7298: return reinterpret_cast<void*>(&AndroidCryptoNative_X509StoreOpenDefault                         );
			case 0x7fa96d0284954375: return reinterpret_cast<void*>(&AndroidCryptoNative_X509Decode                                   );
			case 0x813bedf08c3388d4: return reinterpret_cast<void*>(&AndroidCryptoNative_Aes128Cfb8                                   );
			case 0x84cc0301870c37ce: return reinterpret_cast<void*>(&AndroidCryptoNative_SSLStreamSetTargetHost                       );
			case 0x868e09dc7dfea364: return reinterpret_cast<void*>(&AndroidCryptoNative_RsaSignPrimitive                             );
			case 0x870191ad244b8069: return reinterpret_cast<void*>(&AndroidCryptoNative_RegisterRemoteCertificateValidationCallback  );
			case 0x87019b7831c0c34c: return reinterpret_cast<void*>(&AndroidCryptoNative_Aes192Gcm                                    );
			case 0x87c447e7f873cff0: return reinterpret_cast<void*>(&AndroidCryptoNative_X509ChainValidate                            );
			case 0x9039632237d70ae7: return reinterpret_cast<void*>(&AndroidCryptoNative_NewGlobalReference                           );
			case 0x9161ade1206fd86e: return reinterpret_cast<void*>(&AndroidCryptoNative_Aes256Cfb128                                 );
			case 0x9167a072639a7c95: return reinterpret_cast<void*>(&AndroidCryptoNative_Aes256Ccm                                    );
			case 0x91f065ec0d3aec55: return reinterpret_cast<void*>(&AndroidCryptoNative_X509StoreAddCertificateWithPrivateKey        );
			case 0x95a0e2fc5c0cb49e: return reinterpret_cast<void*>(&AndroidCryptoNative_SSLStreamSetApplicationProtocols             );
			case 0x9991a277809ef205: return reinterpret_cast<void*>(&AndroidCryptoNative_SSLStreamGetApplicationProtocol              );
			case 0x9aab07f824659d3e: return reinterpret_cast<void*>(&AndroidCryptoNative_DsaKeyCreateByExplicitParameters             );
			case 0x9e79166979634030: return reinterpret_cast<void*>(&AndroidCryptoNative_CipherSetKeyAndIV                            );
			case 0x9edddf30d660eff4: return reinterpret_cast<void*>(&AndroidCryptoNative_Aes192Ecb                                    );
			case 0xa308025a784497df: return reinterpret_cast<void*>(&AndroidCryptoNative_SSLStreamSetEnabledProtocols                 );
			case 0xa56954e28eb9a9c9: return reinterpret_cast<void*>(&AndroidCryptoNative_Des3Cfb8                                     );
			case 0xa5eda72b95fe78c3: return reinterpret_cast<void*>(&AndroidCryptoNative_X509ChainGetErrors                           );
			case 0xa93eb533acf7564d: return reinterpret_cast<void*>(&AndroidCryptoNative_DesEcb                                       );
			case 0xa961e8db31830e16: return reinterpret_cast<void*>(&AndroidCryptoNative_Aes192Cfb8                                   );
			case 0xaa8f0f87ae474ffe: return reinterpret_cast<void*>(&AndroidCryptoNative_SSLStreamCreateWithKeyStorePrivateKeyEntry   );
			case 0xad1a2d6575cdd4e3: return reinterpret_cast<void*>(&AndroidCryptoNative_SSLStreamWrite                               );
			case 0xae82e9ceae24192d: return reinterpret_cast<void*>(&AndroidCryptoNative_Pbkdf2                                       );
			case 0xb0df46ff09c57741: return reinterpret_cast<void*>(&AndroidCryptoNative_GetRsaParameters                             );
			case 0xb1c394b9992bd67d: return reinterpret_cast<void*>(&AndroidCryptoNative_EcDsaSign                                    );
			case 0xb1ff12f3bd735982: return reinterpret_cast<void*>(&AndroidCryptoNative_AeadCipherFinalEx                            );
			case 0xb4996dd1aba38200: return reinterpret_cast<void*>(&AndroidCryptoNative_EcDsaSize                                    );
			case 0xb575ec01a7a79f8f: return reinterpret_cast<void*>(&AndroidCryptoNative_DesCfb8                                      );
			case 0xb66be1550d27bfb4: return reinterpret_cast<void*>(&AndroidCryptoNative_GetECCurveParameters                         );
			case 0xbd5a0be2f7904089: return reinterpret_cast<void*>(&AndroidCryptoNative_X509StoreAddCertificate                      );
			case 0xbdbbd2898347c0d1: return reinterpret_cast<void*>(&AndroidCryptoNative_SSLStreamHandshake                           );
			case 0xc11cd661db8be230: return reinterpret_cast<void*>(&AndroidCryptoNative_Des3Cfb64                                    );
			case 0xc2d5e1c465b2f5b6: return reinterpret_cast<void*>(&AndroidCryptoNative_DsaSizeP                                     );
			case 0xc3145e336c38379b: return reinterpret_cast<void*>(&AndroidCryptoNative_SSLStreamGetPeerCertificates                 );
			case 0xc7815e0476511544: return reinterpret_cast<void*>(&AndroidCryptoNative_X509Encode                                   );
			case 0xc8a52a8b6d96b32b: return reinterpret_cast<void*>(&AndroidCryptoNative_X509ExportPkcs7                              );
			case 0xca48c3927c202794: return reinterpret_cast<void*>(&AndroidCryptoNative_GetECKeyParameters                           );
			case 0xcb4bcdafdc81d116: return reinterpret_cast<void*>(&AndroidCryptoNative_CipherCreatePartial                          );
			case 0xcc433093c073719e: return reinterpret_cast<void*>(&AndroidCryptoNative_SSLStreamRead                                );
			case 0xce9f8a6ac705faa5: return reinterpret_cast<void*>(&AndroidCryptoNative_X509DecodeCollection                         );
			case 0xd5c063a90ae882c1: return reinterpret_cast<void*>(&AndroidCryptoNative_CipherIsSupported                            );
			case 0xd7d818c7640598dc: return reinterpret_cast<void*>(&AndroidCryptoNative_X509ChainGetCertificates                     );
			case 0xd7f1a8f616897ace: return reinterpret_cast<void*>(&AndroidCryptoNative_Aes256Cfb8                                   );
			case 0xd9bd0b370726ce34: return reinterpret_cast<void*>(&AndroidCryptoNative_CipherReset                                  );
			case 0xda4898a26933f73d: return reinterpret_cast<void*>(&AndroidCryptoNative_DsaVerify                                    );
			case 0xdbb4752ed23670f0: return reinterpret_cast<void*>(&AndroidCryptoNative_DesCbc                                       );
			case 0xdc780005b0d39711: return reinterpret_cast<void*>(&AndroidCryptoNative_X509StoreGetPrivateKeyEntry                  );
			case 0xdd4c03f06ce96e04: return reinterpret_cast<void*>(&AndroidCryptoNative_RsaDestroy                                   );
			case 0xdde06993f87d6ffc: return reinterpret_cast<void*>(&AndroidCryptoNative_Aes128Cfb128                                 );
			case 0xde1e22dd097f799c: return reinterpret_cast<void*>(&AndroidCryptoNative_CipherSetNonceLength                         );
			case 0xde259001bf54e6f1: return reinterpret_cast<void*>(&AndroidCryptoNative_SSLStreamIsLocalCertificateUsed              );
			case 0xdec5c7544d2c8cb1: return reinterpret_cast<void*>(&AndroidCryptoNative_GetDsaParameters                             );
			case 0xdfede2defd776f7e: return reinterpret_cast<void*>(&AndroidCryptoNative_X509ChainSetCustomTrustStore                 );
			case 0xe059239741e0011a: return reinterpret_cast<void*>(&AndroidCryptoNative_EcKeyCreateByKeyParameters                   );
			case 0xe0f34ce89fd38aef: return reinterpret_cast<void*>(&AndroidCryptoNative_RsaPublicEncrypt                             );
			case 0xe604fca300068c0c: return reinterpret_cast<void*>(&AndroidCryptoNative_CipherCtxSetPadding                          );
			case 0xeab45239fb3f138d: return reinterpret_cast<void*>(&AndroidCryptoNative_GetBigNumBytes                               );
			case 0xeff5d014640ae969: return reinterpret_cast<void*>(&AndroidCryptoNative_DeleteGlobalReference                        );
			case 0xf1577384f409ea85: return reinterpret_cast<void*>(&AndroidCryptoNative_BigNumToBinary                               );
			case 0xf4dea312f71c5ff2: return reinterpret_cast<void*>(&AndroidCryptoNative_Aes128Ecb                                    );
			case 0xf57f81262f07542c: return reinterpret_cast<void*>(&AndroidCryptoNative_Des3Ecb                                      );
			case 0xf7b334768844b502: return reinterpret_cast<void*>(&AndroidCryptoNative_X509StoreContainsCertificate                 );
			case 0xf85b8ffeba9b06c1: return reinterpret_cast<void*>(&AndroidCryptoNative_X509StoreEnumerateTrustedCertificates        );
			case 0xf96bc1e7e15e69f2: return reinterpret_cast<void*>(&AndroidCryptoNative_CipherUpdate                                 );
			case 0xf970881d4fa83e07: return reinterpret_cast<void*>(&AndroidCryptoNative_CipherCreate                                 );
			case 0xf9c3d216226b3355: return reinterpret_cast<void*>(&AndroidCryptoNative_CipherSetTagLength                           );
			case 0xfa2669c25616a8ff: return reinterpret_cast<void*>(&AndroidCryptoNative_X509IsKeyStorePrivateKeyEntry                );
			case 0xfaa7766eaa2c54a5: return reinterpret_cast<void*>(&AndroidCryptoNative_DecodeRsaSubjectPublicKeyInfo                );
			case 0xfc0bad2b1528000f: return reinterpret_cast<void*>(&AndroidCryptoNative_RsaVerificationPrimitive                     );
			case 0xfcdeea476953780c: return reinterpret_cast<void*>(&AndroidCryptoNative_Aes192Cfb128                                 );
			case 0xfd2cdd99f11de76c: return reinterpret_cast<void*>(&AndroidCryptoNative_EcKeyGetSize                                 );
			case 0xfd4f2784ec1c98aa: return reinterpret_cast<void*>(&AndroidCryptoNative_SSLStreamRequestClientAuthentication         );
			case 0xfe3dd06281f7cd1f: return reinterpret_cast<void*>(&AndroidCryptoNative_SSLStreamInitialize                          );
		}
#endif
		return nullptr;
	}

#if INTPTR_MAX == INT64_MAX
	void find_system_security_cryptography_benchmark ()
	{
		find_entry_benchmark<find_system_security_cryptography_entry> ("System.Security.Cryptography", system_crypto_android_pinvokes);
	}
#endif

	void* find_system_io_compression_entry (hash_t entrypoint_hash)
	{
//		log_debug (LOG_ASSEMBLY, "Looking up System.IO.Compression p/invoke");

#if INTPTR_MAX == INT64_MAX
		switch (entrypoint_hash) {
			case   0x99f2ee02463000: return reinterpret_cast<void*>(&CompressionNative_Crc32         );
			case 0x403e1bc0b3baba84: return reinterpret_cast<void*>(&CompressionNative_Inflate       );
			case 0xafe3d21bbaa71464: return reinterpret_cast<void*>(&CompressionNative_DeflateEnd    );
			case 0xc10e411c989a9314: return reinterpret_cast<void*>(&CompressionNative_Deflate       );
			case 0xca001af79c0d7a8b: return reinterpret_cast<void*>(&CompressionNative_InflateEnd    );
			case 0xcd5d8a63493f5e38: return reinterpret_cast<void*>(&CompressionNative_InflateInit2_ );
			case 0xea5e6653389b924a: return reinterpret_cast<void*>(&CompressionNative_DeflateInit2_ );
		}
#endif
		return nullptr;
	}

#if INTPTR_MAX == INT64_MAX
	void find_system_io_compression_benchmark ()
	{
		find_entry_benchmark<find_system_io_compression_entry> ("System.IO.Compression", system_io_compression_pinvokes);
	}
#endif

	extern "C"
	[[gnu::noinline]]
	void* find_pinvoke (hash_t library_name_hash, hash_t entrypoint_hash, bool& known_library)
	{
		// Order of `case` statements should be roughly sorted by the (projected) frequency of calls from the
		// managed land.
		switch (library_name_hash) {
			// `libSystem.Native` and `xa-internal-api` are both used during startup, put them first
			case system_native_library_hash:
				return find_system_native_entry (entrypoint_hash);

			case xa_internal_api_library_hash:
			case java_interop_library_hash:
				return find_internal_entry (entrypoint_hash);

			case system_globalization_native_library_hash:
				return find_system_globalization_entry (entrypoint_hash);

			case system_security_cryptography_native_android_library_hash:
				return find_system_security_cryptography_entry (entrypoint_hash);

			case system_io_compression_native_library_hash:
				return find_system_io_compression_entry (entrypoint_hash);
		}

		known_library = false;
		return nullptr;
	}
}

namespace {
	bool benchmarks_done = false;
}

[[gnu::flatten]]
void*
PinvokeOverride::monodroid_pinvoke_override (const char *library_name, const char *entrypoint_name)
{
	log_debug (LOG_ASSEMBLY, __PRETTY_FUNCTION__);
	log_debug (LOG_ASSEMBLY, "library_name == '%s'; entrypoint_name == '%s'", library_name, entrypoint_name);

	bool known_library = true;
#if INTPTR_MAX == INT64_MAX
	if (!benchmarks_done) {
		benchmarks_done = true;

		if (FastTiming::enabled ()) {
			find_system_io_compression_benchmark ();
			find_system_globalization_benchmark ();
			find_internal_benchmark ();
			find_system_security_cryptography_benchmark ();
			find_system_native_entry_benchmark ();
		}
	}
	known_library = true;
#endif

	if (library_name == nullptr || entrypoint_name == nullptr) {
        return nullptr;
    }

	hash_t library_name_hash = xxhash::hash (library_name, strlen (library_name));
    hash_t entrypoint_hash = xxhash::hash (entrypoint_name, strlen (entrypoint_name));
	log_debug (LOG_ASSEMBLY, "library_name_hash == 0x%zx; entrypoint_hash == 0x%zx", library_name_hash, entrypoint_hash);

	//bool known_library = true;
	void *pinvoke_ptr = find_pinvoke (library_name_hash, entrypoint_hash, known_library);
	if (pinvoke_ptr != nullptr) {
		return pinvoke_ptr;
	}

	if (known_library) [[unlikely]] {
		log_debug (LOG_ASSEMBLY, "Lookup in a known library == internal");
		// Should "never" happen.  It seems we have a known library hash (of one that's linked into the dynamically
		// built DSO) but an unknown symbol hash.  The symbol **probably** doesn't exist (was most likely linked out if
		// the find* functions didn't know its hash), but we cannot be sure of that so we'll try to load it.
		pinvoke_ptr = dlsym (RTLD_DEFAULT, entrypoint_name);
		if (pinvoke_ptr == nullptr) {
			log_warn (LOG_ASSEMBLY, "Unable to load p/invoke entry '%s/%s' from the unified runtime DSO", library_name, entrypoint_name);
		}

		return pinvoke_ptr;
	}

	log_debug (LOG_ASSEMBLY, "p/invoke not from a known library, slow path taken.");
	return handle_other_pinvoke_request (library_name, library_name_hash, entrypoint_name, entrypoint_hash);;
}

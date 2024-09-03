#include <dlfcn.h>

#include "internal-pinvokes.hh"
#include "logger.hh"

#define PINVOKE_OVERRIDE_INLINE [[gnu::noinline]]
#include "pinvoke-override-api-impl.hh"

using namespace xamarin::android;

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
}

namespace {
	//
	// These functions will eventually reside in the generated portion of code
	//
	void* find_system_native_entry (hash_t entrypoint_hash)
	{
		log_debug (LOG_ASSEMBLY, "Looking up System.Native p/invoke");
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

	void* find_internal_entry (hash_t entrypoint_hash)
	{
		log_debug (LOG_ASSEMBLY, "Looking up internal p/invoke");

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

	void* find_system_globalization_entry (hash_t entrypoint_hash)
	{
		log_debug (LOG_ASSEMBLY, "Looking up System.Globalization p/invoke");
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

	void* find_system_security_cryptography_entry (hash_t entrypoint_hash)
	{
		log_debug (LOG_ASSEMBLY, "Looking up System.Security.Cryptography p/invoke");
		return nullptr;
	}

	void* find_system_io_compression_entry (hash_t entrypoint_hash)
	{
		log_debug (LOG_ASSEMBLY, "Looking up System.IO.Compression p/invoke");
		return nullptr;
	}

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

[[gnu::flatten]]
void*
PinvokeOverride::monodroid_pinvoke_override (const char *library_name, const char *entrypoint_name)
{
	log_debug (LOG_ASSEMBLY, __PRETTY_FUNCTION__);
	log_debug (LOG_ASSEMBLY, "library_name == '%s'; entrypoint_name == '%s'", library_name, entrypoint_name);

	if (library_name == nullptr || entrypoint_name == nullptr) {
        return nullptr;
    }

	hash_t library_name_hash = xxhash::hash (library_name, strlen (library_name));
    hash_t entrypoint_hash = xxhash::hash (entrypoint_name, strlen (entrypoint_name));
	log_debug (LOG_ASSEMBLY, "library_name_hash == 0x%zx; entrypoint_hash == 0x%zx", library_name_hash, entrypoint_hash);

	bool known_library = true;
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

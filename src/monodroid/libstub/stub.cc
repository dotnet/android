#if !defined (STUB_LIB_NAME)
#error STUB_LIB_NAME must be defined on command line
#endif

void STUB_LIB_NAME ()
{
	// no-op
}

#if defined(IN_LIBC)
extern "C" {
	[[gnu::weak]] int puts ([[maybe_unused]] const char *s)
	{
		return -1;
	}

	[[gnu::weak]] void abort ()
	{
		// no-op
	} 
}
#endif

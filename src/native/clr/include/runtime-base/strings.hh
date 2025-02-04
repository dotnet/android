#pragma once

#include <array>
#include <cstring>
#include <cerrno>
#include <expected>
#include <limits>
#include <string>
#include <string_view>
#include <type_traits>
#include <unistd.h>

#include "../shared/helpers.hh"
#include "../constants.hh"

namespace xamarin::android {
	static constexpr size_t SENSIBLE_TYPE_NAME_LENGTH = 128uz;
	static constexpr size_t SENSIBLE_PATH_MAX = 256uz;

#if DEBUG
	static constexpr bool BoundsCheck = true;
#else
	static constexpr bool BoundsCheck = false;
#endif

	enum class string_segment_error
	{
		index_out_of_range,
	};

	static inline auto to_string (string_segment_error error) -> std::string_view const
	{
		using std::operator""sv;

		switch (error) {
			case string_segment_error::index_out_of_range:
				return "Index out of range"sv;

			default:
				return "Unknown error"sv;
		}
	}

	class string_segment
	{
	public:
		[[gnu::always_inline]]
		auto initialized () const noexcept -> bool
		{
			return !_fresh;
		}

		[[gnu::always_inline]]
		auto start () const noexcept -> const char*
		{
			return _start;
		}

		[[gnu::always_inline]]
		auto at (size_t offset) const noexcept -> std::expected<const char*, string_segment_error>
		{
			if (offset >= length ()) {
				return std::unexpected (string_segment_error::index_out_of_range);
			}

			return _start + offset;
		}

		[[gnu::always_inline]]
		auto length () const noexcept -> size_t
		{
			return _length;
		}

		[[gnu::always_inline]]
		auto empty () const noexcept -> bool
		{
			return length () == 0;
		}

		[[gnu::always_inline]]
		auto equal (const char *s) const noexcept -> bool
		{
			if (s == nullptr) {
				return false;
			}

			return equal (s, strlen (s));
		}

		[[gnu::always_inline]]
		auto equal (const char *s, size_t s_length) const noexcept -> bool
		{
			if (s == nullptr) {
				return false;
			}

			if (length () != s_length) {
				return false;
			}

			if (!can_access (s_length)) [[unlikely]] {
				return false;
			}

			if (length () == 0) {
				return true;
			}

			return memcmp (_start, s, length ()) == 0;
		}

		template<size_t Size> [[gnu::always_inline]]
		auto equal (const char (&s)[Size]) noexcept -> bool
		{
			return equal (s, Size - 1);
		}

		[[gnu::always_inline]]
		auto equal (std::string_view const& s) noexcept -> bool
		{
			return equal (s.data (), s.length ());
		}

		[[gnu::always_inline]]
		auto starts_with_c (const char *s) const noexcept -> bool
		{
			if (s == nullptr) {
				return false;
			}

			return starts_with (s, strlen (s));
		}

		[[gnu::always_inline]]
		auto starts_with (const char *s, size_t s_length) const noexcept -> bool
		{
			if (s == nullptr || !can_access (s_length)) {
				return false;
			}

			if (length () < s_length) {
				return false;
			}

			return memcmp (start (), s, s_length) == 0;
		}

		template<size_t Size> [[gnu::always_inline]]
		auto starts_with (const char (&s)[Size]) const noexcept -> bool
		{
			return starts_with (s, Size - 1);
		}

		[[gnu::always_inline]]
		auto starts_with (std::string_view const& s) const noexcept -> bool
		{
			return starts_with (s.data (), s.length ());
		}

		[[gnu::always_inline]]
		auto has_at (const char ch, size_t index) const noexcept -> bool
		{
			if (!can_access (index)) {
				return false;
			}

			return start ()[index] == ch;
		}

		[[gnu::always_inline]]
		auto find (const char ch, size_t start_index) const noexcept -> ssize_t
		{
			if (!can_access (start_index)) {
				return -1;
			}

			while (start_index <= length ()) {
				if (start ()[start_index] == ch) {
					return static_cast<ssize_t>(start_index);
				}
				start_index++;
			}

			return -1;
		}

		template<typename T> [[gnu::always_inline]]
		auto to_integer (T &val, size_t start_index = 0uz, int base = 10) const noexcept -> bool
		{
			static_assert (std::is_integral_v<T>);
			constexpr T min = std::numeric_limits<T>::min ();
			constexpr T max = std::numeric_limits<T>::max ();
			using integer = typename std::conditional<std::is_signed_v<T>, int64_t, uint64_t>::type;

			if (length () == 0uz) {
				return false;
			}

			if (!can_access (start_index)) {
				log_error (LOG_DEFAULT, "Cannot convert string to integer, index {} is out of range", start_index);
				return false;
			}

			// FIXME: this is less than ideal... we shouldn't need another buffer here
			size_t slen = length () - start_index;
			char s[slen + 1];

			memcpy (s, start () + start_index, slen);
			s[slen] = '\0';

			integer ret;
			char *endp;
			bool out_of_range;
			errno = 0;
			if constexpr (std::is_signed_v<T>) {
				ret = strtoll (s, &endp, base);
				out_of_range = ret < min || ret > max;
			} else {
				ret = strtoull (s, &endp, base);
				out_of_range = ret > max;
			}

			if (out_of_range || errno == ERANGE) {
				log_error (LOG_DEFAULT, "Value {} is out of range of this type ({}..{})", reinterpret_cast<char*>(s), static_cast<int64_t>(min), static_cast<uint64_t>(max));
				return false;
			}

			if (endp == s) {
				log_error (LOG_DEFAULT, "Value {} does not represent a base {} integer", reinterpret_cast<char*>(s), base);
				return false;
			}

			if (*endp != '\0') {
				log_error (LOG_DEFAULT, "Value {} has non-numeric characters at the end", reinterpret_cast<char*>(s));
				return false;
			}

			val = static_cast<T>(ret);
			return true;
		}

	private:
		[[gnu::always_inline]]
		auto can_access (size_t index) const noexcept -> bool
		{
			if (!initialized () || start () == nullptr) [[unlikely]] {
				return false;
			}

			if (index > length ()) {
				return false;
			}

			return true;
		}

	protected:
		size_t       _last_index = 0uz;
		bool         _fresh = true;
		const char  *_start = nullptr;
		size_t       _length = 0uz;

		template<size_t MaxStackSize, typename TStorage, typename TChar> friend class string_base;
	};

	template<size_t MaxStackSize, bool HasResize, typename T = char>
	class local_storage
	{
		protected:
		using LocalStoreArray = std::array<T, MaxStackSize>;

	public:
		static constexpr bool has_resize = HasResize;

	public:
		explicit local_storage (size_t size) noexcept
		{
			static_assert (MaxStackSize > 0, "MaxStackSize must be more than 0");
			init_store (size < MaxStackSize ? MaxStackSize : size);
		}

		virtual ~local_storage ()
		{
			free_store ();
		}

		auto get () noexcept -> T*
		{
			return allocated_store == nullptr ? local_store.data () : allocated_store;
		}

		auto get () const noexcept -> const T*
		{
			return allocated_store == nullptr ? local_store.data () : allocated_store;
		}

		auto size () const noexcept -> size_t
		{
			return store_size;
		}

	protected:
		[[gnu::always_inline]]
		void init_store (size_t new_size) noexcept
		{
			if (new_size > MaxStackSize) {
				allocated_store = new T[new_size];
			} else {
				allocated_store = nullptr;
			}
			store_size = new_size;
		}

		[[gnu::always_inline]]
		void free_store () noexcept
		{
			if (allocated_store == nullptr) {
				return;
			}

			delete[] allocated_store;
		}

		[[gnu::always_inline]]
		auto get_local_store () noexcept -> LocalStoreArray&
		{
			return local_store;
		}

		[[gnu::always_inline]]
		auto get_allocated_store () noexcept -> T*
		{
			return allocated_store;
		}

	private:
		size_t store_size;
		LocalStoreArray local_store;
		T* allocated_store;
	};

	// This class is meant to provide a *very* thin veneer (by design) over space allocated for *local* buffers - that
	// is one which are not meant to survive the exit of the function they are created in.  The idea is that the caller
	// knows the size of the buffer they need and they want to put the buffer on stack, if it doesn't exceed a certain
	// value, or dynamically allocate memory if more is needed.  Even though the class could be used on its own, it's
	// really meant to be a base for more specialized buffers.  There are not many safeguards, by design - the code is
	// meant to be performant.  This is the reason why the size is static throughout the life of the object, so that we
	// can perform as few checks as possible.

	template <size_t MaxStackSize, typename T> using static_local_storage_base = local_storage<MaxStackSize, false, T>;

	template<size_t MaxStackSize, typename T = char>
	class static_local_storage final : public static_local_storage_base<MaxStackSize, T>
	{
		using base = static_local_storage_base<MaxStackSize, T>;

	public:
		explicit static_local_storage (size_t initial_size) noexcept
			: base (initial_size)
		{}
	};

	template <size_t MaxStackSize, typename T> using dynamic_local_storage_base = local_storage<MaxStackSize, true, T>;

	template<size_t MaxStackSize, typename T = char>
	class dynamic_local_storage final : public dynamic_local_storage_base<MaxStackSize, T>
	{
		using base = dynamic_local_storage_base<MaxStackSize, T>;

	public:
		explicit dynamic_local_storage (size_t initial_size = 0uz) noexcept
			: base (initial_size)
		{}

		//
		// If `new_size` is smaller than the current size and the dynamic store is allocated, data WILL NOT be
		// preserved.
		//
		// If `new_size` is bigger than the current size bigger than MaxStackSize, data will be copied to the new
		// dynamically allocated store
		//
		// If `new_size` is smaller or equal to `MaxStackSize`, no changes are made unless dynamic store was allocated,
		// in which case it will be freed
		//
		void resize (size_t new_size) noexcept
		{
			size_t old_size = base::size ();

			if (new_size == old_size) {
				return;
			}

			if (new_size <= MaxStackSize) {
				new_size = MaxStackSize;
				base::free_store ();
				return;
			}

			if (new_size < old_size) {
				base::free_store ();
				base::init_store (new_size);
				return;
			}

			T* old_allocated_store = base::get_allocated_store ();
			base::init_store (new_size);

			T* new_allocated_store = base::get_allocated_store ();
			if (old_allocated_store != nullptr) {
				std::memcpy (new_allocated_store, old_allocated_store, old_size);
				delete[] old_allocated_store;
				return;
			}

			std::memcpy (new_allocated_store, base::get_local_store ().data (), MaxStackSize);
		}
	};

	template<size_t MaxStackSize, typename TStorage, typename TChar = char>
	class string_base
	{
	protected:
		static constexpr TChar NUL = '\0';
		static constexpr TChar ZERO = '0';

	public:
		explicit string_base (size_t initial_size = 0uz)
			: buffer (initial_size)
		{
			// Currently we care only about `char`, maybe in the future we'll add support for `wchar` (if needed)
			static_assert (std::is_same_v<TChar, char>, "TChar must be an 8-bit character type");

			clear ();
		}

		explicit string_base (const string_segment &token)
			: string_base (token.initialized () ? token.length () : 0)
		{
			if (token.initialized ()) {
				assign (token.start (), token.length ());
			}
		}

		[[gnu::always_inline]]
		auto length () const noexcept -> size_t
		{
			return idx;
		}

		[[gnu::always_inline]]
		auto empty () const noexcept -> bool
		{
			return length () == 0;
		}

		[[gnu::always_inline]]
		void set_length (size_t new_length) noexcept
		{
			if (new_length >= buffer.size ()) {
				return;
			}

			idx = new_length;
			terminate ();
		}

		[[gnu::always_inline]]
		void clear () noexcept
		{
			set_length (0);
			buffer.get ()[0] = NUL;
		}

		[[gnu::always_inline]]
		void terminate () noexcept
		{
			buffer.get ()[idx] = NUL;
		}

		[[gnu::always_inline]]
		auto replace (const TChar c1, const TChar c2) noexcept -> string_base&
		{
			if (empty ()) {
				return *this;
			}

			for (size_t i = 0uz; i < length (); i++) {
				if (buffer.get ()[i] == c1) {
					buffer.get ()[i] = c2;
				}
			}

			return *this;
		}

		[[gnu::always_inline]]
		auto append (const TChar* s, size_t length) noexcept -> string_base&
		{
			if (s == nullptr || length == 0uz) {
				return *this;
			}

			resize_for_extra (length);
			if constexpr (BoundsCheck) {
				ensure_have_extra (length);
			}

			std::memcpy (buffer.get () + idx, s, length);
			idx += length;
			buffer.get ()[idx] = NUL;

			return *this;
		}

		template<size_t LocalMaxStackSize, typename LocalTStorage, typename LocalTChar = char>
		[[gnu::always_inline]]
		auto append (string_base<LocalMaxStackSize, LocalTStorage, LocalTChar> const& str) noexcept -> string_base&
		{
			return append (str.get (), str.length ());
		}

		[[gnu::always_inline]]
		auto append (std::string_view const& sv) noexcept -> string_base&
		{
			return append (sv.data (), sv.length ());
		}

		template<size_t Size> [[gnu::always_inline]]
		auto append (const char (&s)[Size]) noexcept -> string_base&
		{
			return append (s, Size - 1);
		}

		[[gnu::always_inline]]
		auto append_c (const char *s) noexcept -> string_base&
		{
			if (s == nullptr) {
				return *this;
			}

			return append (s, strlen (s));
		}

		[[gnu::always_inline]]
		auto append (int16_t i) noexcept -> string_base&
		{
			resize_for_extra (Constants::MAX_INTEGER_DIGIT_COUNT_BASE10);
			append_integer (buffer.get (), i);
			return *this;
		}

		[[gnu::always_inline]]
		auto append (uint16_t i) noexcept -> string_base&
		{
			resize_for_extra (Constants::MAX_INTEGER_DIGIT_COUNT_BASE10);
			append_integer (i);
			return *this;
		}

		[[gnu::always_inline]]
		auto append (int32_t i) noexcept -> string_base&
		{
			resize_for_extra (Constants::MAX_INTEGER_DIGIT_COUNT_BASE10);
			append_integer (i);
			return *this;
		}

		[[gnu::always_inline]]
		auto append (uint32_t i) noexcept -> string_base&
		{
			resize_for_extra (Constants::MAX_INTEGER_DIGIT_COUNT_BASE10);
			append_integer (i);
			return *this;
		}

		[[gnu::always_inline]]
		auto append (int64_t i) noexcept -> string_base&
		{
			resize_for_extra (Constants::MAX_INTEGER_DIGIT_COUNT_BASE10);
			append_integer (i);
			return *this;
		}

		[[gnu::always_inline]]
		auto append (uint64_t i) noexcept -> string_base&
		{
			resize_for_extra (Constants::MAX_INTEGER_DIGIT_COUNT_BASE10);
			append_integer (i);
			return *this;
		}

		[[gnu::always_inline]]
		auto assign (const TChar* s, size_t length) noexcept -> string_base&
		{
			idx = 0;
			return append (s, length);
		}

		[[gnu::always_inline]]
		auto assign_c (const TChar* s) noexcept -> string_base&
		{
			if (s == nullptr) {
				return *this;
			}

			return assign (s, strlen (s));
		}

		[[gnu::always_inline]]
		auto assign (std::string_view const& sv) noexcept -> string_base&
		{
			return assign (sv.data (), sv.size ());
		}

		[[gnu::always_inline]]
		auto assign (std::string const& s) noexcept -> string_base&
		{
			return assign (s.data (), s.size ());
		}

		template<size_t Size> [[gnu::always_inline]]
		auto assign (const char (&s)[Size]) noexcept -> string_base&
		{
			return assign (s, Size - 1);
		}

		template<size_t LocalMaxStackSize, typename LocalTStorage, typename LocalTChar = char>
		[[gnu::always_inline]]
		auto assign (string_base<LocalMaxStackSize, LocalTStorage, LocalTChar> const& str) noexcept -> string_base&
		{
			return assign (str.get (), str.length ());
		}

		[[gnu::always_inline]]
		auto assign (const TChar* s, size_t offset, size_t count) noexcept -> string_base&
		{
			if (s == nullptr) {
				return *this;
			}

			if constexpr (BoundsCheck) {
				size_t slen = strlen (s);
				if (offset + count > slen) {
					Helpers::abort_application ("Attempt to assign data from a string exceeds the source string length");
				}
			}

			return assign (s + offset, count);
		}

		[[gnu::always_inline]]
		auto next_token (size_t start_index, const TChar separator, string_segment& token) const noexcept -> bool
		{
			size_t index;
			if (token._fresh) {
				token._fresh = false;
				token._last_index = start_index;
				index = start_index;
			} else {
				index = token._last_index + 1;
			}

			token._start = nullptr;
			token._length = 0;
			if (token._last_index + 1 >= buffer.size ()) {
				return false;
			}

			const TChar *start = buffer.get () + index;
			const TChar *p = start;
			while (*p != NUL) {
				if (*p == separator) {
					break;
				}
				p++;
				index++;
			}

			token._last_index = *p == NUL ? buffer.size () : index;
			token._start = start;
			token._length = static_cast<size_t>(p - start);

			return true;
		}

		[[gnu::always_inline]]
		auto next_token (const char separator, string_segment& token) const noexcept -> bool
		{
			return next_token (0, separator, token);
		}

		[[gnu::always_inline]]
		auto index_of (const TChar ch) const noexcept -> ssize_t
		{
			const TChar *p = buffer.get ();
			while (p != nullptr && *p != NUL) {
				if (*p == ch) {
					return static_cast<ssize_t>(p - buffer.get ());
				}
				p++;
			}

			return -1;
		}

		[[gnu::always_inline]]
		auto starts_with (const TChar *s, size_t s_length) const noexcept -> bool
		{
			if (s == nullptr || s_length == 0 || s_length > buffer.size ()) {
				return false;
			}

			return memcmp (buffer.get (), s, s_length) == 0;
		}

		[[gnu::always_inline]]
		auto starts_with_c (const char* s) noexcept -> bool
		{
			if (s == nullptr) {
				return false;
			}

			return starts_with (s, strlen (s));
		}

		template<size_t Size> [[gnu::always_inline]]
		auto starts_with (const char (&s)[Size]) noexcept -> bool
		{
			return starts_with (s, Size - 1);
		}

		[[gnu::always_inline]]
		auto starts_with (std::string_view const& s) noexcept -> bool
		{
			return starts_with (s.data (), s.length ());
		}

		[[gnu::always_inline]]
		void set_length_after_direct_write (size_t new_length) noexcept
		{
			set_length (new_length);
			terminate ();
		}

		[[gnu::always_inline]]
		void set_at (size_t index, const TChar ch) noexcept
		{
			ensure_valid_index (index);
			TChar *p = buffer + index;
			if (*p == NUL) {
				return;
			}

			*p = ch;
		}

		[[gnu::always_inline]]
		auto get_at (size_t index) const noexcept -> const TChar
		{
			ensure_valid_index (index);
			return *(buffer.get () + index);
		}

		[[gnu::always_inline]]
		auto get_at (size_t index) noexcept -> TChar&
		{
			ensure_valid_index (index);
			return *(buffer.get () + index);
		}

		[[gnu::always_inline]]
		auto get () const noexcept -> const TChar*
		{
			return buffer.get ();
		}

		[[gnu::always_inline]]
		auto get () noexcept -> TChar*
		{
			return buffer.get ();
		}

		[[gnu::always_inline]]
		auto size () const noexcept -> size_t
		{
			return buffer.size ();
		}

		[[gnu::always_inline]]
		auto operator[] (size_t index) const noexcept -> char
		{
			return get_at (index);
		}

		[[gnu::always_inline]]
		auto operator[] (size_t index) noexcept -> char&
		{
			return get_at (index);
		}

	protected:
		template<typename Integer> [[gnu::always_inline]]
		void append_integer (Integer i) noexcept
		{
			static_assert (std::is_integral_v<Integer>);

			resize_for_extra (Constants::MAX_INTEGER_DIGIT_COUNT_BASE10);
			if constexpr (BoundsCheck) {
				ensure_have_extra (Constants::MAX_INTEGER_DIGIT_COUNT_BASE10);
			}

			if (i == 0) {
				constexpr char zero[] = "0";
				constexpr size_t zero_len = sizeof(zero) - 1uz;

				append (zero, zero_len);
				return;
			}

			TChar temp_buf[Constants::MAX_INTEGER_DIGIT_COUNT_BASE10 + 1uz];
			TChar *p = temp_buf + Constants::MAX_INTEGER_DIGIT_COUNT_BASE10;
			*p = NUL;
			TChar *end = p;

			uint32_t x;
			if constexpr (sizeof(Integer) > 4uz) {
				uint64_t y;

				if constexpr (std::is_signed_v<Integer>) {
					y = static_cast<uint64_t>(i > 0 ? i : -i);
				} else {
					y = static_cast<uint64_t>(i);
				}
				while (y > std::numeric_limits<uint32_t>::max ()) {
					*--p = (y % 10) + ZERO;
					y /= 10;
				}
				x = static_cast<uint32_t>(y);
			} else {
				if constexpr (std::is_signed_v<Integer>) {
					x = static_cast<uint32_t>(i > 0 ? i : -i);
				} else {
					x = static_cast<uint32_t>(i);
				}
			}

			while (x > 0) {
				*--p = (x % 10) + ZERO;
				x /= 10;
			}

			if constexpr (std::is_signed_v<Integer>) {
				if (i < 0)
					*--p = '-';
			}

			append (p, static_cast<size_t>(end - p));
		}

		[[gnu::always_inline]]
		void ensure_valid_index (size_t access_index) const noexcept
		{
			if (access_index < idx && access_index < buffer.size ()) [[likely]] {
				return;
			}

			char *message = nullptr;
			int n = asprintf (&message, "Index %zu is out of range (0 - %zu)", access_index, idx);
			Helpers::abort_application (n == -1 ? "Index out of range" : message);
		}

		[[gnu::always_inline]]
		void ensure_have_extra (size_t length) noexcept
		{
			size_t needed_space = Helpers::add_with_overflow_check<size_t> (length, idx + 1);
			if (needed_space > buffer.size ()) {
				char *message = nullptr;
				int n = asprintf (
					&message,
					"Attempt to store too much data in a buffer (capacity: %zu; exceeded by: %zu)",
					buffer.size (),
					needed_space - buffer.size ()
				);
				Helpers::abort_application (n == -1 ? "Attempt to store too much data in a buffer" : message);
			}
		}

		[[gnu::always_inline]]
		void resize_for_extra (size_t needed_space) noexcept
		{
			if constexpr (TStorage::has_resize) {
				size_t required_space = Helpers::add_with_overflow_check<size_t> (needed_space, idx + 1uz);
				size_t current_size = buffer.size ();
				if (required_space > current_size) {
					size_t new_size = Helpers::add_with_overflow_check<size_t> (current_size, (current_size / 2uz));
					new_size = Helpers::add_with_overflow_check<size_t> (new_size, required_space);
					buffer.resize (new_size);
				}
			}
		}

	private:
		size_t   idx;
		TStorage buffer;
	};

	template<size_t MaxStackSize, typename TChar = char>
	class static_local_string : public string_base<MaxStackSize, static_local_storage<MaxStackSize, TChar>, TChar>
	{
		using base = string_base<MaxStackSize, static_local_storage<MaxStackSize, TChar>, TChar>;

	public:
		explicit static_local_string (size_t initial_size = 0uz) noexcept
			: base (initial_size)
		{}

		explicit static_local_string (const string_segment &token) noexcept
			: base (token)
		{}

		template<size_t N>
		explicit static_local_string (const char (&str)[N])
			: base (N)
		{
			append (str);
		}
	};

	template<size_t MaxStackSize, typename TChar = char>
	class dynamic_local_string : public string_base<MaxStackSize, dynamic_local_storage<MaxStackSize, TChar>, TChar>
	{
		using base = string_base<MaxStackSize, dynamic_local_storage<MaxStackSize, TChar>, TChar>;

	public:
		explicit dynamic_local_string (size_t initial_size = 0uz)
			: base (initial_size)
		{}

		explicit dynamic_local_string (const string_segment &token) noexcept
			: base (token)
		{}

		template<size_t N>
		explicit dynamic_local_string (const char (&str)[N])
			: base (N)
		{
			base::append (str);
		}

		explicit dynamic_local_string (std::string_view const& str)
			: base (str.length ())
		{
			base::append (str);
		}
	};
}

import subprocess
import sys
import time
import select
import os
import lldb

def clearjdb(debugger, command, result, internal_dict):
    run_jdb_quit()

# Function that can be run directly without LLDB
def run_jdb_quit():
    """
    Standalone function to run jdb and send quit command
    Usage: python -c "import lldb_commands; lldb_commands.run_jdb_quit()"
    """
    try:
        time.sleep(1)
        # Start jdb process with pipes for stdin/stdout
        process = subprocess.Popen(
            ['jdb', '-attach', 'localhost:8700'],
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            text=True,
            bufsize=1
        )
        
        # Wait briefly to let JDB establish connection and print initial messages
        # this is needed otherwise the debugger will not attach
        time.sleep(1)
        process.kill()
        print("JDB session cleared successfully")
    except subprocess.TimeoutExpired:
        print("JDB process timed out - killing process")
        process.kill()
    except Exception as e:
        print(f"Error running jdb: {e}")

def System_String_Summary(valobj, internal_dict):
    try:
        data_ptr = valobj.GetChildMemberWithName("_firstChar")
        length = valobj.GetChildMemberWithName("_stringLength").GetValueAsUnsigned()
        if data_ptr and length:
            process = valobj.GetProcess()
            error = lldb.SBError()
            address = data_ptr.GetLoadAddress()
            if address == lldb.LLDB_INVALID_ADDRESS:
                return "<invalid address>"
            string_data = process.ReadMemory(address, length * 2, error)  # UTF-16 encoding uses 2 bytes per character
            if error.Success():
                return string_data.decode("utf-16")
            else:
                return f"<error reading memory: {error}>"
    except Exception as e:
        return f"<error reading string: {e}>"
    return "<empty>"

def __lldb_init_module(debugger, internal_dict):
    debugger.HandleCommand('type summary add --python-function lldb_commands.System_String_Summary "String"')
    print('The "formatter" command has been installed!')

# Allow direct execution of this script
if __name__ == "__main__":
    run_jdb_quit()
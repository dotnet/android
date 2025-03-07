import subprocess
import sys
import time
import select
import os

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

# Allow direct execution of this script
if __name__ == "__main__":
    run_jdb_quit()
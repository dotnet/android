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
        time.sleep(0.5)
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
        # # Send the quit command to stdin
        # process.stdin.write('quit\n')
        # process.stdin.flush()
        
        # # Wait briefly for the quit command to be processed
        # time.sleep(0.5)
        
        # # Wait for process to complete (with timeout)
        # process.wait(timeout=5)
        
        print("JDB session cleared successfully")
    except subprocess.TimeoutExpired:
        print("JDB process timed out - killing process")
        process.kill()
    except Exception as e:
        print(f"Error running jdb: {e}")

def read_and_print_output(process):
    """
    Read and print available output from the process in a non-blocking way
    """
    # Check if we're on Windows
    if os.name == 'nt':
        # Windows doesn't support select on pipes, so we'll use a simpler approach
        import msvcrt
        from queue import Queue, Empty
        import threading
        
        output_queue = Queue()
        def reader_thread():
            for line in process.stdout:
                if line:
                    output_queue.put(line)
        
        t = threading.Thread(target=reader_thread)
        t.daemon = True
        t.start()
        
        # Read for up to 1 second
        end_time = time.time() + 1
        while time.time() < end_time:
            try:
                line = output_queue.get(block=False)
                print(f"JDB> {line}", end='')
            except Empty:
                time.sleep(0.1)
    else:
        # Unix-like systems can use select
        readable, _, _ = select.select([process.stdout], [], [], 1)
        if process.stdout in readable:
            while True:
                line = process.stdout.readline()
                if not line:
                    break
                print(f"JDB> {line}", end='')

# Allow direct execution of this script
if __name__ == "__main__":
    run_jdb_quit()
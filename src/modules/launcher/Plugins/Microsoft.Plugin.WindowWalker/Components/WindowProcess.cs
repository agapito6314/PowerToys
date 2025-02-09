// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Microsoft.Plugin.WindowWalker.Components
{
    /// <summary>
    /// Represents the process data of an open window. This class is used in the process cache and for the process object of the open window
    /// </summary>
    public class WindowProcess
    {
        /// <summary>
        /// Maximum size of a file name
        /// </summary>
        private const int MaximumFileNameLength = 1000;

        /// <summary>
        /// An indicator if the window belongs to an 'Universal Windows Platform (UWP)' process
        /// </summary>
        private readonly bool _isUwpApp;

        /// <summary>
        /// Gets the id of the process
        /// </summary>
        public uint ProcessID
        {
            get; private set;
        }

        /// <summary>
        /// Gets the id of the thread
        /// </summary>
        public uint ThreadID
        {
            get; private set;
        }

        /// <summary>
        /// Gets the name of the process
        /// </summary>
        public string Name
        {
            get; private set;
        }

        /// <summary>
        /// Gets a value indicating whether the window belongs to an 'Universal Windows Platform (UWP)' process
        /// </summary>
        public bool IsUwpApp
        {
            get { return _isUwpApp; }
        }

        /// <summary>
        /// Gets a value indicating whether this is the shell process or not
        /// The shell process (like explorer.exe) hosts parts of the user interface (like taskbar, start menu, ...)
        /// </summary>
        public bool IsShellProcess
        {
            get
            {
                IntPtr hShellWindow = NativeMethods.GetShellWindow();
                return GetProcessIDFromWindowHandle(hShellWindow) == ProcessID;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the process exists on the machine
        /// </summary>
        public bool DoesExist
        {
            get
            {
                try
                {
                    var p = Process.GetProcessById((int)ProcessID);
                    p.Dispose();
                    return true;
                }
                catch (InvalidOperationException)
                {
                    // Thrown when process not exist.
                    return false;
                }
                catch (ArgumentException)
                {
                    // Thrown when process not exist.
                    return false;
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether full access to the process is denied or not
        /// </summary>
        public bool IsFullAccessDenied
        {
            get; private set;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WindowProcess"/> class.
        /// </summary>
        /// <param name="pid">New process id.</param>
        /// <param name="tid">New thread id.</param>
        /// <param name="name">New process name.</param>
        public WindowProcess(uint pid, uint tid, string name)
        {
            UpdateProcessInfo(pid, tid, name);
            _isUwpApp = Name.ToUpperInvariant().Equals("APPLICATIONFRAMEHOST.EXE", StringComparison.Ordinal);
        }

        /// <summary>
        /// Updates the process information of the <see cref="WindowProcess"/> instance.
        /// </summary>
        /// <param name="pid">New process id.</param>
        /// <param name="tid">New thread id.</param>
        /// <param name="name">New process name.</param>
        public void UpdateProcessInfo(uint pid, uint tid, string name)
        {
            // TODO: Add verification as to wether the process id and thread id is valid
            ProcessID = pid;
            ThreadID = tid;
            Name = name;

            // Process can be elevated only if process id is not 0 (Dummy value on error)
            IsFullAccessDenied = (pid != 0) ? TestProcessAccessUsingAllAccessFlag(pid) : false;
        }

        /// <summary>
        /// Gets the process ID for the window handle
        /// </summary>
        /// <param name="hwnd">The handle to the window</param>
        /// <returns>The process ID</returns>
        public static uint GetProcessIDFromWindowHandle(IntPtr hwnd)
        {
            _ = NativeMethods.GetWindowThreadProcessId(hwnd, out uint processId);
            return processId;
        }

        /// <summary>
        /// Gets the thread ID for the window handle
        /// </summary>
        /// <param name="hwnd">The handle to the window</param>
        /// <returns>The thread ID</returns>
        public static uint GetThreadIDFromWindowHandle(IntPtr hwnd)
        {
            uint threadId = NativeMethods.GetWindowThreadProcessId(hwnd, out _);
            return threadId;
        }

        /// <summary>
        /// Gets the process name for the process ID
        /// </summary>
        /// <param name="pid">The id of the process/param>
        /// <returns>A string representing the process name or an empty string if the function fails</returns>
        public static string GetProcessNameFromProcessID(uint pid)
        {
            IntPtr processHandle = NativeMethods.OpenProcess(NativeMethods.ProcessAccessFlags.QueryLimitedInformation, true, (int)pid);
            StringBuilder processName = new StringBuilder(MaximumFileNameLength);

            if (NativeMethods.GetProcessImageFileName(processHandle, processName, MaximumFileNameLength) != 0)
            {
                _ = NativeMethods.CloseHandleIfNotNull(processHandle);
                return processName.ToString().Split('\\').Reverse().ToArray()[0];
            }
            else
            {
                _ = NativeMethods.CloseHandleIfNotNull(processHandle);
                return string.Empty;
            }
        }

        /// <summary>
        /// Gets a boolean value indicating whether the access to a process using the AllAccess flag is denied or not.
        /// </summary>
        /// <param name="pid">The process ID of the process</param>
        /// <returns>True if denied and false if not.</returns>
        private static bool TestProcessAccessUsingAllAccessFlag(uint pid)
        {
            IntPtr processHandle = NativeMethods.OpenProcess(NativeMethods.ProcessAccessFlags.AllAccess, true, (int)pid);

            if (NativeMethods.GetLastWin32Error() == 5)
            {
                // Error 5 = ERROR_ACCESS_DENIED
                _ = NativeMethods.CloseHandleIfNotNull(processHandle);
                return true;
            }
            else
            {
                _ = NativeMethods.CloseHandleIfNotNull(processHandle);
                return false;
            }
        }
    }
}

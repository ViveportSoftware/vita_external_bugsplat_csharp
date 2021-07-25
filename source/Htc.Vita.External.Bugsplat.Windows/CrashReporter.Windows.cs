using System;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Security;
using System.Windows.Threading;

namespace Htc.Vita.External.Bugsplat
{
    public partial class CrashReporter
    {
        public static int ExitCodeOnDispatcherError { get; set; } = 1;
        public static bool ExitOnDispatcherError { get; set; } = true;

        [SecurityCritical]
        [HandleProcessCorruptedStateExceptions]
        public static void DispatcherUnhandledExceptionHandler(
                object sender,
                DispatcherUnhandledExceptionEventArgs args)
        {
            lock (Lock)
            {
                if (_isApplicationExiting)
                {
                    return;
                }
                if (ExitOnDispatcherError)
                {
                    _isApplicationExiting = true;
                }
            }

            var exception = args.Exception;
            if (exception == null)
            {
                Trace.WriteLine($"[{nameof(CrashReporter)}][{nameof(DispatcherUnhandledExceptionHandler)}] Do not find valid exception. Skipped.");
                return;
            }

            try
            {
                OnExceptionHandled?.Invoke(exception);
                ReportException(exception);
            }
            catch (Exception)
            {
                Environment.Exit(ExitCodeOnDispatcherError);
            }

            if (!ExitOnDispatcherError)
            {
                args.Handled = true;
                return;
            }
            Environment.Exit(ExitCodeOnDispatcherError);
        }
    }
}

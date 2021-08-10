using System;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Runtime.ExceptionServices;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using BugSplatDotNetStandard;

namespace Htc.Vita.External.Bugsplat
{
    public partial class CrashReporter
    {
        public static event Action<Exception> OnExceptionHandled;

        public static int ExitCodeOnAppDomainError { get; set; } = 1;
        public static int ExitCodeOnTaskSchedulerError { get; set; } = 1;
        public static bool ExitOnAppDomainError { get; set; } = true;
        public static bool ExitOnTaskSchedulerError { get; set; } = true;

        private static readonly object Lock = new object();

        private static string _bugsplatAppName;
        private static string _bugsplatAppVersion;
        private static string _bugsplatDatabaseName;
        private static bool _isApplicationExiting;

        [SecurityCritical]
        [HandleProcessCorruptedStateExceptions]
        public static void AppDomainUnhandledExceptionHandler(
                object sender,
                UnhandledExceptionEventArgs args)
        {
            lock (Lock)
            {
                if (_isApplicationExiting)
                {
                    return;
                }
                if (ExitOnAppDomainError)
                {
                    _isApplicationExiting = true;
                }
            }

            var exception = args.ExceptionObject as Exception;
            if (exception == null)
            {
                Trace.WriteLine($"[{nameof(CrashReporter)}][{nameof(AppDomainUnhandledExceptionHandler)}] Do not find valid exception. Skipped.");
                return;
            }

            try
            {
                OnExceptionHandled?.Invoke(exception);
                ReportException(exception);
            }
            catch (Exception)
            {
                Environment.Exit(ExitCodeOnAppDomainError);
            }

            if (!ExitOnAppDomainError)
            {
                return;
            }
            Environment.Exit(ExitCodeOnAppDomainError);
        }

        public static void Init(
                string databaseName,
                string appName,
                string appVersion)
        {
            if (string.IsNullOrWhiteSpace(appName))
            {
                throw new ArgumentException("Arguments can not be empty.", nameof(appName));
            }
            _bugsplatAppName = appName;
            if (string.IsNullOrWhiteSpace(appVersion))
            {
                throw new ArgumentException("Arguments can not be empty.", nameof(appVersion));
            }
            _bugsplatAppVersion = appVersion;
            if (string.IsNullOrWhiteSpace(databaseName))
            {
                throw new ArgumentException("Arguments can not be empty.", nameof(databaseName));
            }
            _bugsplatDatabaseName = databaseName;
        }

        private static void ReportException(Exception exception)
        {
            if (exception == null)
            {
                return;
            }

            var bugSplat = new BugSplat(
                    _bugsplatDatabaseName,
                    _bugsplatAppName,
                    _bugsplatAppVersion
            );
            var oldCulture = Thread.CurrentThread.CurrentCulture;
            var oldUICulture = Thread.CurrentThread.CurrentUICulture;
            try
            {
                var exceptionCulture = CultureInfo.InvariantCulture;
                Thread.CurrentThread.CurrentCulture = exceptionCulture;
                Thread.CurrentThread.CurrentUICulture = exceptionCulture;
                var response = bugSplat.Post(exception).Result;
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    Trace.WriteLine($"[{nameof(CrashReporter)}][{nameof(ReportException)}] Exception is reported successfully.");
                }
            }
            catch (Exception e)
            {
                Trace.WriteLine(e);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = oldCulture;
                Thread.CurrentThread.CurrentUICulture = oldUICulture;
            }
        }

        [SecurityCritical]
        [HandleProcessCorruptedStateExceptions]
        public static void TaskSchedulerUnobservedTaskExceptionHandler(
                object sender,
                UnobservedTaskExceptionEventArgs args)
        {
            lock (Lock)
            {
                if (_isApplicationExiting)
                {
                    return;
                }
                if (ExitOnTaskSchedulerError)
                {
                    _isApplicationExiting = true;
                }
            }

            var exception = args.Exception;
            if (exception == null)
            {
                Trace.WriteLine($"[{nameof(CrashReporter)}][{nameof(TaskSchedulerUnobservedTaskExceptionHandler)}] Do not find valid exception. Skipped.");
                return;
            }

            try
            {
                OnExceptionHandled?.Invoke(exception);
                ReportException(exception);
            }
            catch (Exception)
            {
                Environment.Exit(ExitCodeOnTaskSchedulerError);
            }

            if (!ExitOnTaskSchedulerError)
            {
                return;
            }
            Environment.Exit(ExitCodeOnTaskSchedulerError);
        }
    }
}

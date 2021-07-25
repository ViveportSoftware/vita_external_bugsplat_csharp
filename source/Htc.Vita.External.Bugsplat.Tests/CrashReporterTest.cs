using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Htc.Vita.External.Bugsplat.Tests
{
    public class CrashReporterTest
    {
        [Fact]
        public void CrashReporterTest_0_Init()
        {
            CrashReporter.Init(
                    "vita_2_htc_gmail_com",
                    "MyDotNetCrasherTest",
                    Assembly.GetExecutingAssembly().GetName().Version.ToString()
            );
            CrashReporter.ExitOnTaskSchedulerError = false;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

            Task.Run(() =>
            {
                throw new InvalidOperationException();
            });

            SpinWait.SpinUntil(() => false, TimeSpan.FromSeconds(5));
        }

        private static void TaskScheduler_UnobservedTaskException(
                object sender,
                UnobservedTaskExceptionEventArgs args)
        {
            args.SetObserved();

            CrashReporter.TaskSchedulerUnobservedTaskExceptionHandler(
                    sender,
                    args
            );
        }
    }
}

using System;
using System.Net;
using System.Reflection;
using BugSplatDotNetStandard;
using Xunit;

namespace Htc.Vita.External.Bugsplat.Tests
{
    public class BugSplatTest
    {
        [Fact]
        public void BugSplat_Post_ShouldPostExceptionToBugSplat()
        {
            try
            {
                throw new Exception("BugSplat!");
            }
            catch (Exception ex)
            {
                var sut = new BugSplat(
                        "vita_2_htc_gmail_com",
                        "MyDotNetCrasherTest",
                        Assembly.GetExecutingAssembly().GetName().Version.ToString()
                );

                var response = sut.Post(ex).Result;
                var body = response.Content.ReadAsStringAsync().Result;

                Assert.True(response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.BadRequest);
            }
        }
    }
}

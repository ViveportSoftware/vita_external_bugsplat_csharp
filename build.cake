#addin "nuget:?package=Cake.Git&version=0.22.0"

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var configuration = Argument("configuration", "Debug");
var revision = EnvironmentVariable("BUILD_NUMBER") ?? Argument("revision", "9999");
var target = Argument("target", "Default");


//////////////////////////////////////////////////////////////////////
// PREPARATION
//////////////////////////////////////////////////////////////////////

// Define git commit id
var commitId = "SNAPSHOT";

// Define product name and version
var product = "Htc.Vita.External.Bugsplat";
var companyName = "HTC";
var version = "0.9.2";
var semanticVersion = $"{version}.{revision}";
var ciVersion = $"{version}.0";
var buildVersion = "Release".Equals(configuration) ? semanticVersion : $"{ciVersion}-CI{revision}";
var targetPlatformsMap = new Dictionary<string, List<string>>
{
        ["Portable"] = new List<string>
        {
                "net45",
                "netstandard2.0"
        },
        ["Windows"] = new List<string>
        {
                "net45",
                "netcoreapp3.1"
        }
};

// Define copyright
var copyright = $"Copyright © 2021 - {DateTime.Now.Year}";

// Define timestamp for signing
var lastSignTimestamp = DateTime.Now;
var signIntervalInMilli = 1000 * 5;

// Define path
var solutionFile = File($"./source/{product}.sln");

// Define directories.
var distDir = Directory("./dist");
var tempDir = Directory("./temp");
var generatedDir = Directory("./source/generated");
var packagesDir = Directory("./source/packages");
var nugetDir = distDir + Directory(configuration) + Directory("nuget");
var homeDir = Directory(EnvironmentVariable("USERPROFILE") ?? EnvironmentVariable("HOME"));

// Define signing key, password and timestamp server
var signKeyEnc00 = EnvironmentVariable("SIGNKEYENC00");
var signKeyEnc01 = EnvironmentVariable("SIGNKEYENC01");
var signKeyEnc02 = EnvironmentVariable("SIGNKEYENC02");
var signKeyEnc03 = EnvironmentVariable("SIGNKEYENC03");
var signKeyEnc04 = EnvironmentVariable("SIGNKEYENC04");
var signKeyEnc05 = EnvironmentVariable("SIGNKEYENC05");
var signKeyEnc06 = EnvironmentVariable("SIGNKEYENC06");
var signKeyEnc07 = EnvironmentVariable("SIGNKEYENC07");
var signKeyEnc = EnvironmentVariable("SIGNKEYENC") ?? signKeyEnc00 + signKeyEnc01 + signKeyEnc02 + signKeyEnc03 + signKeyEnc04 + signKeyEnc05 + signKeyEnc06 + signKeyEnc07 ?? "NOTSET";
var signPass = EnvironmentVariable("SIGNPASS") ?? "NOTSET";
var signSha1Uri = new Uri("http://timestamp.digicert.com");
var signSha256Uri = new Uri("http://timestamp.digicert.com");

// Define nuget push source and key
var nugetApiKey = EnvironmentVariable("NUGET_PUSH_TOKEN") ?? EnvironmentVariable("NUGET_APIKEY") ?? "NOTSET";
var nugetSource = EnvironmentVariable("NUGET_PUSH_PATH") ?? EnvironmentVariable("NUGET_SOURCE") ?? "NOTSET";


//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Task("Fetch-Git-Commit-ID")
    .ContinueOnError()
    .Does(() =>
{
    var lastCommit = GitLogTip(MakeAbsolute(Directory(".")));
    commitId = lastCommit.Sha;
});

Task("Display-Config")
    .IsDependentOn("Fetch-Git-Commit-ID")
    .Does(() =>
{
    Information($"Build target:        {target}");
    Information($"Build configuration: {configuration}");
    Information($"Build commitId:      {commitId}");
    Information($"Build version:       {buildVersion}");
});

Task("Clean-Workspace")
    .IsDependentOn("Display-Config")
    .Does(() =>
{
    CleanDirectory(distDir);
    CleanDirectory(tempDir);
    CleanDirectory(generatedDir);
    CleanDirectory(packagesDir);
});

Task("Restore-NuGet-Packages")
    .IsDependentOn("Clean-Workspace")
    .Does(() =>
{
    NuGetRestore(new FilePath($"./source/{product}.sln"));
});

Task("Generate-AssemblyInfo")
    .IsDependentOn("Restore-NuGet-Packages")
    .Does(() =>
{
    CreateDirectory(generatedDir);
    var assemblyVersion = "Release".Equals(configuration) ? semanticVersion : ciVersion;
    CreateAssemblyInfo(
            new FilePath("./source/generated/SharedAssemblyInfo.cs"),
            new AssemblyInfoSettings
            {
                    Company = companyName,
                    Copyright = copyright,
                    FileVersion = assemblyVersion,
                    InformationalVersion = assemblyVersion,
                    Product = $"{product} : {commitId}",
                    Version = version
            }
    );
});

Task("Build-Assemblies")
    .IsDependentOn("Generate-AssemblyInfo")
    .Does(() =>
{
    DotNetCoreBuild(
            "./source/",
            new DotNetCoreBuildSettings
            {
                    Configuration = configuration
            }
    );
});


Task("Sign-Assemblies")
    .WithCriteria(() => "Release".Equals(configuration) && !"NOTSET".Equals(signPass) && !"NOTSET".Equals(signKeyEnc))
    .IsDependentOn("Build-Assemblies")
    .Does(() =>
{
    var currentSignTimestamp = DateTime.Now;
    Information($"Last timestamp:    {lastSignTimestamp}");
    Information($"Current timestamp: {currentSignTimestamp}");
    var signKey = "./temp/key.pfx";
    System.IO.File.WriteAllBytes(
            signKey,
            Convert.FromBase64String(signKeyEnc)
    );

    foreach (var targetPlatformsKey in targetPlatformsMap.Keys)
    {
        var targetPlatforms = targetPlatformsMap[targetPlatformsKey];

        foreach (var targetPlatform in targetPlatforms)
        {
            var file = $"./temp/{configuration}/{product}.{targetPlatformsKey}/bin/{targetPlatform}/{product}.{targetPlatformsKey}.dll";

            var totalTimeInMilli = (DateTime.Now - lastSignTimestamp).TotalMilliseconds;
            if (totalTimeInMilli < signIntervalInMilli)
            {
                System.Threading.Thread.Sleep(signIntervalInMilli - (int)totalTimeInMilli);
            }
            Sign(
                    file,
                    new SignToolSignSettings
                    {
                            CertPath = signKey,
                            Password = signPass,
                            TimeStampUri = signSha1Uri
                    }
            );
            lastSignTimestamp = DateTime.Now;

            System.Threading.Thread.Sleep(signIntervalInMilli);
            Sign(
                    file,
                    new SignToolSignSettings
                    {
                            AppendSignature = true,
                            CertPath = signKey,
                            DigestAlgorithm = SignToolDigestAlgorithm.Sha256,
                            Password = signPass,
                            TimeStampDigestAlgorithm = SignToolDigestAlgorithm.Sha256,
                            TimeStampUri = signSha256Uri
                    }
            );
            lastSignTimestamp = DateTime.Now;
        }
    }
});

Task("Build-NuGet-Package")
    .IsDependentOn("Sign-Assemblies")
    .Does(() =>
{
    CreateDirectory(nugetDir);
    foreach (var targetPlatformsKey in targetPlatformsMap.Keys)
    {
        DotNetCorePack(
                $"./source/{product}.{targetPlatformsKey}/",
                new DotNetCorePackSettings
                {
                        ArgumentCustomization = (args) =>
                        {
                                return args.Append($"/p:Version={buildVersion}");
                        },
                        Configuration = configuration,
                        NoBuild = true,
                        OutputDirectory = nugetDir
                }
        );
    }
});

Task("Publish-NuGet-Package")
    .WithCriteria(() => "Release".Equals(configuration) && !"NOTSET".Equals(nugetApiKey) && !"NOTSET".Equals(nugetSource))
    .IsDependentOn("Build-NuGet-Package")
    .Does(() =>
{
    foreach (var targetPlatformsKey in targetPlatformsMap.Keys)
    {
        NuGetPush(
                new FilePath($"./dist/{configuration}/nuget/{product}.{targetPlatformsKey}.{buildVersion}.nupkg"),
                new NuGetPushSettings
                {
                        ApiKey = nugetApiKey,
                        Source = nugetSource
                }
        );
    }
});


//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////

Task("Default")
    .IsDependentOn("Build-NuGet-Package");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);

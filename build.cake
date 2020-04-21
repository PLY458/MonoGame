#tool nuget:?package=vswhere&version=2.6.7
#tool nuget:?package=NUnit.ConsoleRunner&version=3.4.0

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var target = Argument("build-target", "Default");
var version = Argument("build-version", EnvironmentVariable("BUILD_NUMBER") ?? "3.8.0.1");
var configuration = Argument("build-configuration", "Release");

//////////////////////////////////////////////////////////////////////
// PREPARATION
//////////////////////////////////////////////////////////////////////

MSBuildSettings msPackSettings, mdPackSettings;
DotNetCoreMSBuildSettings dnBuildSettings;
DotNetCorePackSettings dnPackSettings;

private void PackMSBuild(string filePath)
{
    MSBuild(filePath, msPackSettings);
}

private void PackDotnet(string filePath)
{
    DotNetCorePack(filePath, dnPackSettings);
}

private bool GetMSBuildWith(string requires)
{
    if (IsRunningOnWindows())
    {
        DirectoryPath vsLatest = VSWhereLatest(new VSWhereLatestSettings { Requires = requires });

        if (vsLatest != null)
        {
            var files = GetFiles(vsLatest.FullPath + "/**/MSBuild.exe");
            if (files.Any())
            {
                msPackSettings.ToolPath = files.First();
                return true;
            }
        }
    }

    return false;
}

private bool GetVsixPublisher(out string path)
{
    if (IsRunningOnWindows())
    {
        DirectoryPath vsLatest = VSWhereLatest();

        if (vsLatest != null)
        {
            path = vsLatest.FullPath + "/VSSDK/VisualStudioIntegration/Tools/Bin/VsixPublisher.exe";
            return FileExists(path);
        }
    }

    path = null;
    return false;
}

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Task("Prep")
    .Does(() =>
{
    // We tag the version with the build branch to make it
    // easier to spot special builds in NuGet feeds.
    var branch = EnvironmentVariable("GIT_BRANCH") ?? string.Empty;
    if (branch == "develop")
	version += "-develop";

    Console.WriteLine("Build Version: {0}", version);

    msPackSettings = new MSBuildSettings();
    msPackSettings.Verbosity = Verbosity.Minimal;
    msPackSettings.Configuration = configuration;
    msPackSettings.Restore = true;
    msPackSettings.WithProperty("Version", version);
    msPackSettings.WithTarget("Pack");

    mdPackSettings = new MSBuildSettings();
    mdPackSettings.Verbosity = Verbosity.Minimal;
    mdPackSettings.Configuration = configuration;
    mdPackSettings.WithProperty("Version", version);
    mdPackSettings.WithTarget("PackageAddin");

    dnBuildSettings = new DotNetCoreMSBuildSettings();
    dnBuildSettings.WithProperty("Version", version);

    dnPackSettings = new DotNetCorePackSettings();
    dnPackSettings.MSBuildSettings = dnBuildSettings;
    dnPackSettings.Verbosity = DotNetCoreVerbosity.Minimal;
    dnPackSettings.Configuration = configuration;
});

Task("BuildDesktopGL")
    .IsDependentOn("Prep")
    .Does(() =>
{
    DotNetCoreRestore("MonoGame.Framework/MonoGame.Framework.DesktopGL.csproj");
    PackDotnet("MonoGame.Framework/MonoGame.Framework.DesktopGL.csproj");
});

Task("TestDesktopGL")
    .IsDependentOn("BuildDesktopGL")
    .Does(() =>
{
    CreateDirectory("Artifacts/Tests/DesktopGL/Debug");
    DotNetCoreRun("../../../../Tests/MonoGame.Tests.DesktopGL.csproj", "", new DotNetCoreRunSettings
    {
        WorkingDirectory = "Artifacts/Tests/DesktopGL/Debug",
	    ArgumentCustomization = args => args.Append("--teamcity")
    });
});

Task("BuildWindowsDX")
    .IsDependentOn("Prep")
    .WithCriteria(() => IsRunningOnWindows())
    .Does(() =>
{
    DotNetCoreRestore("MonoGame.Framework/MonoGame.Framework.WindowsDX.csproj");
    PackDotnet("MonoGame.Framework/MonoGame.Framework.WindowsDX.csproj");
});

Task("TestWindowsDX")
    .IsDependentOn("BuildWindowsDX")
    .WithCriteria(() => IsRunningOnWindows())
    .Does(() =>
{
    CreateDirectory("Artifacts/Tests/WindowsDX/Debug");
    DotNetCoreRun("../../../../Tests/MonoGame.Tests.WindowsDX.csproj", "", new DotNetCoreRunSettings
    {
        WorkingDirectory = "Artifacts/Tests/WindowsDX/Debug",
	    ArgumentCustomization = args => args.Append("--teamcity")
    });
});

Task("BuildAndroid")
    .IsDependentOn("Prep")
    .WithCriteria(() =>
{
    if (IsRunningOnWindows())
        return GetMSBuildWith("Component.Xamarin");

    return DirectoryExists("/Library/Frameworks/Xamarin.Android.framework");
}).Does(() =>
{
    PackMSBuild("MonoGame.Framework/MonoGame.Framework.Android.csproj");
});

Task("BuildiOS")
    .IsDependentOn("Prep")
    .WithCriteria(() =>
{
    return DirectoryExists("/Library/Frameworks/Xamarin.iOS.framework");
}).Does(() =>
{
    PackMSBuild("MonoGame.Framework/MonoGame.Framework.iOS.csproj");
});

Task("BuildUWP")
    .IsDependentOn("Prep")
    .WithCriteria(() => GetMSBuildWith("Microsoft.VisualStudio.Component.Windows10SDK.17763"))
    .Does(() =>
{
    PackMSBuild("MonoGame.Framework/MonoGame.Framework.WindowsUniversal.csproj");
});

Task("BuildContentPipeline")
    .IsDependentOn("Prep")
    .Does(() =>
{
    PackDotnet("MonoGame.Framework.Content.Pipeline/MonoGame.Framework.Content.Pipeline.csproj");
});

Task("BuildTools")
    .IsDependentOn("Prep")
    .Does(() =>
{
    PackDotnet("Tools/MonoGame.Content.Builder/MonoGame.Content.Builder.csproj");
    
    PackDotnet("Tools/MonoGame.Effect.Compiler/MonoGame.Effect.Compiler.csproj");
    
    PackDotnet("Tools/MonoGame.Content.Builder.Editor/MonoGame.Content.Builder.Editor.csproj");

    PackDotnet("Tools/MonoGame.Content.Builder.Task/MonoGame.Content.Builder.Task.csproj");

    PackDotnet("Tools/MonoGame.Packaging.Flatpak/MonoGame.Packaging.Flatpak.csproj");
});

Task("TestTools")
    .IsDependentOn("BuildTools")
    .Does(() =>
{
    CreateDirectory("Artifacts/Tests/Tools/Debug");
    DotNetCoreRun("../../../../Tools/MonoGame.Tools.Tests/MonoGame.Tools.Tests.csproj", "", new DotNetCoreRunSettings
    {
        WorkingDirectory = "Artifacts/Tests/Tools/Debug",
	    ArgumentCustomization = args => args.Append("--teamcity")
    });
});

Task("PackDotNetTemplates")
    .IsDependentOn("Prep")
    .Does(() =>
{
    PackDotnet("Templates/MonoGame.Templates.CSharp/MonoGame.Templates.CSharp.csproj");
});

Task("PackVSTemplates")
    .IsDependentOn("PackDotNetTemplates")
    .WithCriteria(() => IsRunningOnWindows())
    .Does(() =>
{
    var dotnet = Context.Tools.Resolve("dotnet.exe");
    if (StartProcess(dotnet, "tool restore") != 0)
        throw new Exception("dotnet tool restore failed.");

    var result = StartProcess(
        dotnet,
        "vstemplate " +
       $"-s Artifacts/MonoGame.Templates.CSharp/Release/MonoGame.Templates.CSharp.{version}.nupkg " +
       $"--vsix Artifacts/MonoGame.Templates.CSharp/MonoGame.Templates.CSharp.{version}.vsix " +
        "@Templates/VisualStudio/settings.rsp");

    if (result != 0)
        throw new Exception("dotnet-vstemplate failed to create VSIX.");
});
Task("PublishVSTemplates")
    .IsDependentOn("PackVSTemplates")
    .WithCriteria(() => IsRunningOnWindows())
    .Does(() =>
{
    if (!GetVsixPublisher(out var vsixPublisher))
        throw new Exception("Did not find VsixPublisher.exe.");

    var pat = EnvironmentVariable("AZURE_DEVOPS_MARKETPLACE_PAT");

    if (string.IsNullOrEmpty(pat))
        throw new Exception("Azure DevOps Personal Access Token for VS Marketplace not set (AZURE_DEVOPS_MARKETPLACE_PAT).");

    var result = StartProcess(vsixPublisher,
        "publish " +
       $"-payload Artifacts/MonoGame.Template.CSharp/MonoGame.Templates.CSharp.{version}.vsix " +
        "-publishManifest Templates/VisualStudio/csharp-manifest.json " +
       $"-personalAccessToken {pat}");

    if (result != 0)
        throw new Exception("VsixPublisher.exe publish failed.");
});

Task("PackVSMacTemplates")
    .IsDependentOn("PackDotNetTemplates")
    .WithCriteria(() => IsRunningOnUnix() && DirectoryExists("/Applications") && DirectoryExists("/Library"))
    .Does(() =>
{
    DotNetCoreRestore("Templates/VisualStudioForMac/MonoGame.IDE.VisualStudioForMac.csproj");
    MSBuild("Templates/VisualStudioForMac/MonoGame.IDE.VisualStudioForMac.csproj", mdPackSettings);
});

//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////

Task("BuildAll")
    .IsDependentOn("BuildDesktopGL")
    .IsDependentOn("BuildWindowsDX")
    .IsDependentOn("BuildAndroid")
    .IsDependentOn("BuildiOS")
    .IsDependentOn("BuildUWP")
    .IsDependentOn("BuildContentPipeline")
    .IsDependentOn("BuildTools");

Task("Pack")
    .IsDependentOn("BuildAll")
    .IsDependentOn("PackDotNetTemplates")
    .IsDependentOn("PackVSMacTemplates");

Task("Test")
    .IsDependentOn("TestWindowsDX")
    .IsDependentOn("TestDesktopGL")
    .IsDependentOn("TestTools");

Task("PublishStable")
    .IsDependentOn("PublishVSTemplates");

Task("Default")
    .IsDependentOn("Pack");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);

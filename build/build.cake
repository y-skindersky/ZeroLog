#l "scripts/utilities.cake"
#tool nuget:?package=NUnit.ConsoleRunner&version=3.7.0

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
var paths = new {
    solution = MakeAbsolute(File("./../src/ZeroLog.sln")).FullPath,
    project  = MakeAbsolute(File("./../src/ZeroLog/ZeroLog.csproj")).FullPath,
    version = MakeAbsolute(File("./../version.yml")).FullPath,
    assemblyInfo = MakeAbsolute(File("./../src/SharedVersionInfo.cs")).FullPath,
    output = new {
        build = MakeAbsolute(Directory("./../output/build")).FullPath,
        nuget = MakeAbsolute(Directory("./../output/nuget")).FullPath,
    },
    nuspec = MakeAbsolute(File("./ZeroLog.nuspec")).FullPath,
    testProject = MakeAbsolute(File("./../src/ZeroLog.Tests/ZeroLog.Tests.csproj")).FullPath,
};

ReadContext(paths.version);

//////////////////////////////////////////////////////////////////////
// HELPERS
//////////////////////////////////////////////////////////////////////

private void Build(string configuration, string outputDirectoryPath)
{
    MSBuild(paths.solution, settings => settings.SetConfiguration(configuration)
                                                .SetPlatformTarget(PlatformTarget.MSIL)
                                                .WithProperty("OutDir", outputDirectoryPath + "/" + configuration));
}

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Task("UpdateBuildVersionNumber").Does(() => UpdateAppVeyorBuildVersionNumber());
Task("Clean").Does(() =>
{
    CleanDirectory(paths.output.build);
    CleanDirectory(paths.output.nuget);
});
Task("Restore-NuGet-Packages").Does(() => DotNetCoreRestore(paths.solution));
Task("Create-AssemblyInfo").Does(()=>{
    Information("Assembly Version: {0}", VersionContext.AssemblyVersion);
    Information("   NuGet Version: {0}", VersionContext.NugetVersion);
    CreateAssemblyInfo(paths.assemblyInfo, new AssemblyInfoSettings {
        Version = VersionContext.AssemblyVersion,
        FileVersion = VersionContext.AssemblyVersion,
        InformationalVersion = VersionContext.NugetVersion + " Commit: " + VersionContext.Git.Sha
    });
});
Task("Build-Net45").Does(() => DotNetCoreBuild(paths.project, new DotNetCoreBuildSettings { Framework = "net45", Configuration = "Release", OutputDirectory = paths.output.build + "/net45"} ));
Task("Build-NetCore").Does(() => DotNetCoreBuild(paths.project, new DotNetCoreBuildSettings { Framework = "netstandard2.0", Configuration = "Release", OutputDirectory = paths.output.build + "/netstandard2.0"} ));
Task("Clean-AssemblyInfo").Does(() => System.IO.File.WriteAllText(paths.assemblyInfo, string.Empty));
Task("Run-Unit-Tests-Net45").Does(() => DotNetCoreTest(paths.testProject, new DotNetCoreTestSettings { Configuration = "Release", Framework = "net45" }));
Task("Run-Unit-Tests-NetCore").Does(() => DotNetCoreTest(paths.testProject, new DotNetCoreTestSettings { Configuration = "Release", Framework = "netcoreapp2.0" }));
Task("Nuget-Pack").Does(() => 
{
    NuGetPack(paths.nuspec, new NuGetPackSettings {
        Version = VersionContext.NugetVersion,
        BasePath = paths.output.build,
        OutputDirectory = paths.output.nuget
    });
});

//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////

Task("Build")
    .IsDependentOn("UpdateBuildVersionNumber")
    .IsDependentOn("Clean")
    .IsDependentOn("Restore-NuGet-Packages")
    .IsDependentOn("Create-AssemblyInfo")
    .IsDependentOn("Build-Net45")
    .IsDependentOn("Build-NetCore")
    .IsDependentOn("Clean-AssemblyInfo");

Task("Test")
    .IsDependentOn("Build")
    .IsDependentOn("Run-Unit-Tests-Net45")
    .IsDependentOn("Run-Unit-Tests-NetCore");

Task("Nuget")
    .IsDependentOn("Test")
    .IsDependentOn("Nuget-Pack")
    .Does(() => {
        Information("   Nuget package is now ready at location: {0}.", paths.output.nuget);
        Warning("   Please remember to create and push a tag based on the currently built version.");
        Information("   You can do so by copying/pasting the following commands:");
        Information("       git tag v{0}", VersionContext.NugetVersion);
        Information("       git push origin --tags");
    });

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);

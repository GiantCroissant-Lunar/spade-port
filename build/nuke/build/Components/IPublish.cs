using System;
using System.IO;
using System.Linq;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tools.DotNet;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

interface IPublish : ICompile, IBuildConfig, IClean
{
    AbsolutePath ArtifactsRoot => ArtifactsDirectory;

    string ArtifactsVersion => this is Build b ? b.GitVersionNuGet : "0.0.0-local";

    AbsolutePath PackagesDirectory => ArtifactsRoot / ArtifactsVersion / "nuget";

    Target Pack => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            var projects = Config.PublishProjectPaths.Any()
                ? Config.PublishProjectPaths
                    .Select(p => (AbsolutePath)(RootDirectory / p))
                    .ToList()
                : new[]
                {
                    RootDirectory / "dotnet" / "src" / "Spade" / "Spade.csproj",
                    RootDirectory / "dotnet" / "src" / "Spade.Advanced" / "Spade.Advanced.csproj",
                }.ToList();

            PackagesDirectory.CreateOrCleanDirectory();

            foreach (var project in projects)
            {
                DotNetPack(s => s
                    .SetProject(project)
                    .SetConfiguration(Configuration)
                    .SetOutputDirectory(PackagesDirectory)
                    .SetVersion(ArtifactsVersion)
                    .EnableNoBuild()
                    .EnableNoRestore());
            }

            if (Config.SyncLocalNugetFeed && !string.IsNullOrWhiteSpace(Config.LocalNugetFeedRoot))
            {
                var feedRoot = RootDirectory / Config.LocalNugetFeedRoot;
                var flatDir = feedRoot / Config.LocalNugetFeedFlatSubdir;
                var hierarchicalDir = feedRoot / Config.LocalNugetFeedHierarchicalSubdir;

                flatDir.CreateDirectory();
                hierarchicalDir.CreateDirectory();

                foreach (var package in PackagesDirectory.GlobFiles("*.nupkg"))
                {
                    var packagePath = (string)package;
                    var fileName = Path.GetFileName(packagePath);
                    var parts = fileName.Split('.');
                    if (parts.Length < 3)
                        continue;

                    var packageId = string.Join(".", parts.Take(parts.Length - 2));
                    var idDir = hierarchicalDir / packageId;
                    idDir.CreateDirectory();
                    var flatDest = Path.Combine((string)flatDir, fileName);
                    Directory.CreateDirectory(Path.GetDirectoryName(flatDest)!);
                    File.Copy(packagePath, flatDest, overwrite: true);

                    var idDirPath = (string)idDir;
                    var hierDest = Path.Combine(idDirPath, fileName);
                    Directory.CreateDirectory(Path.GetDirectoryName(hierDest)!);
                    File.Copy(packagePath, hierDest, overwrite: true);
                }
            }
        });
}

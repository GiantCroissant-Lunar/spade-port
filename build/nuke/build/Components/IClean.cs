using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Utilities.Collections;

interface IClean : INukeBuild
{
    AbsolutePath SourceDirectory => RootDirectory / "dotnet";
    AbsolutePath ArtifactsDirectory => RootDirectory / "build" / "_artifacts";

    Target Clean => _ => _
        .Before<IRestore>()
        .Executes(() =>
        {
            SourceDirectory.GlobDirectories("**/bin", "**/obj")
                .ForEach(d => d.DeleteDirectory());

            ArtifactsDirectory.CreateOrCleanDirectory();
        });
}

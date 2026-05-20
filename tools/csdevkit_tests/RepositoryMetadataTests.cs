using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace Mu3Library.CsDevKit.Tests;

public sealed class RepositoryMetadataTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    [Fact]
    public void BuiltInWorkspacePointsToBuiltInSolution()
    {
        WorkspaceFile workspace = LoadWorkspace("UnityProject_BuiltIn/Mu3Library_ForUnity.code-workspace");

        Assert.Equal("UnityProject_BuiltIn/UnityProject_BuiltIn.slnx", workspace.Settings.DotnetDefaultSolution);
    }

    [Fact]
    public void UrpWorkspacePointsToUrpSolution()
    {
        WorkspaceFile workspace = LoadWorkspace("UnityProject_URP/Mu3Library_ForUnity.code-workspace");

        Assert.Equal("UnityProject_URP/UnityProject_URP.slnx", workspace.Settings.DotnetDefaultSolution);
    }

    [Fact]
    public void UrpPackageDependsOnCurrentBaseVersion()
    {
        PackageFile basePackage = LoadPackage("Mu3Library_Base/package.json");
        PackageFile urpPackage = LoadPackage("Mu3Library_URP/package.json");

        Assert.True(
            urpPackage.Dependencies.TryGetValue("com.github.doqltl179.mu3library.base", out string? dependencyVersion),
            "URP package should declare a dependency on the Base package."
        );
        Assert.Equal(basePackage.Version, dependencyVersion);
    }

    [Fact]
    public void BuiltInRemainsTheDefaultContext()
    {
        PackageFile basePackage = LoadPackage("Mu3Library_Base/package.json");
        PackageFile urpPackage = LoadPackage("Mu3Library_URP/package.json");

        Assert.Equal("com.github.doqltl179.mu3library.base", basePackage.Name);
        Assert.Equal("com.github.doqltl179.mu3library.urp", urpPackage.Name);
    }

    private static WorkspaceFile LoadWorkspace(string relativePath)
    {
        return JsonSerializer.Deserialize<WorkspaceFile>(ReadRepoText(relativePath), JsonOptions)
            ?? throw new InvalidOperationException($"Failed to parse workspace file: {relativePath}");
    }

    private static PackageFile LoadPackage(string relativePath)
    {
        return JsonSerializer.Deserialize<PackageFile>(ReadRepoText(relativePath), JsonOptions)
            ?? throw new InvalidOperationException($"Failed to parse package file: {relativePath}");
    }

    private static string ReadRepoText(string relativePath)
    {
        string filePath = Path.Combine(RepositoryRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));
        return File.ReadAllText(filePath);
    }

    private static string RepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);

        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "AGENTS.md"))
                && Directory.Exists(Path.Combine(current.FullName, ".github")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Failed to locate the repository root.");
    }

    private sealed class WorkspaceFile
    {
        public WorkspaceSettings Settings { get; init; } = new();
    }

    private sealed class WorkspaceSettings
    {
        [JsonPropertyName("dotnet.defaultSolution")]
        public string DotnetDefaultSolution { get; init; } = string.Empty;
    }

    private sealed class PackageFile
    {
        public string Name { get; init; } = string.Empty;

        public string Version { get; init; } = string.Empty;

        public Dictionary<string, string> Dependencies { get; init; } = new(StringComparer.Ordinal);
    }
}
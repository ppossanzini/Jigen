namespace Jigen.Index.Sparse.Tests;

internal sealed class TestStorageDirectory : IDisposable
{
    public string DirectoryPath { get; }

    public TestStorageDirectory()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !IsRepositoryRoot(root))
            root = root.Parent;
        if (root is null)
            throw new InvalidOperationException("Cannot locate the repository for test storage.");

        DirectoryPath = Path.Combine(root.FullName, ".temp", $"sparse-tests-{Guid.CreateVersion7():N}");
        Directory.CreateDirectory(DirectoryPath);
    }

    public void Dispose() => Directory.Delete(DirectoryPath, recursive: true);

    // The solution file marks the root; both formats are accepted so the check
    // survives a conversion from .sln to .slnx.
    private static bool IsRepositoryRoot(DirectoryInfo directory) =>
        File.Exists(Path.Combine(directory.FullName, "Jigen.slnx")) ||
        File.Exists(Path.Combine(directory.FullName, "Jigen.sln"));
}

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ApkAssistant.Tests;

public class DiffManagerTests
{
    private sealed class TempDir : IDisposable
    {
        public string Path { get; }
        public TempDir()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "ApkDiffTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }
        public string Write(string relativeName, string content)
        {
            var full = System.IO.Path.Combine(Path, relativeName);
            var dir = System.IO.Path.GetDirectoryName(full)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(full, content);
            return full;
        }
        public void Dispose() { try { Directory.Delete(Path, true); } catch { } }
    }

    [Fact]
    public async Task CompareDirectoriesAsync_DetectsAddedRemovedModifiedAndUnchanged()
    {
        using var left = new TempDir();
        using var right = new TempDir();

        left.Write("same.txt", "identical content");
        right.Write("same.txt", "identical content");

        left.Write("mod.txt", "a\nb\nc\n");
        right.Write("mod.txt", "a\nX\nc\nd\n");

        left.Write("removed.txt", "only in left");
        right.Write("added.txt", "only in right");

        var dm = new DiffManager();
        var entries = await dm.CompareDirectoriesAsync(left.Path, right.Path, CancellationToken.None);

        Assert.DoesNotContain(entries, e => e.RelativePath == "same.txt");
        Assert.Contains(entries, e => e.RelativePath == "mod.txt" && e.Status == DiffStatus.Modified);
        Assert.Contains(entries, e => e.RelativePath == "removed.txt" && e.Status == DiffStatus.Removed);
        Assert.Contains(entries, e => e.RelativePath == "added.txt" && e.Status == DiffStatus.Added);
        Assert.Equal(3, entries.Count);
    }

    [Fact]
    public void BuildLineDiff_ProducesAddedRemovedAndUnchangedMarkers()
    {
        using var left = new TempDir();
        using var right = new TempDir();
        var leftFile = left.Write("f.txt", "a\nb\nc\n");
        var rightFile = right.Write("f.txt", "a\nX\nc\nd\n");

        var dm = new DiffManager();
        var diff = dm.BuildLineDiff(leftFile, rightFile);

        Assert.Contains("  a", diff);
        Assert.Contains("- b", diff);
        Assert.Contains("+ X", diff);
        Assert.Contains("+ d", diff);
    }

    [Fact]
    public async Task CompareDirectoriesAsync_MissingDirectory_ThrowsDirectoryNotFoundException()
    {
        using var right = new TempDir();
        var dm = new DiffManager();

        await Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
            dm.CompareDirectoriesAsync(
                Path.Combine(right.Path, "yok-boyle-bir-klasor"), right.Path, CancellationToken.None));
    }

    [Fact]
    public void LooksLikeBinary_TextFile_ReturnsFalse_BinaryFile_ReturnsTrue()
    {
        using var dir = new TempDir();
        var textFile = dir.Write("x.txt", "hello world");
        Assert.False(DiffManager.LooksLikeBinary(textFile));

        var binFile = System.IO.Path.Combine(dir.Path, "x.bin");
        File.WriteAllBytes(binFile, new byte[] { 0x50, 0x4B, 0x00, 0x01, 0x02 });
        Assert.True(DiffManager.LooksLikeBinary(binFile));
    }
}

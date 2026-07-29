using MetadataEditor.Core.Localization;
using MetadataEditor.Core.Models;

namespace MetadataEditor.Core.Services;

public sealed class FileSaveService(IMetadataService metadataService)
{
    public IReadOnlyList<SaveOperation> BuildPlan(SaveRequest request)
    {
        if (request.Mode == SaveMode.CopyToFolder &&
            string.IsNullOrWhiteSpace(request.DestinationFolder))
        {
            throw new InvalidOperationException(CoreLoc.T("CopyFolderNotSelected"));
        }

        var operations = request.Files
            .Select(file => new SaveOperation(file, GetDestinationPath(request, file)))
            .ToList();

        var duplicate = operations
            .GroupBy(
                operation => operation.DestinationPath,
                StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                CoreLoc.F("DuplicateDestinationPath", duplicate.Key));
        }

        foreach (var operation in operations)
        {
            var destinationExists = File.Exists(operation.DestinationPath);
            var isOriginalPath = string.Equals(
                operation.File.SourcePath,
                operation.DestinationPath,
                StringComparison.OrdinalIgnoreCase);

            if (destinationExists &&
                (request.Mode == SaveMode.CopyToFolder || !isOriginalPath))
            {
                throw new InvalidOperationException(
                    CoreLoc.F("FileAlreadyExists", operation.DestinationPath));
            }
        }

        return operations;
    }

    public Task<SaveResult> SaveAsync(
        SaveRequest request,
        IProgress<SaveProgress>? progress,
        CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            var operations = BuildPlan(request);
            var missingSource = operations.FirstOrDefault(
                operation => !File.Exists(operation.File.SourcePath));
            if (missingSource is not null)
            {
                throw new FileNotFoundException(
                    CoreLoc.T("SourceFileNotFound"),
                    missingSource.File.SourcePath);
            }

            var results = new List<SaveItemResult>(operations.Count);
            for (var index = 0; index < operations.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var operation = operations[index];

                try
                {
                    SaveFile(operation, request);
                    results.Add(new SaveItemResult(
                        operation.File.SourcePath,
                        operation.DestinationPath,
                        true,
                        null));
                }
                catch (Exception exception) when (
                    exception is not OperationCanceledException and
                    not OutOfMemoryException)
                {
                    results.Add(new SaveItemResult(
                        operation.File.SourcePath,
                        null,
                        false,
                        exception.Message));
                }

                progress?.Report(new SaveProgress(
                    index + 1,
                    operations.Count,
                    Path.GetFileName(operation.File.SourcePath)));
            }

            return new SaveResult(results);
        }, cancellationToken);
    }

    private void SaveFile(SaveOperation operation, SaveRequest request)
    {
        var destinationDirectory = Path.GetDirectoryName(operation.DestinationPath)
            ?? throw new InvalidOperationException(CoreLoc.T("DestinationFolderUnknown"));
        Directory.CreateDirectory(destinationDirectory);

        var temporaryPath = CreateTemporaryPath(operation.DestinationPath);
        try
        {
            File.Copy(operation.File.SourcePath, temporaryPath);
            metadataService.Write(temporaryPath, operation.File.Metadata);

            if (request.Mode == SaveMode.CopyToFolder)
            {
                File.Move(temporaryPath, operation.DestinationPath);
                return;
            }

            SaveOriginal(operation, temporaryPath, request.CreateBackups);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static void SaveOriginal(
        SaveOperation operation,
        string temporaryPath,
        bool createBackup)
    {
        var source = operation.File.SourcePath;
        var destination = operation.DestinationPath;

        if (createBackup)
        {
            File.Copy(source, CreateBackupPath(source));
        }

        if (string.Equals(source, destination, StringComparison.Ordinal))
        {
            File.Move(temporaryPath, source, overwrite: true);
            return;
        }

        if (string.Equals(source, destination, StringComparison.OrdinalIgnoreCase))
        {
            File.Move(temporaryPath, source, overwrite: true);
            var casingPath = CreateTemporaryPath(source);
            File.Move(source, casingPath);
            File.Move(casingPath, destination);
            return;
        }

        File.Move(temporaryPath, destination);
        try
        {
            File.Delete(source);
        }
        catch
        {
            File.Delete(destination);
            throw;
        }
    }

    private static string GetDestinationPath(
        SaveRequest request,
        SaveFileRequest file)
    {
        var fileName = file.ApplyRename
            ? file.ProposedFileName
            : Path.GetFileName(file.SourcePath);
        if (string.IsNullOrWhiteSpace(fileName) ||
            !string.Equals(fileName, Path.GetFileName(fileName), StringComparison.Ordinal))
        {
            throw new InvalidOperationException(CoreLoc.T("InvalidDestinationFileName"));
        }

        if (request.Mode == SaveMode.ModifyOriginals)
        {
            return Path.Combine(Path.GetDirectoryName(file.SourcePath)!, fileName);
        }

        var destinationRoot = Path.GetFullPath(request.DestinationFolder!);
        return Path.Combine(destinationRoot, fileName);
    }

    private static string CreateTemporaryPath(string targetPath)
    {
        var directory = Path.GetDirectoryName(targetPath)!;
        var baseName = Path.GetFileNameWithoutExtension(targetPath);
        var extension = Path.GetExtension(targetPath);
        return Path.Combine(
            directory,
            $".{baseName}.{Guid.NewGuid():N}.tmp{extension}");
    }

    private static string CreateBackupPath(string sourcePath)
    {
        var candidate = sourcePath + ".bak";
        var index = 2;

        while (File.Exists(candidate))
        {
            candidate = $"{sourcePath}.bak.{index++}";
        }

        return candidate;
    }
}

public sealed record SaveOperation(
    SaveFileRequest File,
    string DestinationPath);

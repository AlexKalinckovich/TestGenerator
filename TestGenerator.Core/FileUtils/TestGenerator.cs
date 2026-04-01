using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TestGenerator.Core.CodeGenerators.Orchestrator;

namespace TestGenerator.Core.FileUtils;

public class TestGenerator
{
    private readonly EnumerationOptions _directoryEnumerationOptions = new()
    {
        RecurseSubdirectories = false,
        AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
        IgnoreInaccessible = true,
        BufferSize = 8192,
        MatchType = MatchType.Simple,
        MatchCasing = MatchCasing.PlatformDefault
    };

    private readonly ConcurrentQueue<string> _directoryQueue;
    private readonly ConcurrentQueue<string> _fileReadQueue;
    private readonly ConcurrentQueue<FileWriteItem> _fileWriteQueue;

    private readonly AutoResetEvent _dirReadEvent;
    private readonly AutoResetEvent _fileReadEvent;
    private readonly AutoResetEvent _fileWriteEvent;

    private readonly TestGeneratorOrchestrator _testGeneratorOrchestrator;
    private readonly string _srcDirectoryPath;
    private readonly string _dstDirectoryPath;

    private int _pendingDirectories;
    private int _pendingFiles;
    private int _pendingWrites;

    private readonly CancellationTokenSource _cts;
    private readonly TaskCompletionSource _completion;

    private readonly int _directoryWorkers;
    private readonly int _fileReadWorkers;
    private readonly int _fileWriteWorkers;

    private readonly Task[] _allWorkers;

    public TestGenerator(
        string srcDirectoryPath,
        string dstDirectoryPath,
        int directoryWorkers = 2,
        int fileReadWorkers = 4,
        int fileWriteWorkers = 2)
    {
        ValidatePaths(srcDirectoryPath, dstDirectoryPath);
        _srcDirectoryPath = srcDirectoryPath;
        _dstDirectoryPath = dstDirectoryPath;
        _directoryWorkers = directoryWorkers;
        _fileReadWorkers = fileReadWorkers;
        _fileWriteWorkers = fileWriteWorkers;

        _directoryQueue = new ConcurrentQueue<string>();
        _fileReadQueue = new ConcurrentQueue<string>();
        _fileWriteQueue = new ConcurrentQueue<FileWriteItem>();

        _dirReadEvent = new AutoResetEvent(false);
        _fileReadEvent = new AutoResetEvent(false);
        _fileWriteEvent = new AutoResetEvent(false);

        _testGeneratorOrchestrator = new TestGeneratorOrchestrator();
        _cts = new CancellationTokenSource();
        _completion = new TaskCompletionSource();

        _allWorkers = CreateAllWorkers();
    }

    private Task[] CreateAllWorkers()
    {
        Task[] directoryWorkerTasks = CreateDirectoryWorkerTasks();
        Task[] fileReadWorkerTasks = CreateFileReadWorkerTasks();
        Task[] fileWriteWorkerTasks = CreateFileWriteWorkerTasks();

        return directoryWorkerTasks
            .Concat(fileReadWorkerTasks)
            .Concat(fileWriteWorkerTasks)
            .ToArray();
    }

    private Task[] CreateDirectoryWorkerTasks()
    {
        return Enumerable.Range(0, _directoryWorkers)
            .Select(_ => Task.Run(DirectoryWorker))
            .ToArray();
    }

    private Task[] CreateFileReadWorkerTasks()
    {
        return Enumerable.Range(0, _fileReadWorkers)
            .Select(_ => Task.Run(FileReadWorker))
            .ToArray();
    }

    private Task[] CreateFileWriteWorkerTasks()
    {
        return Enumerable.Range(0, _fileWriteWorkers)
            .Select(_ => Task.Run(FileWriteWorker))
            .ToArray();
    }

    private void ValidatePaths(string srcDirectoryPath, string dstDirectoryPath)
    {
        AssertSrcDirExists(srcDirectoryPath);
        CreateDstDirIfNeeded(dstDirectoryPath);
    }

    public async Task Run()
    {
        EnqueueRootDirectory();
        await WaitForCompletion();
        await ShutdownWorkers();
    }

    private void EnqueueRootDirectory()
    {
        _directoryQueue.Enqueue(_srcDirectoryPath);
        Interlocked.Increment(ref _pendingDirectories);
        _dirReadEvent.Set();
        DebugLog("Root directory enqueued, signal _dirReadEvent");
    }

    private async Task WaitForCompletion()
    {
        await _completion.Task;
        DebugLog("Completion detected, cancelling workers");
    }

    private async Task ShutdownWorkers()
    {
        await _cts.CancelAsync();
        SignalAllEvents();
        await Task.WhenAll(_allWorkers);
        DebugLog("All workers finished");
    }

    private void SignalAllEvents()
    {
        for (int i = 0; i < _directoryWorkers; i++)
        {
            _dirReadEvent.Set();
        }

        for (int i = 0; i < _fileReadWorkers; i++)
        {
            _fileReadEvent.Set();
        }

        for (int i = 0; i < _fileWriteWorkers; i++)
        {
            _fileWriteEvent.Set();
        }
    }

    private void DirectoryWorker()
    {
        DebugLog($"DirectoryWorker {Task.CurrentId} started");

        while (IsWorkerActive())
        {
            WaitForDirectoryEvent();
            if(!_completion.Task.IsCompleted)
            {
                ProcessDirectoryQueue();
            }
        }

        DebugLog($"DirectoryWorker {Task.CurrentId} exiting");
    }

    private bool IsWorkerActive()
    {
        return !_cts.Token.IsCancellationRequested;
    }

    private void WaitForDirectoryEvent()
    {
        DebugLog($"DirectoryWorker {Task.CurrentId} waiting on _dirReadEvent");
        _dirReadEvent.WaitOne();
        DebugLog($"DirectoryWorker {Task.CurrentId} woke up");
    }

    private void ProcessDirectoryQueue()
    {
        while (_directoryQueue.TryDequeue(out string? dirPath))
        {
            DebugLog($"DirectoryWorker {Task.CurrentId} processing directory: {dirPath}");
            ProcessSingleDirectory(dirPath);
        }
    }

    private void ProcessSingleDirectory(string dirPath)
    {
        try
        {
            ProcessDirectory(dirPath);
        }
        finally
        {
            DecrementPendingDirectories();
        }
    }

    private void ProcessDirectory(string dirPath)
    {
        EnqueueFilesFromDirectory(dirPath);
        EnqueueSubdirectories(dirPath);
    }

    private void EnqueueFilesFromDirectory(string dirPath)
    {
        foreach (string file in Directory.EnumerateFiles(dirPath, "*.cs", SearchOption.TopDirectoryOnly))
        {
            _fileReadQueue.Enqueue(file);
            Interlocked.Increment(ref _pendingFiles);
            _fileReadEvent.Set();
            DebugLog($"Enqueued file for read: {file}, pendingFiles={_pendingFiles}");
        }
    }

    private void EnqueueSubdirectories(string dirPath)
    {
        foreach (string subDir in Directory.EnumerateDirectories(dirPath, "*", _directoryEnumerationOptions))
        {
            _directoryQueue.Enqueue(subDir);
            Interlocked.Increment(ref _pendingDirectories);
            _dirReadEvent.Set();
            DebugLog($"Enqueued subdirectory: {subDir}, pendingDirectories={_pendingDirectories}");
        }
    }

    private void DecrementPendingDirectories()
    {
        int remainingDirectories = Interlocked.Decrement(ref _pendingDirectories);
        DebugLog($"DirectoryWorker {Task.CurrentId} decremented _pendingDirectories to {remainingDirectories}");
        CheckCompletionCondition();
    }

    private void CheckCompletionCondition()
    {
        int pendingFiles = Interlocked.Add(ref _pendingFiles, 0);
        int pendingWrites = Interlocked.Add(ref _pendingWrites, 0);
        int remainingDirectories = Interlocked.Add(ref _pendingDirectories, 0);

        DebugLog($"pendingFiles={pendingFiles}, pendingWrites={pendingWrites}, remainingDirectories={remainingDirectories}");

        if (IsAllWorkComplete(remainingDirectories, pendingFiles, pendingWrites))
        {
            SetCompletionSource();
        }
    }

    private bool IsAllWorkComplete(int remainingDirectories, int pendingFiles, int pendingWrites)
    {
        return remainingDirectories == 0 && pendingFiles == 0 && pendingWrites == 0;
    }

    private void SetCompletionSource()
    {
        DebugLog("All work completed, setting completion source");
        _completion.TrySetResult();
    }

    private void FileReadWorker()
    {
        DebugLog($"FileReadWorker {Task.CurrentId} started");

        while (IsWorkerActive())
        {
            WaitForFileReadEvent();
            if (!_completion.Task.IsCompleted)
            {
                ProcessFileReadQueue();
            }
        }

        DebugLog($"FileReadWorker {Task.CurrentId} exiting");
    }

    private void WaitForFileReadEvent()
    {
        DebugLog($"FileReadWorker {Task.CurrentId} waiting on _fileReadEvent");
        _fileReadEvent.WaitOne();
        DebugLog($"FileReadWorker {Task.CurrentId} woke up");
    }

    private void ProcessFileReadQueue()
    {
        while (_fileReadQueue.TryDequeue(out string? filePath))
        {
            DebugLog($"FileReadWorker {Task.CurrentId} processing file: {filePath}");
            ProcessSingleFileRead(filePath);
        }
    }

    private void ProcessSingleFileRead(string filePath)
    {
        string generatedCode = _testGeneratorOrchestrator.GenerateToString(filePath);
        string destinationFilePath = GetDestinationFilePath(filePath);
        EnqueueFileWrite(destinationFilePath, generatedCode);
        DecrementPendingFiles();
    }

    private void EnqueueFileWrite(string destinationFilePath, string generatedCode)
    {
        _fileWriteQueue.Enqueue(new FileWriteItem(destinationFilePath, generatedCode));
        Interlocked.Increment(ref _pendingWrites);
        _fileWriteEvent.Set();
        DebugLog($"Enqueued write for {destinationFilePath}, pendingWrites={_pendingWrites}");
    }

    private void DecrementPendingFiles()
    {
        Interlocked.Decrement(ref _pendingFiles);
        CheckCompletionCondition();
    }

    private void FileWriteWorker()
    {
        DebugLog($"FileWriteWorker {Task.CurrentId} started");

        while (IsWorkerActive())
        {
            WaitForFileWriteEvent();
            if (!_completion.Task.IsCompleted)
            {
                ProcessFileWriteQueue();
            }
        }

        DebugLog($"FileWriteWorker {Task.CurrentId} exiting");
    }

    private void WaitForFileWriteEvent()
    {
        DebugLog($"FileWriteWorker {Task.CurrentId} waiting on _fileWriteEvent");
        _fileWriteEvent.WaitOne();
        DebugLog($"FileWriteWorker {Task.CurrentId} woke up");
    }

    private void ProcessFileWriteQueue()
    {
        while (_fileWriteQueue.TryDequeue(out FileWriteItem item))
        {
            DebugLog($"FileWriteWorker {Task.CurrentId} writing file: {item.FilePath}");
            WriteSingleFile(item);
        }
    }

    private void WriteSingleFile(FileWriteItem item)
    {
        EnsureDirectoryExists(item.FilePath);
        File.WriteAllText(item.FilePath, item.Content);
        DecrementPendingWrites();
    }

    private void EnsureDirectoryExists(string filePath)
    {
        string? directory = Path.GetDirectoryName(filePath);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private void DecrementPendingWrites()
    {
        Interlocked.Decrement(ref _pendingWrites);
        CheckCompletionCondition();
    }

    private string GetDestinationFilePath(string sourceFilePath)
    {
        string relativePath = Path.GetRelativePath(_srcDirectoryPath, sourceFilePath);
        string relativeDir = GetRelativeDirectory(relativePath);
        string fileName = BuildTestFileName(sourceFilePath);
        return Path.Combine(_dstDirectoryPath, relativeDir, fileName);
    }

    private string GetRelativeDirectory(string relativePath)
    {
        return Path.GetDirectoryName(relativePath) ?? string.Empty;
    }

    private string BuildTestFileName(string sourceFilePath)
    {
        return Path.GetFileNameWithoutExtension(sourceFilePath) + ".Tests.cs";
    }

    private static void CreateDstDirIfNeeded(string dstDirectoryPath)
    {
        if (!Directory.Exists(dstDirectoryPath))
        {
            Directory.CreateDirectory(dstDirectoryPath);
        }
    }

    private static void AssertSrcDirExists(string srcDirectoryPath)
    {
        if (!Directory.Exists(srcDirectoryPath))
        {
            throw new DirectoryNotFoundException($"Source directory does not exist: {srcDirectoryPath}");
        }
    }

    [Conditional("DEBUG")]
    private static void DebugLog(string message)
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}][Thread {Thread.CurrentThread.ManagedThreadId}] {message}");
    }

    
}
// =============================================================================
// Workflow - Mesin eksekusi graph workflow yang menjalankan executor secara terurut
// Mendukung sequential, parallel (fan-out/fan-in), conditional routing, dan retry
// =============================================================================

using System.Diagnostics;

namespace Workflows.Executors;

/// <summary>
/// Mesin eksekusi workflow graph yang menjalankan node berdasarkan topologi edges.
/// Mendukung tiga pola eksekusi: sequential, parallel, dan conditional.
/// </summary>
public class Workflow
{
    // Semua node dalam graph yang diindeks berdasarkan ExecutorId
    private readonly Dictionary<string, IWorkflowExecutor> _nodes;

    // Semua edge yang mendefinisikan koneksi antar node
    private readonly List<WorkflowEdge> _edges;

    // ID node pertama yang dijalankan saat workflow dimulai
    private readonly string _entryNodeId;

    // Kondisi untuk menentukan output final workflow
    private readonly List<OutputCondition> _outputConditions;

    // Maksimal retry per executor sebelum dinyatakan gagal permanen
    private readonly int _maxRetries;

    // Daftar event yang terjadi selama eksekusi (untuk monitoring)
    private readonly List<WorkflowEvent> _events = new();

    // Status setiap node dalam workflow
    private readonly Dictionary<string, StepStatus> _stepStatuses = new();

    /// <summary>
    /// Mengakses daftar event yang terjadi selama eksekusi workflow.
    /// Digunakan untuk real-time visualization dan monitoring.
    /// </summary>
    public IReadOnlyList<WorkflowEvent> Events => _events.AsReadOnly();

    /// <summary>
    /// Mengakses status setiap step dalam workflow.
    /// </summary>
    public IReadOnlyDictionary<string, StepStatus> StepStatuses =>
        _stepStatuses.AsReadOnly();

    /// <summary>
    /// Konstruktor internal, dibuat melalui WorkflowBuilder.Build().
    /// </summary>
    internal Workflow(
        List<IWorkflowExecutor> nodes,
        List<WorkflowEdge> edges,
        string entryNodeId,
        List<OutputCondition> outputConditions,
        int maxRetries)
    {
        // Mengindeks node berdasarkan ID untuk akses cepat
        _nodes = nodes.ToDictionary(n => n.ExecutorId);
        _edges = edges;
        _entryNodeId = entryNodeId;
        _outputConditions = outputConditions;
        _maxRetries = maxRetries;

        // Inisialisasi status semua node sebagai Pending
        foreach (var node in nodes)
        {
            _stepStatuses[node.ExecutorId] = StepStatus.Pending;
        }
    }

    /// <summary>
    /// Menjalankan workflow dari entry node hingga output atau kegagalan.
    /// Menggunakan BFS traversal pada graph untuk menentukan urutan eksekusi.
    /// </summary>
    /// <param name="input">Input awal untuk entry node</param>
    /// <param name="cancellationToken">Token pembatalan operasi</param>
    /// <returns>Hasil akhir dari workflow (output dari node terakhir)</returns>
    public async Task<WorkflowRunResult> RunAsync(
        string input, CancellationToken cancellationToken = default)
    {
        // Menyimpan hasil eksekusi setiap node untuk diteruskan ke node berikutnya
        var nodeResults = new Dictionary<string, ExecutorResult>();

        // Antrian node yang perlu dieksekusi (BFS traversal)
        var executionQueue = new Queue<(string NodeId, string Input)>();
        executionQueue.Enqueue((_entryNodeId, input));

        // Proteksi terhadap loop tak terbatas (approve/reject loop dibatasi)
        var maxIterations = 50;
        var iterationCount = 0;

        while (executionQueue.Count > 0 && iterationCount < maxIterations)
        {
            iterationCount++;
            cancellationToken.ThrowIfCancellationRequested();

            // Mengambil node berikutnya dari antrian
            var (currentNodeId, currentInput) = executionQueue.Dequeue();

            // Menjalankan executor dengan mekanisme retry
            var result = await ExecuteWithRetryAsync(currentNodeId, currentInput, cancellationToken);
            nodeResults[currentNodeId] = result;

            // Jika executor gagal permanen, hentikan jalur ini
            if (!result.IsSuccess)
            {
                // Mencatat downstream yang terdampak
                var downstream = GetDownstreamNodes(currentNodeId);
                _events.Add(new ExecutorFailedPermanentlyEvent
                {
                    ExecutorId = currentNodeId,
                    ErrorMessage = result.ErrorMessage ?? "Unknown error",
                    TotalAttempts = _maxRetries,
                    AffectedDownstream = downstream
                });

                // Menandai downstream sebagai Skipped
                foreach (var nodeId in downstream)
                {
                    _stepStatuses[nodeId] = StepStatus.Skipped;
                }

                continue;
            }

            // Memeriksa apakah kondisi output final terpenuhi
            var outputCondition = _outputConditions.FirstOrDefault(
                oc => oc.ExecutorId == currentNodeId);
            if (outputCondition != null && outputCondition.Condition(result))
            {
                // Workflow selesai dengan output final
                return new WorkflowRunResult
                {
                    IsSuccess = true,
                    FinalOutput = result.Output,
                    CompletedByNode = currentNodeId,
                    Events = _events.ToList(),
                    StepStatuses = new Dictionary<string, StepStatus>(_stepStatuses)
                };
            }

            // Menentukan edge yang akan dilalui berdasarkan kondisi
            var outgoingEdges = _edges.Where(e => e.SourceId == currentNodeId).ToList();

            // Mengumpulkan target node yang memenuhi kondisi (untuk parallel execution)
            var nextNodes = new List<string>();
            foreach (var edge in outgoingEdges)
            {
                // Evaluasi kondisi edge (jika ada)
                if (edge.Condition == null || edge.Condition(result))
                {
                    nextNodes.Add(edge.TargetId);
                }
            }

            // Jika ada multiple target: fan-out (parallel execution)
            if (nextNodes.Count > 1)
            {
                // Menjalankan semua target secara paralel
                var parallelResults = await ExecuteParallelAsync(
                    nextNodes, result.Output, cancellationToken);

                // Menyimpan hasil parallel dan menentukan node berikutnya
                foreach (var (nodeId, parallelResult) in parallelResults)
                {
                    nodeResults[nodeId] = parallelResult;

                    // Mencari edge keluar dari setiap parallel node
                    var fanInEdges = _edges
                        .Where(e => e.SourceId == nodeId)
                        .ToList();

                    foreach (var fanInEdge in fanInEdges)
                    {
                        if (fanInEdge.Condition == null || fanInEdge.Condition(parallelResult))
                        {
                            // Fan-in: mengkombinasikan output parallel sebagai input
                            var combinedInput = string.Join("\n---\n",
                                parallelResults
                                    .Where(pr => pr.Value.IsSuccess)
                                    .Select(pr => pr.Value.Output));

                            // Hanya enqueue fan-in target sekali
                            if (!executionQueue.Any(q => q.NodeId == fanInEdge.TargetId))
                            {
                                executionQueue.Enqueue((fanInEdge.TargetId, combinedInput));
                            }
                        }
                    }
                }
            }
            else if (nextNodes.Count == 1)
            {
                // Sequential: satu target berikutnya
                executionQueue.Enqueue((nextNodes[0], result.Output));
            }
            // Jika nextNodes.Count == 0: dead-end, tidak ada node berikutnya
        }

        // Jika workflow selesai tanpa memenuhi output condition
        var lastResult = nodeResults.Values.LastOrDefault();
        return new WorkflowRunResult
        {
            IsSuccess = lastResult?.IsSuccess ?? false,
            FinalOutput = lastResult?.Output ?? string.Empty,
            CompletedByNode = nodeResults.Keys.LastOrDefault() ?? _entryNodeId,
            Events = _events.ToList(),
            StepStatuses = new Dictionary<string, StepStatus>(_stepStatuses)
        };
    }

    /// <summary>
    /// Menjalankan executor dengan mekanisme retry (maks 3 percobaan).
    /// Setiap kegagalan akan di-retry sebelum dinyatakan gagal permanen.
    /// </summary>
    private async Task<ExecutorResult> ExecuteWithRetryAsync(
        string nodeId, string input, CancellationToken cancellationToken)
    {
        var executor = _nodes[nodeId];

        for (int attempt = 1; attempt <= _maxRetries; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Mencatat event mulai eksekusi
            _stepStatuses[nodeId] = StepStatus.Running;
            _events.Add(new ExecutorStartedEvent
            {
                ExecutorId = nodeId,
                AttemptNumber = attempt
            });

            // Mengukur durasi eksekusi
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Menjalankan executor
                var result = await executor.ExecuteAsync(input, cancellationToken);
                stopwatch.Stop();

                if (result.IsSuccess)
                {
                    // Berhasil: update status dan catat event
                    _stepStatuses[nodeId] = StepStatus.Completed;
                    _events.Add(new ExecutorCompletedEvent
                    {
                        ExecutorId = nodeId,
                        IsSuccess = true,
                        DurationMs = stopwatch.ElapsedMilliseconds,
                        AttemptNumber = attempt
                    });
                    return result;
                }

                // Executor mengembalikan failure (bukan exception)
                if (attempt < _maxRetries)
                {
                    // Mencatat event retry
                    _events.Add(new ExecutorRetryEvent
                    {
                        ExecutorId = nodeId,
                        NextAttemptNumber = attempt + 1,
                        Reason = result.ErrorMessage ?? "Eksekusi gagal"
                    });

                    // Menampilkan info retry ke console
                    DisplayRetryInfo(nodeId, attempt, result.ErrorMessage ?? "Unknown");
                }
                else
                {
                    // Semua percobaan habis
                    _stepStatuses[nodeId] = StepStatus.Failed;
                    _events.Add(new ExecutorCompletedEvent
                    {
                        ExecutorId = nodeId,
                        IsSuccess = false,
                        DurationMs = stopwatch.ElapsedMilliseconds,
                        AttemptNumber = attempt,
                        ErrorMessage = result.ErrorMessage
                    });
                    return result;
                }
            }
            catch (OperationCanceledException)
            {
                throw; // Meneruskan pembatalan user
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                if (attempt < _maxRetries)
                {
                    // Mencatat retry karena exception
                    _events.Add(new ExecutorRetryEvent
                    {
                        ExecutorId = nodeId,
                        NextAttemptNumber = attempt + 1,
                        Reason = ex.Message
                    });

                    DisplayRetryInfo(nodeId, attempt, ex.Message);
                }
                else
                {
                    // Gagal permanen karena exception
                    _stepStatuses[nodeId] = StepStatus.Failed;
                    _events.Add(new ExecutorCompletedEvent
                    {
                        ExecutorId = nodeId,
                        IsSuccess = false,
                        DurationMs = stopwatch.ElapsedMilliseconds,
                        AttemptNumber = attempt,
                        ErrorMessage = ex.Message
                    });

                    return new ExecutorResult
                    {
                        IsSuccess = false,
                        ErrorMessage = ex.Message
                    };
                }
            }
        }

        // Seharusnya tidak tercapai, safety fallback
        return new ExecutorResult { IsSuccess = false, ErrorMessage = "Unexpected state" };
    }

    /// <summary>
    /// Menjalankan multiple executor secara paralel (fan-out pattern).
    /// Semua executor dijalankan bersamaan dan hasilnya dikumpulkan.
    /// </summary>
    private async Task<Dictionary<string, ExecutorResult>> ExecuteParallelAsync(
        List<string> nodeIds, string input, CancellationToken cancellationToken)
    {
        var results = new Dictionary<string, ExecutorResult>();

        // Membuat task paralel untuk setiap node
        var tasks = nodeIds.Select(async nodeId =>
        {
            var result = await ExecuteWithRetryAsync(nodeId, input, cancellationToken);
            return (nodeId, result);
        }).ToList();

        // Menunggu semua task selesai
        var completedTasks = await Task.WhenAll(tasks);

        // Mengumpulkan hasil dari semua task
        foreach (var (nodeId, result) in completedTasks)
        {
            results[nodeId] = result;
        }

        return results;
    }

    /// <summary>
    /// Mendapatkan semua node downstream dari node yang gagal.
    /// Digunakan untuk menentukan dampak kegagalan pada workflow.
    /// </summary>
    private List<string> GetDownstreamNodes(string nodeId)
    {
        var downstream = new List<string>();
        var visited = new HashSet<string>();
        var queue = new Queue<string>();

        // Mencari semua node yang terhubung dari node gagal
        var directTargets = _edges
            .Where(e => e.SourceId == nodeId)
            .Select(e => e.TargetId);

        foreach (var target in directTargets)
        {
            queue.Enqueue(target);
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (visited.Contains(current)) continue;

            visited.Add(current);
            downstream.Add(current);

            // Mencari node yang terhubung dari current
            var nextTargets = _edges
                .Where(e => e.SourceId == current)
                .Select(e => e.TargetId);

            foreach (var next in nextTargets)
            {
                if (!visited.Contains(next))
                    queue.Enqueue(next);
            }
        }

        return downstream;
    }

    /// <summary>
    /// Menampilkan informasi retry ke console untuk visualisasi real-time.
    /// </summary>
    private static void DisplayRetryInfo(string nodeId, int attempt, string reason)
    {
        Console.WriteLine($"  [⟳ RETRY] {nodeId}: percobaan {attempt}/{3} gagal - {reason}");
        Console.WriteLine($"            Menunggu sebelum percobaan berikutnya...");
    }
}

/// <summary>
/// Hasil akhir dari eksekusi workflow secara keseluruhan.
/// </summary>
public record WorkflowRunResult
{
    /// <summary>
    /// Apakah workflow berhasil mencapai output final.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Output final dari workflow.
    /// </summary>
    public string FinalOutput { get; init; } = string.Empty;

    /// <summary>
    /// ID node yang menghasilkan output final.
    /// </summary>
    public string CompletedByNode { get; init; } = string.Empty;

    /// <summary>
    /// Semua event yang terjadi selama eksekusi.
    /// </summary>
    public List<WorkflowEvent> Events { get; init; } = new();

    /// <summary>
    /// Status akhir setiap step dalam workflow.
    /// </summary>
    public Dictionary<string, StepStatus> StepStatuses { get; init; } = new();
}

/// <summary>
/// Enum status setiap langkah (step) dalam workflow.
/// </summary>
public enum StepStatus
{
    // Belum dimulai
    Pending,

    // Sedang berjalan
    Running,

    // Berhasil selesai
    Completed,

    // Gagal permanen (semua retry habis)
    Failed,

    // Dilewati karena dependency gagal
    Skipped
}

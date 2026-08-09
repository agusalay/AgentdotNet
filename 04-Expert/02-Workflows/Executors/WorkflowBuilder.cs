// =============================================================================
// WorkflowBuilder - Membangun directed graph workflow secara deklaratif
// Mendukung sequential, parallel (fan-out/fan-in), dan conditional routing
// =============================================================================

namespace Workflows.Executors;

/// <summary>
/// Builder untuk mendefinisikan workflow graph secara deklaratif.
/// Menggunakan pola fluent API untuk menambahkan node dan edge.
/// </summary>
public class WorkflowBuilder
{
    // Daftar semua node (executor) dalam graph
    private readonly List<IWorkflowExecutor> _nodes = new();

    // Daftar semua edge (koneksi antar node) dengan kondisi opsional
    private readonly List<WorkflowEdge> _edges = new();

    // Node awal (entry point) dari workflow
    private readonly IWorkflowExecutor _entryNode;

    // Kondisi output: node mana yang menghasilkan output final
    private readonly List<OutputCondition> _outputConditions = new();

    // Konfigurasi retry: maksimal percobaan per executor
    private int _maxRetries = 3;

    /// <summary>
    /// Membuat WorkflowBuilder dengan entry node sebagai titik mulai graph.
    /// </summary>
    /// <param name="entryNode">Executor pertama yang akan dijalankan saat workflow dimulai</param>
    public WorkflowBuilder(IWorkflowExecutor entryNode)
    {
        // Menyimpan dan mendaftarkan entry node sebagai node pertama
        _entryNode = entryNode;
        _nodes.Add(entryNode);
    }

    /// <summary>
    /// Menambahkan edge (koneksi) antara dua executor tanpa kondisi.
    /// Data output dari source akan diteruskan ke target.
    /// </summary>
    /// <param name="source">Executor sumber (dari)</param>
    /// <param name="target">Executor tujuan (ke)</param>
    /// <returns>Builder ini untuk chaining method calls</returns>
    public WorkflowBuilder AddEdge(IWorkflowExecutor source, IWorkflowExecutor target)
    {
        // Mendaftarkan node jika belum terdaftar
        RegisterNodeIfNew(source);
        RegisterNodeIfNew(target);

        // Menambahkan edge tanpa kondisi (selalu dieksekusi)
        _edges.Add(new WorkflowEdge(source.ExecutorId, target.ExecutorId));
        return this;
    }

    /// <summary>
    /// Menambahkan edge dengan kondisi. Edge hanya dilalui jika condition bernilai true.
    /// Digunakan untuk conditional routing (contoh: approve/reject loop).
    /// </summary>
    /// <param name="source">Executor sumber (dari)</param>
    /// <param name="target">Executor tujuan (ke)</param>
    /// <param name="condition">Fungsi kondisi yang mengevaluasi result dari source</param>
    /// <returns>Builder ini untuk chaining method calls</returns>
    public WorkflowBuilder AddEdge(
        IWorkflowExecutor source,
        IWorkflowExecutor target,
        Func<ExecutorResult, bool> condition)
    {
        // Mendaftarkan node jika belum terdaftar
        RegisterNodeIfNew(source);
        RegisterNodeIfNew(target);

        // Menambahkan edge dengan kondisi evaluasi
        _edges.Add(new WorkflowEdge(source.ExecutorId, target.ExecutorId, condition));
        return this;
    }

    /// <summary>
    /// Mendefinisikan output final workflow dari executor tertentu dengan kondisi.
    /// Workflow selesai ketika kondisi output terpenuhi.
    /// </summary>
    /// <param name="outputExecutor">Executor yang menghasilkan output final</param>
    /// <param name="condition">Kondisi yang harus terpenuhi untuk mengeluarkan output</param>
    /// <returns>Builder ini untuk chaining method calls</returns>
    public WorkflowBuilder WithOutputFrom(
        IWorkflowExecutor outputExecutor,
        Func<ExecutorResult, bool> condition)
    {
        // Mendaftarkan kondisi output final workflow
        _outputConditions.Add(new OutputCondition(outputExecutor.ExecutorId, condition));
        return this;
    }

    /// <summary>
    /// Mengatur jumlah maksimal retry untuk setiap executor yang gagal.
    /// </summary>
    /// <param name="maxRetries">Jumlah retry maksimal (default: 3)</param>
    /// <returns>Builder ini untuk chaining method calls</returns>
    public WorkflowBuilder WithMaxRetries(int maxRetries)
    {
        _maxRetries = maxRetries;
        return this;
    }

    /// <summary>
    /// Membangun workflow yang siap dijalankan dari definisi graph.
    /// </summary>
    /// <returns>Instance Workflow yang dapat dieksekusi</returns>
    public Workflow Build()
    {
        // Memvalidasi graph sebelum build
        ValidateGraph();

        // Membuat instance workflow dengan semua konfigurasi
        return new Workflow(
            _nodes.ToList(),
            _edges.ToList(),
            _entryNode.ExecutorId,
            _outputConditions.ToList(),
            _maxRetries);
    }

    // Mendaftarkan node ke daftar jika belum ada
    private void RegisterNodeIfNew(IWorkflowExecutor executor)
    {
        if (!_nodes.Any(n => n.ExecutorId == executor.ExecutorId))
        {
            _nodes.Add(executor);
        }
    }

    // Validasi bahwa graph terdefinisi dengan benar
    private void ValidateGraph()
    {
        if (_nodes.Count == 0)
            throw new InvalidOperationException("Workflow harus memiliki minimal satu node.");

        if (_edges.Count == 0)
            throw new InvalidOperationException("Workflow harus memiliki minimal satu edge.");

        // Memastikan semua edge mereferensikan node yang terdaftar
        foreach (var edge in _edges)
        {
            if (!_nodes.Any(n => n.ExecutorId == edge.SourceId))
                throw new InvalidOperationException(
                    $"Edge mereferensikan source '{edge.SourceId}' yang tidak terdaftar.");

            if (!_nodes.Any(n => n.ExecutorId == edge.TargetId))
                throw new InvalidOperationException(
                    $"Edge mereferensikan target '{edge.TargetId}' yang tidak terdaftar.");
        }
    }
}

/// <summary>
/// Merepresentasikan koneksi (edge) antar dua node dalam workflow graph.
/// Edge bisa memiliki kondisi opsional yang menentukan apakah edge dilalui.
/// </summary>
public record WorkflowEdge(
    string SourceId,
    string TargetId,
    Func<ExecutorResult, bool>? Condition = null);

/// <summary>
/// Kondisi untuk menentukan apakah workflow menghasilkan output final
/// dari executor tertentu.
/// </summary>
public record OutputCondition(
    string ExecutorId,
    Func<ExecutorResult, bool> Condition);

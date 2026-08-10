// KnowledgeBaseCompletions.cs — Handler auto-completion untuk Knowledge Base
// Completions menyediakan saran otomatis saat user mengetik parameter pada:
// 1. Prompt arguments — contoh: saran articleId saat mengisi parameter prompt "summarize-article"
// 2. Resource template variables — contoh: saran categoryName saat mengakses template URI
//
// Mekanisme completions meningkatkan UX karena user tidak perlu mengingat
// semua ID artikel atau nama kategori yang tersedia di knowledge base.

using McpAdvanced.Server.Models;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace McpAdvanced.Server.Completions;

/// <summary>
/// Handler untuk auto-completion pada prompt arguments dan resource template variables.
/// Menggunakan prefix-matching: hanya saran yang dimulai dengan teks yang sudah diketik
/// user yang akan dikembalikan. Ini memastikan saran selalu relevan.
/// </summary>
public static class KnowledgeBaseCompletions
{
    /// <summary>
    /// Handler utama yang diregistrasi melalui WithCompleteHandler pada Program.cs.
    /// Menerima request completion dari client dan mengembalikan saran berdasarkan
    /// tipe referensi (prompt atau resource template) dan nama argumen.
    /// </summary>
    /// <param name="context">Konteks request berisi parameter completion dari client</param>
    /// <param name="cancellationToken">Token pembatalan untuk operasi async</param>
    /// <returns>CompleteResult berisi daftar saran yang cocok dengan prefix</returns>
    public static ValueTask<CompleteResult> HandleCompleteAsync(
        RequestContext<CompleteRequestParams> context,
        CancellationToken cancellationToken)
    {
        var parameters = context.Params
            ?? throw new NotSupportedException("Parameter completion tidak boleh null.");

        var reference = parameters.Ref;
        var argument = parameters.Argument;

        // Ambil KnowledgeBaseStore dari DI container untuk mengakses data aktual
        var store = context.Services?.GetRequiredService<KnowledgeBaseStore>()
            ?? throw new InvalidOperationException("KnowledgeBaseStore tidak tersedia di DI container.");

        // Ambil nama argumen dan nilai parsial yang sudah diketik user
        var argumentName = argument.Name;
        var argumentValue = argument.Value;

        // Tentukan tipe completion berdasarkan referensi yang diterima
        if (reference is PromptReference)
        {
            // Completion untuk argumen pada prompt templates
            return ValueTask.FromResult(CompletePromptArgument(argumentName, argumentValue, store));
        }

        if (reference is ResourceTemplateReference)
        {
            // Completion untuk variabel pada resource URI templates
            return ValueTask.FromResult(CompleteResourceTemplateVariable(argumentName, argumentValue, store));
        }

        // Tipe referensi tidak dikenali — kembalikan hasil kosong
        return ValueTask.FromResult(new CompleteResult
        {
            Completion = new Completion
            {
                Values = [],
                HasMore = false,
                Total = 0
            }
        });
    }

    /// <summary>
    /// Menyediakan completion untuk argumen prompt.
    /// Saat ini mendukung argumen "articleId" yang ada pada prompt:
    /// - "summarize-article" (parameter articleId)
    /// - "compare-articles" (parameter articleId1, articleId2)
    /// Saran diambil dari daftar artikel ID yang tersedia di store.
    /// </summary>
    /// <param name="argumentName">Nama argumen yang sedang di-complete</param>
    /// <param name="argumentValue">Nilai parsial yang sudah diketik user</param>
    /// <param name="store">Knowledge base store untuk mengambil data artikel</param>
    /// <returns>CompleteResult dengan saran article ID yang cocok</returns>
    private static CompleteResult CompletePromptArgument(
        string argumentName,
        string argumentValue,
        KnowledgeBaseStore store)
    {
        // Hanya berikan completion untuk argumen yang berkaitan dengan articleId
        // Argumen lain (query, language) tidak memerlukan completion dari data store
        if (argumentName.Equals("articleId", StringComparison.OrdinalIgnoreCase) ||
            argumentName.Equals("articleId1", StringComparison.OrdinalIgnoreCase) ||
            argumentName.Equals("articleId2", StringComparison.OrdinalIgnoreCase))
        {
            // Ambil semua article ID dari store
            var allArticleIds = store.Articles.Keys.ToList();

            // Filter berdasarkan prefix — hanya kembalikan ID yang dimulai dengan input user
            var matchingIds = allArticleIds
                .Where(id => id.StartsWith(argumentValue, StringComparison.OrdinalIgnoreCase))
                .OrderBy(id => id)
                .ToList();

            return new CompleteResult
            {
                Completion = new Completion
                {
                    Values = matchingIds,
                    HasMore = false,
                    Total = matchingIds.Count
                }
            };
        }

        // Argumen tidak dikenali — kembalikan hasil kosong
        return new CompleteResult
        {
            Completion = new Completion
            {
                Values = [],
                HasMore = false,
                Total = 0
            }
        };
    }

    /// <summary>
    /// Menyediakan completion untuk variabel pada resource URI templates.
    /// Mendukung variabel "categoryName" yang digunakan pada template:
    /// - kb://categories/{categoryName}/articles
    /// Dan variabel "articleId" pada template:
    /// - kb://articles/{articleId}
    /// Saran diambil dari daftar nama kategori atau ID artikel yang tersedia di store.
    /// </summary>
    /// <param name="argumentName">Nama variabel yang sedang di-complete</param>
    /// <param name="argumentValue">Nilai parsial yang sudah diketik user</param>
    /// <param name="store">Knowledge base store untuk mengambil data</param>
    /// <returns>CompleteResult dengan saran yang cocok</returns>
    private static CompleteResult CompleteResourceTemplateVariable(
        string argumentName,
        string argumentValue,
        KnowledgeBaseStore store)
    {
        // Completion untuk variabel categoryName pada resource template
        if (argumentName.Equals("categoryName", StringComparison.OrdinalIgnoreCase))
        {
            // Ambil semua nama kategori dari store
            var allCategoryNames = store.Categories.Values
                .Select(c => c.Name)
                .ToList();

            // Filter berdasarkan prefix — hanya kembalikan nama yang dimulai dengan input user
            var matchingNames = allCategoryNames
                .Where(name => name.StartsWith(argumentValue, StringComparison.OrdinalIgnoreCase))
                .OrderBy(name => name)
                .ToList();

            return new CompleteResult
            {
                Completion = new Completion
                {
                    Values = matchingNames,
                    HasMore = false,
                    Total = matchingNames.Count
                }
            };
        }

        // Completion untuk variabel articleId pada resource template kb://articles/{articleId}
        if (argumentName.Equals("articleId", StringComparison.OrdinalIgnoreCase))
        {
            // Ambil semua article ID dari store
            var allArticleIds = store.Articles.Keys.ToList();

            // Filter berdasarkan prefix
            var matchingIds = allArticleIds
                .Where(id => id.StartsWith(argumentValue, StringComparison.OrdinalIgnoreCase))
                .OrderBy(id => id)
                .ToList();

            return new CompleteResult
            {
                Completion = new Completion
                {
                    Values = matchingIds,
                    HasMore = false,
                    Total = matchingIds.Count
                }
            };
        }

        // Variabel tidak dikenali — kembalikan hasil kosong
        return new CompleteResult
        {
            Completion = new Completion
            {
                Values = [],
                HasMore = false,
                Total = 0
            }
        };
    }
}

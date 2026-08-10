using System.ComponentModel;
using McpAdvanced.Server.Models;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace McpAdvanced.Server.Prompts;

/// <summary>
/// Kelas yang mengekspos prompt templates untuk Knowledge Base melalui MCP protocol.
/// Setiap method yang ditandai [McpServerPrompt] menjadi prompt yang dapat ditemukan
/// dan dipanggil oleh MCP client. Prompt menghasilkan pesan terstruktur yang siap
/// digunakan oleh LLM untuk melakukan operasi pada knowledge base.
/// </summary>
[McpServerPromptType]
public class KnowledgeBasePrompts
{
    /// <summary>
    /// Prompt untuk mencari artikel di knowledge base berdasarkan query.
    /// Mengembalikan hasil pencarian sebagai embedded resource references,
    /// sehingga client dapat langsung mengakses artikel yang relevan.
    /// </summary>
    /// <param name="query">Kata kunci pencarian untuk menemukan artikel yang relevan</param>
    /// <param name="store">Store knowledge base yang di-inject melalui DI</param>
    /// <returns>Daftar PromptMessage berisi instruksi dan hasil pencarian</returns>
    [McpServerPrompt(Name = "search-knowledge-base")]
    [Description("Searches the knowledge base for relevant articles")]
    public static IEnumerable<PromptMessage> SearchKnowledgeBase(
        [Description("The search query")] string query,
        KnowledgeBaseStore store)
    {
        // Lakukan pencarian di store menggunakan query yang diberikan
        var results = store.SearchArticles(query).ToList();

        // Pesan pertama: instruksi untuk LLM dengan konteks pencarian
        yield return new PromptMessage
        {
            Role = Role.User,
            Content = new TextContentBlock
            {
                Text = $"Search the knowledge base for: \"{query}\"\n\n" +
                       $"Found {results.Count} article(s). " +
                       "Please analyze the following results and provide a helpful summary."
            }
        };

        if (results.Count == 0)
        {
            // Tidak ada hasil — berikan instruksi fallback ke LLM
            yield return new PromptMessage
            {
                Role = Role.User,
                Content = new TextContentBlock
                {
                    Text = "No articles found matching the query. " +
                           "Please suggest alternative search terms or related topics."
                }
            };
        }
        else
        {
            // Sertakan setiap artikel yang ditemukan sebagai embedded resource reference
            // Embedded resource memungkinkan client mereferensikan konten secara langsung
            foreach (var article in results)
            {
                // Tambahkan metadata artikel sebagai teks konteks
                yield return new PromptMessage
                {
                    Role = Role.User,
                    Content = new TextContentBlock
                    {
                        Text = $"Article: \"{article.Title}\" (ID: {article.Id}, " +
                               $"Category: {article.CategoryId}, Tags: {string.Join(", ", article.Tags)})"
                    }
                };

                // Sertakan konten artikel sebagai embedded resource reference
                // URI mengikuti skema kb://articles/{id} sesuai resource template
                yield return new PromptMessage
                {
                    Role = Role.User,
                    Content = new EmbeddedResourceBlock
                    {
                        Resource = new TextResourceContents
                        {
                            Uri = $"kb://articles/{article.Id}",
                            MimeType = article.MimeType,
                            Text = article.Content
                        }
                    }
                };
            }
        }

        // Pesan penutup: instruksi ringkasan untuk LLM
        yield return new PromptMessage
        {
            Role = Role.Assistant,
            Content = new TextContentBlock
            {
                Text = "I'll analyze the search results and provide a comprehensive summary " +
                       "of the most relevant articles found."
            }
        };
    }

    /// <summary>
    /// Prompt untuk meringkas artikel tertentu dalam bahasa yang ditentukan.
    /// Mengambil artikel dari store dan membangun prompt yang menginstruksikan
    /// LLM untuk membuat ringkasan dalam bahasa target.
    /// </summary>
    /// <param name="articleId">ID artikel yang akan diringkas</param>
    /// <param name="language">Bahasa target untuk ringkasan (contoh: "Indonesian", "English")</param>
    /// <param name="store">Store knowledge base yang di-inject melalui DI</param>
    /// <returns>Daftar PromptMessage berisi artikel dan instruksi ringkasan</returns>
    [McpServerPrompt(Name = "summarize-article")]
    [Description("Summarizes an article from the knowledge base")]
    public static IEnumerable<PromptMessage> SummarizeArticle(
        [Description("The article ID to summarize")] string articleId,
        [Description("Target language for the summary")] string language,
        KnowledgeBaseStore store)
    {
        // Ambil artikel dari store berdasarkan ID
        var article = store.GetArticle(articleId);

        if (article is null)
        {
            // Artikel tidak ditemukan — berikan pesan error yang informatif
            yield return new PromptMessage
            {
                Role = Role.User,
                Content = new TextContentBlock
                {
                    Text = $"Article with ID \"{articleId}\" was not found in the knowledge base. " +
                           "Please verify the article ID and try again."
                }
            };
            yield break;
        }

        // Instruksi utama untuk LLM — meminta ringkasan dalam bahasa tertentu
        yield return new PromptMessage
        {
            Role = Role.User,
            Content = new TextContentBlock
            {
                Text = $"Please summarize the following article in {language}.\n\n" +
                       $"Title: {article.Title}\n" +
                       $"Author: {article.Author}\n" +
                       $"Category: {article.CategoryId}\n" +
                       $"Created: {article.CreatedAt:yyyy-MM-dd}\n" +
                       $"Tags: {string.Join(", ", article.Tags)}"
            }
        };

        // Sertakan konten artikel sebagai embedded resource
        // Ini memungkinkan client melihat sumber data yang digunakan prompt
        yield return new PromptMessage
        {
            Role = Role.User,
            Content = new EmbeddedResourceBlock
            {
                Resource = new TextResourceContents
                {
                    Uri = $"kb://articles/{article.Id}",
                    MimeType = article.MimeType,
                    Text = article.Content
                }
            }
        };

        // Instruksi tambahan untuk format ringkasan
        yield return new PromptMessage
        {
            Role = Role.User,
            Content = new TextContentBlock
            {
                Text = $"Requirements for the summary:\n" +
                       $"- Language: {language}\n" +
                       "- Include key points and main ideas\n" +
                       "- Keep it concise (3-5 paragraphs)\n" +
                       "- Preserve technical accuracy"
            }
        };

        // Konfirmasi dari assistant bahwa instruksi dipahami
        yield return new PromptMessage
        {
            Role = Role.Assistant,
            Content = new TextContentBlock
            {
                Text = $"I'll provide a concise summary of \"{article.Title}\" in {language}, " +
                       "covering the key points while maintaining technical accuracy."
            }
        };
    }

    /// <summary>
    /// Prompt untuk membandingkan dua artikel dari knowledge base.
    /// Mengambil kedua artikel dan membangun prompt perbandingan yang komprehensif
    /// dengan embedded resource references untuk masing-masing artikel.
    /// </summary>
    /// <param name="articleId1">ID artikel pertama untuk perbandingan</param>
    /// <param name="articleId2">ID artikel kedua untuk perbandingan</param>
    /// <param name="store">Store knowledge base yang di-inject melalui DI</param>
    /// <returns>Daftar PromptMessage berisi kedua artikel dan instruksi perbandingan</returns>
    [McpServerPrompt(Name = "compare-articles")]
    [Description("Compares two articles from the knowledge base")]
    public static IEnumerable<PromptMessage> CompareArticles(
        [Description("First article ID")] string articleId1,
        [Description("Second article ID")] string articleId2,
        KnowledgeBaseStore store)
    {
        // Ambil kedua artikel dari store
        var article1 = store.GetArticle(articleId1);
        var article2 = store.GetArticle(articleId2);

        // Validasi: pastikan kedua artikel ditemukan
        if (article1 is null || article2 is null)
        {
            var missing = article1 is null ? articleId1 : articleId2;
            yield return new PromptMessage
            {
                Role = Role.User,
                Content = new TextContentBlock
                {
                    Text = $"Cannot compare articles: article \"{missing}\" was not found " +
                           "in the knowledge base. Please verify both article IDs and try again."
                }
            };
            yield break;
        }

        // Instruksi perbandingan untuk LLM
        yield return new PromptMessage
        {
            Role = Role.User,
            Content = new TextContentBlock
            {
                Text = "Please compare the following two articles from the knowledge base.\n\n" +
                       "Provide a detailed comparison covering:\n" +
                       "- Topic and scope differences\n" +
                       "- Target audience\n" +
                       "- Depth of coverage\n" +
                       "- Complementary information\n" +
                       "- Recommendations for which to read first"
            }
        };

        // Artikel pertama — metadata sebagai teks
        yield return new PromptMessage
        {
            Role = Role.User,
            Content = new TextContentBlock
            {
                Text = $"--- Article 1 ---\n" +
                       $"Title: {article1.Title}\n" +
                       $"Author: {article1.Author}\n" +
                       $"Category: {article1.CategoryId}\n" +
                       $"Tags: {string.Join(", ", article1.Tags)}\n" +
                       $"Created: {article1.CreatedAt:yyyy-MM-dd}"
            }
        };

        // Konten artikel pertama sebagai embedded resource
        yield return new PromptMessage
        {
            Role = Role.User,
            Content = new EmbeddedResourceBlock
            {
                Resource = new TextResourceContents
                {
                    Uri = $"kb://articles/{article1.Id}",
                    MimeType = article1.MimeType,
                    Text = article1.Content
                }
            }
        };

        // Artikel kedua — metadata sebagai teks
        yield return new PromptMessage
        {
            Role = Role.User,
            Content = new TextContentBlock
            {
                Text = $"--- Article 2 ---\n" +
                       $"Title: {article2.Title}\n" +
                       $"Author: {article2.Author}\n" +
                       $"Category: {article2.CategoryId}\n" +
                       $"Tags: {string.Join(", ", article2.Tags)}\n" +
                       $"Created: {article2.CreatedAt:yyyy-MM-dd}"
            }
        };

        // Konten artikel kedua sebagai embedded resource
        yield return new PromptMessage
        {
            Role = Role.User,
            Content = new EmbeddedResourceBlock
            {
                Resource = new TextResourceContents
                {
                    Uri = $"kb://articles/{article2.Id}",
                    MimeType = article2.MimeType,
                    Text = article2.Content
                }
            }
        };

        // Konfirmasi dari assistant
        yield return new PromptMessage
        {
            Role = Role.Assistant,
            Content = new TextContentBlock
            {
                Text = $"I'll compare \"{article1.Title}\" and \"{article2.Title}\", " +
                       "highlighting their differences, similarities, and how they complement each other."
            }
        };
    }
}

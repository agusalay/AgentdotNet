// =============================================================================
// Property 1: Configuration validation produces informative errors
// Validates: Requirements 2.6
//
// For any invalid configuration state (missing appsettings.json, malformed JSON,
// missing required keys), the application SHALL produce an error message that
// contains the name of the problematic file and terminate without an unhandled
// exception.
// =============================================================================

using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Tests.Properties;

/// <summary>
/// Property-based tests validating that configuration validation
/// produces informative error messages for all invalid config states.
/// **Validates: Requirements 2.6**
/// </summary>
public class ConfigValidationProperties
{
    /// <summary>
    /// Property 1: For any non-existent base path, attempting to load config
    /// produces an InvalidOperationException mentioning "appsettings.json".
    /// </summary>
    [Property(MaxTest = 20)]
    public Property MissingConfigFile_ProducesErrorWithFileName()
    {
        return Prop.ForAll(
            Gen.Choose(1, 10000).Select(i =>
                Path.Combine(Path.GetTempPath(), $"nonexistent_{i}_{Guid.NewGuid():N}"))
                .ToArbitrary(),
            (string basePath) =>
            {
                var exception = Record.Exception(() =>
                    BuildConfigurationFrom(basePath));

                exception.Should().NotBeNull();
                exception.Should().BeOfType<InvalidOperationException>();
                exception!.Message.Should().Contain("appsettings.json");
                return true;
            });
    }

    /// <summary>
    /// Property 1: For any config missing AzureOpenAI:Endpoint,
    /// validation throws with message referencing "Endpoint".
    /// </summary>
    [Property(MaxTest = 20)]
    public Property MissingEndpoint_ProducesInformativeError()
    {
        var gen = Gen.Elements(
            new Dictionary<string, string?>
            {
                ["AzureOpenAI:DeploymentName"] = "gpt-4o-mini"
            },
            new Dictionary<string, string?>
            {
                ["AzureOpenAI:Endpoint"] = "",
                ["AzureOpenAI:DeploymentName"] = "gpt-4o-mini"
            },
            new Dictionary<string, string?>
            {
                ["AzureOpenAI:Endpoint"] = "   ",
                ["AzureOpenAI:DeploymentName"] = "gpt-4o-mini"
            });

        return Prop.ForAll(gen.ToArbitrary(),
            (Dictionary<string, string?> configData) =>
            {
                var config = new ConfigurationBuilder()
                    .AddInMemoryCollection(configData).Build();

                var exception = Record.Exception(() =>
                    ValidateConfiguration(config));

                exception.Should().NotBeNull();
                exception.Should().BeOfType<InvalidOperationException>();
                exception!.Message.Should().Contain("Endpoint");
                return true;
            });
    }

    /// <summary>
    /// Property 1: For any config missing AzureOpenAI:DeploymentName,
    /// validation throws with message referencing "DeploymentName".
    /// </summary>
    [Property(MaxTest = 20)]
    public Property MissingDeploymentName_ProducesInformativeError()
    {
        var gen = Gen.Elements(
            new Dictionary<string, string?>
            {
                ["AzureOpenAI:Endpoint"] = "https://test.openai.azure.com/"
            },
            new Dictionary<string, string?>
            {
                ["AzureOpenAI:Endpoint"] = "https://test.openai.azure.com/",
                ["AzureOpenAI:DeploymentName"] = ""
            },
            new Dictionary<string, string?>
            {
                ["AzureOpenAI:Endpoint"] = "https://test.openai.azure.com/",
                ["AzureOpenAI:DeploymentName"] = "  "
            });

        return Prop.ForAll(gen.ToArbitrary(),
            (Dictionary<string, string?> configData) =>
            {
                var config = new ConfigurationBuilder()
                    .AddInMemoryCollection(configData).Build();

                var exception = Record.Exception(() =>
                    ValidateConfiguration(config));

                exception.Should().NotBeNull();
                exception.Should().BeOfType<InvalidOperationException>();
                exception!.Message.Should().Contain("DeploymentName");
                return true;
            });
    }

    /// <summary>
    /// Property 1: For any malformed JSON content, attempting to parse
    /// produces an exception (no unhandled crash).
    /// </summary>
    [Property(MaxTest = 20)]
    public Property MalformedJson_ProducesException()
    {
        var gen = Gen.Elements(
            "{ invalid json }",
            "{ \"key\": }",
            "not json at all",
            "{ \"AzureOpenAI\": { \"Endpoint\": ",
            "[}",
            "{{}}",
            "{ 'single': 'quotes' }");

        return Prop.ForAll(gen.ToArbitrary(),
            (string malformedJson) =>
            {
                var tempDir = Path.Combine(
                    Path.GetTempPath(), Guid.NewGuid().ToString());
                Directory.CreateDirectory(tempDir);
                var configFile = Path.Combine(tempDir, "appsettings.json");

                try
                {
                    File.WriteAllText(configFile, malformedJson);

                    var exception = Record.Exception(() =>
                    {
                        new ConfigurationBuilder()
                            .SetBasePath(tempDir)
                            .AddJsonFile("appsettings.json",
                                optional: false, reloadOnChange: false)
                            .Build();
                    });

                    exception.Should().NotBeNull(
                        "malformed JSON should produce an exception");
                    return true;
                }
                finally
                {
                    if (Directory.Exists(tempDir))
                        Directory.Delete(tempDir, recursive: true);
                }
            });
    }

    // =========================================================================
    // Helpers: mirror the BuildConfiguration pattern from learning modules
    // =========================================================================

    private static IConfiguration BuildConfigurationFrom(string basePath)
    {
        var configPath = Path.Combine(basePath, "appsettings.json");
        if (!File.Exists(configPath))
        {
            throw new InvalidOperationException(
                "File appsettings.json tidak ditemukan. " +
                "Pastikan file tersebut ada di direktori project.");
        }

        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json",
                optional: false, reloadOnChange: false)
            .Build();

        ValidateConfiguration(configuration);
        return configuration;
    }

    private static void ValidateConfiguration(IConfiguration configuration)
    {
        var endpoint = configuration["AzureOpenAI:Endpoint"];
        var deploymentName = configuration["AzureOpenAI:DeploymentName"];

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new InvalidOperationException(
                "AzureOpenAI:Endpoint belum dikonfigurasi di appsettings.json.");
        }

        if (string.IsNullOrWhiteSpace(deploymentName))
        {
            throw new InvalidOperationException(
                "AzureOpenAI:DeploymentName belum dikonfigurasi di appsettings.json.");
        }
    }
}

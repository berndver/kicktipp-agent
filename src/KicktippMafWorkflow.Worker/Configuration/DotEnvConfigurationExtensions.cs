using Microsoft.Extensions.Configuration;

namespace KicktippMafWorkflow.Worker.Configuration;

public static class DotEnvConfigurationExtensions
{
    public static IConfigurationBuilder AddDotEnv(this IConfigurationBuilder builder, string path)
    {
        if (!File.Exists(path))
            return builder;

        var values = new Dictionary<string, string?>();
        foreach (var line in File.ReadAllLines(path))
        {
            var trimmed = line.AsSpan().Trim();
            if (trimmed.IsEmpty || trimmed[0] == '#')
                continue;

            var eqIdx = trimmed.IndexOf('=');
            if (eqIdx < 0)
                continue;

            var key = trimmed[..eqIdx].Trim().ToString();
            var value = trimmed[(eqIdx + 1)..].Trim().ToString();

            // Convert __ to : for IConfiguration hierarchy
            key = key.Replace("__", ":");

            values[key] = value;
        }

        return builder.AddInMemoryCollection(values!);
    }
}

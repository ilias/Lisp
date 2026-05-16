using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Runtime.Loader;

namespace Lisp;

internal static class NuGetPackageLoader
{
    private static readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(30),
    };

    private static readonly object _sync = new();
    private static readonly HashSet<string> _loadedPackages = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> _assemblyPathBySimpleName = new(StringComparer.OrdinalIgnoreCase);

    static NuGetPackageLoader()
    {
        AssemblyLoadContext.Default.Resolving += ResolveFromRegisteredPaths;
    }

    public static IReadOnlyList<Assembly> LoadPackage(string spec, string? preferredFramework = null)
    {
        var (packageId, version) = ParseSpec(spec);
        var packageKey = $"{packageId}@{version}";

        lock (_sync)
        {
            if (!_loadedPackages.Contains(packageKey))
            {
                EnsurePackageAvailable(packageId, version);
                _loadedPackages.Add(packageKey);
            }
        }

        var packageDirectory = GetInstalledPackageDirectory(packageId, version)
            ?? throw new LispException($"load-package: package '{packageId}@{version}' is not available in local NuGet cache");

        var assemblyCandidates = CollectAssemblyCandidates(packageDirectory, preferredFramework);
        if (assemblyCandidates.Count == 0)
            throw new LispException($"load-package: no loadable assemblies found under '{packageDirectory}'");

        RegisterAssemblyPaths(assemblyCandidates);

        List<Assembly> loaded = [];
        foreach (var path in assemblyCandidates)
        {
            var asm = TryGetLoadedAssemblyByPath(path);
            if (asm != null)
            {
                loaded.Add(asm);
                continue;
            }

            try
            {
                loaded.Add(AssemblyLoadContext.Default.LoadFromAssemblyPath(path));
            }
            catch (Exception ex)
            {
                throw new LispException($"load-package: failed loading assembly '{Path.GetFileName(path)}': {ex.Message}", ex);
            }
        }

        return loaded;
    }

    private static (string PackageId, string Version) ParseSpec(string spec)
    {
        if (string.IsNullOrWhiteSpace(spec))
            throw new LispException("load-package: expected 'PackageId@Version'");

        var at = spec.LastIndexOf('@');
        if (at <= 0 || at >= spec.Length - 1)
            throw new LispException($"load-package: invalid spec '{spec}'. Expected 'PackageId@Version'");

        var packageId = spec[..at].Trim();
        var version = spec[(at + 1)..].Trim();
        if (packageId.Length == 0 || version.Length == 0)
            throw new LispException($"load-package: invalid spec '{spec}'. Expected 'PackageId@Version'");

        return (packageId, version);
    }

    private static void EnsurePackageAvailable(string packageId, string version)
    {
        if (GetInstalledPackageDirectory(packageId, version) != null)
            return;

        DownloadPackageFromNuGet(packageId, version);

        if (GetInstalledPackageDirectory(packageId, version) == null)
            throw new LispException($"load-package: package '{packageId}@{version}' could not be installed in local cache");
    }

    private static string? GetInstalledPackageDirectory(string packageId, string version)
    {
        var root = GetNuGetPackagesRoot();
        var idLower = packageId.ToLowerInvariant();
        var versionLower = version.ToLowerInvariant();

        var candidates = new[]
        {
            Path.Combine(root, idLower, versionLower),
            Path.Combine(root, idLower, version),
            Path.Combine(root, packageId, versionLower),
            Path.Combine(root, packageId, version),
        };

        foreach (var candidate in candidates)
        {
            if (Directory.Exists(candidate))
                return candidate;
        }

        return null;
    }

    private static string GetNuGetPackagesRoot()
    {
        var fromEnv = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
        if (!string.IsNullOrWhiteSpace(fromEnv))
            return fromEnv;

        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(profile))
            throw new LispException("load-package: unable to determine NuGet package cache location");

        return Path.Combine(profile, ".nuget", "packages");
    }

    private static void DownloadPackageFromNuGet(string packageId, string version)
    {
        var idLower = packageId.ToLowerInvariant();
        var versionLower = version.ToLowerInvariant();
        var url = $"https://api.nuget.org/v3-flatcontainer/{idLower}/{versionLower}/{idLower}.{versionLower}.nupkg";

        HttpResponseMessage response;
        try
        {
            response = _http.GetAsync(url).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            throw new LispException($"load-package: failed to download '{packageId}@{version}': {ex.Message}", ex);
        }

        if (!response.IsSuccessStatusCode)
            throw new LispException($"load-package: NuGet download failed for '{packageId}@{version}' ({(int)response.StatusCode} {response.ReasonPhrase})");

        byte[] bytes;
        try
        {
            bytes = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            throw new LispException($"load-package: failed reading NuGet response: {ex.Message}", ex);
        }

        var installDir = Path.Combine(GetNuGetPackagesRoot(), packageId.ToLowerInvariant(), version.ToLowerInvariant());
        Directory.CreateDirectory(installDir);

        try
        {
            using var ms = new MemoryStream(bytes);
            using var archive = new ZipArchive(ms, ZipArchiveMode.Read);
            ExtractArchiveSafely(archive, installDir);
        }
        catch (Exception ex)
        {
            throw new LispException($"load-package: failed extracting package '{packageId}@{version}': {ex.Message}", ex);
        }
    }

    private static void ExtractArchiveSafely(ZipArchive archive, string destination)
    {
        var destinationFull = Path.GetFullPath(destination);

        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.FullName))
                continue;

            var targetPath = Path.GetFullPath(Path.Combine(destinationFull, entry.FullName));
            if (!targetPath.StartsWith(destinationFull, StringComparison.OrdinalIgnoreCase))
                throw new LispException($"load-package: invalid package entry path '{entry.FullName}'");

            if (entry.FullName.EndsWith('/'))
            {
                Directory.CreateDirectory(targetPath);
                continue;
            }

            var dir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            entry.ExtractToFile(targetPath, overwrite: true);
        }
    }

    private static List<string> CollectAssemblyCandidates(string packageDirectory, string? preferredFramework)
    {
        List<string> candidates = [];

        var libRoot = Path.Combine(packageDirectory, "lib");
        if (Directory.Exists(libRoot))
        {
            var tfmDir = SelectBestFrameworkDirectory(libRoot, preferredFramework);
            if (tfmDir != null)
                candidates.AddRange(GetDllsInDirectory(tfmDir));
        }

        var runtimesRoot = Path.Combine(packageDirectory, "runtimes");
        if (Directory.Exists(runtimesRoot))
        {
            var runtimeDlls = CollectRuntimeDlls(runtimesRoot, preferredFramework);
            candidates.AddRange(runtimeDlls);
        }

        return [.. candidates
            .Where(path => Path.GetFileName(path) is not null)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)];
    }

    private static IEnumerable<string> CollectRuntimeDlls(string runtimesRoot, string? preferredFramework)
    {
        var rid = RuntimeInformation.RuntimeIdentifier;
        List<string> results = [];

        if (!string.IsNullOrWhiteSpace(rid))
        {
            var ridLibRoot = Path.Combine(runtimesRoot, rid, "lib");
            if (Directory.Exists(ridLibRoot))
            {
                var tfmDir = SelectBestFrameworkDirectory(ridLibRoot, preferredFramework);
                if (tfmDir != null)
                    results.AddRange(GetDllsInDirectory(tfmDir));
            }
        }

        foreach (var libRoot in Directory.GetDirectories(runtimesRoot, "lib", SearchOption.AllDirectories))
        {
            var tfmDir = SelectBestFrameworkDirectory(libRoot, preferredFramework);
            if (tfmDir != null)
                results.AddRange(GetDllsInDirectory(tfmDir));
        }

        return results;
    }

    private static string? SelectBestFrameworkDirectory(string frameworkRoot, string? preferredFramework)
    {
        var dirs = Directory.GetDirectories(frameworkRoot)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .ToArray();

        if (dirs.Length == 0)
            return null;

        string? selected = null;

        if (!string.IsNullOrWhiteSpace(preferredFramework))
        {
            selected = dirs.FirstOrDefault(d => d.Equals(preferredFramework, StringComparison.OrdinalIgnoreCase));
        }

        selected ??= SelectBestNetFramework(dirs);
        selected ??= SelectBestNetStandardFramework(dirs);
        selected ??= dirs.OrderByDescending(d => d, StringComparer.OrdinalIgnoreCase).FirstOrDefault();

        return selected == null ? null : Path.Combine(frameworkRoot, selected);
    }

    private static string? SelectBestNetFramework(IEnumerable<string> frameworks)
    {
        var current = new Version(Environment.Version.Major, Environment.Version.Minor);
        var parsed = new List<(string Name, Version Version)>();

        foreach (var name in frameworks)
        {
            if (!name.StartsWith("net", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("netstandard", StringComparison.OrdinalIgnoreCase))
                continue;

            var verText = name[3..];
            if (TryParseTfmVersion(verText, out var version))
                parsed.Add((name, version));
        }

        if (parsed.Count == 0)
            return null;

        var notGreaterThanCurrent = parsed
            .Where(p => p.Version <= current)
            .OrderByDescending(p => p.Version)
            .FirstOrDefault();

        if (!string.IsNullOrEmpty(notGreaterThanCurrent.Name))
            return notGreaterThanCurrent.Name;

        return parsed.OrderBy(p => p.Version).First().Name;
    }

    private static string? SelectBestNetStandardFramework(IEnumerable<string> frameworks)
    {
        var parsed = new List<(string Name, Version Version)>();

        foreach (var name in frameworks)
        {
            if (!name.StartsWith("netstandard", StringComparison.OrdinalIgnoreCase))
                continue;

            var verText = name[11..];
            if (TryParseTfmVersion(verText, out var version))
                parsed.Add((name, version));
        }

        return parsed.Count == 0
            ? null
            : parsed.OrderByDescending(p => p.Version).First().Name;
    }

    private static bool TryParseTfmVersion(string text, out Version version)
    {
        text = text.Trim();
        if (text.Length == 0)
        {
            version = default!;
            return false;
        }

        if (text.Contains('.'))
            return Version.TryParse(text, out version!);

        if (text.Length == 1 && char.IsDigit(text[0]))
            return Version.TryParse($"{text}.0", out version!);

        if (text.Length == 2 && char.IsDigit(text[0]) && char.IsDigit(text[1]))
            return Version.TryParse($"{text[0]}.{text[1]}", out version!);

        if (text.Length == 3 && char.IsDigit(text[0]) && char.IsDigit(text[1]) && char.IsDigit(text[2]))
            return Version.TryParse($"{text[0]}{text[1]}.{text[2]}", out version!);

        version = default!;
        return false;
    }

    private static IReadOnlyList<string> GetDllsInDirectory(string directory)
        => [.. Directory.GetFiles(directory, "*.dll", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFullPath)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)];

    private static void RegisterAssemblyPaths(IEnumerable<string> assemblyPaths)
    {
        lock (_sync)
        {
            foreach (var path in assemblyPaths)
            {
                var simpleName = Path.GetFileNameWithoutExtension(path);
                if (string.IsNullOrWhiteSpace(simpleName))
                    continue;

                _assemblyPathBySimpleName[simpleName] = path;
            }
        }
    }

    private static Assembly? ResolveFromRegisteredPaths(AssemblyLoadContext context, AssemblyName name)
    {
        string? path;
        lock (_sync)
        {
            _assemblyPathBySimpleName.TryGetValue(name.Name ?? string.Empty, out path);
        }

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;

        return TryGetLoadedAssemblyByPath(path) ?? context.LoadFromAssemblyPath(path);
    }

    private static Assembly? TryGetLoadedAssemblyByPath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        return AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a =>
        {
            if (string.IsNullOrWhiteSpace(a.Location))
                return false;

            return string.Equals(Path.GetFullPath(a.Location), fullPath, StringComparison.OrdinalIgnoreCase);
        });
    }
}

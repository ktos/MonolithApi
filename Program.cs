// Monolith API wrapper
// Copyright (C) 2026 Marcin "Ktos" Badurowicz <ktos@.ktos.info>

// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.

// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapPost(
    "/archive",
    async ([FromBody] MonolithRequest request, ILogger<Program> logger, IConfiguration config) =>
    {
        logger.LogInformation("Archive request received");

        if (string.IsNullOrWhiteSpace(request.Url) && string.IsNullOrWhiteSpace(request.StdinHtml))
        {
            logger.LogWarning("Invalid request: Neither 'url' nor 'stdinHtml' provided");
            return Results.BadRequest("Either 'url' or 'stdinHtml' must be provided.");
        }

        if (!string.IsNullOrWhiteSpace(request.Url))
            logger.LogInformation("Processing URL: {Url}", request.Url);
        else
            logger.LogInformation("Processing HTML from stdin");

        var args = new List<string>();

        var o = request.Options;

        // Boolean flags
        if (o.ExcludeAudio)
            args.Add("-a");
        if (o.ExcludeCss)
            args.Add("-c");
        if (o.ExcludeImages)
            args.Add("-i");
        if (o.ExcludeJs)
            args.Add("-j");
        if (o.ExcludeFonts)
            args.Add("-F");
        if (o.ExcludeVideos)
            args.Add("-v");
        if (o.OmitFrames)
            args.Add("-f");
        if (o.Isolate)
            args.Add("-I");
        if (o.ExtractNoScript)
            args.Add("-n");
        if (o.Mhtml)
            args.Add("-m");
        if (o.NoMetadata)
            args.Add("-M");
        if (o.IgnoreNetworkErrors)
            args.Add("-e");
        if (o.AcceptInvalidCerts)
            args.Add("-k");
        if (o.Quiet)
            args.Add("-q");

        // Key/value options
        if (!string.IsNullOrWhiteSpace(o.BaseUrl))
        {
            args.Add("-b");
            args.Add(o.BaseUrl);
        }

        if (!string.IsNullOrWhiteSpace(o.UserAgent))
        {
            args.Add("-u");
            args.Add(o.UserAgent);
        }

        if (o.TimeoutSeconds.HasValue)
        {
            args.Add("-t");
            args.Add(o.TimeoutSeconds.Value.ToString());
        }

        if (!string.IsNullOrWhiteSpace(o.CookiesFile))
        {
            args.Add("-C");
            args.Add(o.CookiesFile);
        }

        if (!string.IsNullOrWhiteSpace(o.Encoding))
        {
            args.Add("-E");
            args.Add(o.Encoding);
        }

        foreach (var domain in o.AllowDomains)
        {
            args.Add("-d");
            args.Add(domain);
        }

        foreach (var domain in o.BlockDomains)
        {
            args.Add("-B");
            args.Add(domain);
        }

        // Output to STDOUT
        args.Add("-o");
        args.Add("-");

        // URL or STDIN mode
        if (!string.IsNullOrWhiteSpace(request.Url))
        {
            args.Add(request.Url);
        }
        else
        {
            args.Add("-");
        }

        // Determine monolith executable path
        var useBundledMonolith = config.GetValue<bool>("UseBundledMonolith", false);
        logger.LogDebug("Use bundled Monolith: {UseBundledMonolith}", useBundledMonolith);

        var monolithPath = useBundledMonolith ? Path.Combine(".", "monolith") : "monolith";

        var psi = new ProcessStartInfo
        {
            FileName = monolithPath,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        // Log the full command at verbose level
        var commandLine =
            $"monolith {string.Join(" ", args.Select(a => a.Contains(" ") ? $"\"{a}\"" : a))}";
        logger.LogDebug("Executing command: {CommandLine}", commandLine);

        using var process = Process.Start(psi)!;

        if (!string.IsNullOrWhiteSpace(request.StdinHtml))
        {
            logger.LogDebug(
                "Writing HTML to stdin (length: {HtmlLength} characters)",
                request.StdinHtml.Length
            );
            await process.StandardInput.WriteAsync(request.StdinHtml);
            process.StandardInput.Close();
        }

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        logger.LogDebug("Waiting for process to complete");
        await process.WaitForExitAsync();

        var output = await outputTask;
        var error = await errorTask;

        logger.LogInformation("Process completed with exit code: {ExitCode}", process.ExitCode);

        if (process.ExitCode != 0)
        {
            logger.LogError("Monolith failed with error: {Error}", error);
            return Results.Problem($"Monolith failed: {error}");
        }

        logger.LogDebug(
            "Successfully generated output (length: {OutputLength} characters)",
            output.Length
        );

        var contentType = o.Mhtml ? "multipart/related" : "text/html; charset=utf-8";

        return Results.Content(output, contentType);
    }
);

app.Run();

/// <summary>
/// Request model for archiving web content using Monolith.
/// </summary>
public class MonolithRequest
{
    /// <summary>
    /// URL of the web page to archive. Either this or StdinHtml must be provided.
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// HTML content to archive via stdin. Either this or Url must be provided.
    /// </summary>
    public string? StdinHtml { get; set; } = null;

    /// <summary>
    /// Options for controlling the archiving process.
    /// </summary>
    public MonolithOptions Options { get; set; } = new();
}

/// <summary>
/// Configuration options for the Monolith archiving process.
/// </summary>
public class MonolithOptions
{
    /// <summary>
    /// Exclude audio sources from the archive.
    /// </summary>
    public bool ExcludeAudio { get; set; } = true;

    /// <summary>
    /// Exclude CSS stylesheets from the archive.
    /// </summary>
    public bool ExcludeCss { get; set; } = false;

    /// <summary>
    /// Exclude images from the archive.
    /// </summary>
    public bool ExcludeImages { get; set; } = true;

    /// <summary>
    /// Exclude JavaScript from the archive.
    /// </summary>
    public bool ExcludeJs { get; set; } = true;

    /// <summary>
    /// Exclude fonts from the archive.
    /// </summary>
    public bool ExcludeFonts { get; set; } = true;

    /// <summary>
    /// Exclude video sources from the archive.
    /// </summary>
    public bool ExcludeVideos { get; set; } = true;

    /// <summary>
    /// Omit HTML frames from the archive.
    /// </summary>
    public bool OmitFrames { get; set; } = true;

    /// <summary>
    /// Isolate the document from external content.
    /// </summary>
    public bool Isolate { get; set; }

    /// <summary>
    /// Extract contents of NOSCRIPT elements
    /// </summary>
    public bool ExtractNoScript { get; set; }

    /// <summary>
    /// Output as MHTML format instead of HTML.
    /// </summary>
    public bool Mhtml { get; set; }

    /// <summary>
    /// Exclude metadata from the archive.
    /// </summary>
    public bool NoMetadata { get; set; }

    /// <summary>
    /// Ignore network errors during archiving.
    /// </summary>
    public bool IgnoreNetworkErrors { get; set; }

    /// <summary>
    /// Accept invalid SSL certificates.
    /// </summary>
    public bool AcceptInvalidCerts { get; set; }

    /// <summary>
    /// Suppress output messages.
    /// </summary>
    public bool Quiet { get; set; }

    /// <summary>
    /// Timeout in seconds for the archiving operation.
    /// </summary>
    public int? TimeoutSeconds { get; set; }

    /// <summary>
    /// User agent string to use for requests.
    /// </summary>
    public string? UserAgent { get; set; } = "Monolith-API/1.0";

    /// <summary>
    /// Base URL to use for relative links.
    /// </summary>
    public string? BaseUrl { get; set; } = null;

    /// <summary>
    /// Path to a cookies file for authentication.
    /// </summary>
    public string? CookiesFile { get; set; } = null;

    /// <summary>
    /// Character encoding to use.
    /// </summary>
    public string? Encoding { get; set; }

    /// <summary>
    /// Allow retrieving assets only from specified domain(s)
    /// </summary>
    public List<string> AllowDomains { get; set; } = [];

    /// <summary>
    /// Forbid retrieving assets from specified domain(s)
    /// </summary>
    public List<string> BlockDomains { get; set; } = [];
}

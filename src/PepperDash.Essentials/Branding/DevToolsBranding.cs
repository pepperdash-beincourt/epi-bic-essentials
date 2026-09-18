using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using PepperDash.Core;
using Serilog.Events;

namespace PepperDash.Essentials.Branding;

/// <summary>
/// Applies this organisation's branding to the Essentials dev tools app after it is extracted to
/// html/debug. The app's sign-in heading is baked into its own bundle, so the branding ships as a
/// small script plus a logo copied from the program's logo folder, referenced from the app's
/// index.html. The app's own files are left alone, so a newer dev tools release keeps working.
/// </summary>
public static class DevToolsBranding
{
    private const string ScriptFileName = "brand.js";
    private const string ScriptResourceName = "branding.js";
    private const string LogoFileName = "brand-logo";
    private const string LogoFolderName = "logo";
    private const string BrandedDocumentTitle = "<title>Beincourt Essentials Dev Tools</title>";
    private const string ScriptTagMarker = "id=\"brand-script\"";

    private static readonly string[] LogoExtensions = { ".svg", ".png", ".jpg", ".jpeg", ".gif" };

    /// <summary>
    /// Writes the branding script into the extracted dev tools app, copies in the program's logo, and
    /// references both from the app's index.html. Never throws: branding must not stop the program
    /// from starting.
    /// </summary>
    /// <param name="debugDirectory">Directory the dev tools app was extracted to.</param>
    /// <param name="programDirectory">Program directory holding the logo folder (user/programN).</param>
    public static void Apply(string debugDirectory, string programDirectory)
    {
        try
        {
            if (string.IsNullOrEmpty(debugDirectory) || !Directory.Exists(debugDirectory))
            {
                return;
            }

            var indexPath = Path.Combine(debugDirectory, "index.html");

            if (!File.Exists(indexPath))
            {
                Debug.LogMessage(LogEventLevel.Warning,
                    "Dev tools index.html not found at {indexPath:l}, skipping branding", indexPath);
                return;
            }

            WriteScript(Path.Combine(debugDirectory, ScriptFileName));

            var logoFileName = CopyLogo(debugDirectory, programDirectory);

            var scriptTag = logoFileName == null
                ? $"<script {ScriptTagMarker} src=\"/cws/debug/{ScriptFileName}\"></script>"
                : $"<script {ScriptTagMarker} src=\"/cws/debug/{ScriptFileName}\" data-logo=\"/cws/debug/{logoFileName}\"></script>";

            var html = File.ReadAllText(indexPath);

            // Replace an earlier branding tag rather than stacking a second one, so re-extracting a
            // dev tools release (or a changed logo) stays idempotent.
            html = Regex.Replace(html, $"[ \t]*<script {Regex.Escape(ScriptTagMarker)}.*?</script>\r?\n?", string.Empty);
            html = Regex.Replace(html, "<title>.*?</title>", BrandedDocumentTitle);

            var bodyClose = html.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
            html = bodyClose < 0
                ? html + scriptTag
                : html.Insert(bodyClose, "    " + scriptTag + Environment.NewLine + "  ");

            File.WriteAllText(indexPath, html);

            Debug.LogMessage(LogEventLevel.Information,
                "Branded the dev tools app at {debugDirectory:l} (logo: {logo:l})",
                debugDirectory, logoFileName ?? "none found");
        }
        catch (Exception ex)
        {
            Debug.LogMessage(ex, "Unable to brand the dev tools app", null);
        }
    }

    /// <summary>
    /// Copies the first image in the program's logo folder next to the dev tools app, and returns the
    /// file name it was written as, or null when no logo is deployed.
    /// </summary>
    private static string CopyLogo(string debugDirectory, string programDirectory)
    {
        if (string.IsNullOrEmpty(programDirectory))
        {
            return null;
        }

        var logoDirectory = Path.Combine(
            programDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            LogoFolderName);

        if (!Directory.Exists(logoDirectory))
        {
            Debug.LogMessage(LogEventLevel.Debug,
                "No logo folder at {logoDirectory:l}, branding the dev tools app without a logo", logoDirectory);
            return null;
        }

        var logoFile = new DirectoryInfo(logoDirectory)
            .GetFiles()
            .Where(file => LogoExtensions.Contains(file.Extension.ToLowerInvariant()))
            .OrderBy(file => file.Name)
            .FirstOrDefault();

        if (logoFile == null)
        {
            return null;
        }

        var destinationFileName = LogoFileName + logoFile.Extension.ToLowerInvariant();
        logoFile.CopyTo(Path.Combine(debugDirectory, destinationFileName), overwrite: true);

        return destinationFileName;
    }

    private static void WriteScript(string destinationPath)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly
            .GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith("." + ScriptResourceName, StringComparison.OrdinalIgnoreCase));

        if (resourceName == null)
        {
            Debug.LogMessage(LogEventLevel.Warning,
                "Branding resource {resourceFileName:l} not found in the assembly", ScriptResourceName);
            return;
        }

        using var resource = assembly.GetManifestResourceStream(resourceName);

        if (resource == null)
        {
            return;
        }

        using var file = File.Create(destinationPath);
        resource.CopyTo(file);
    }
}

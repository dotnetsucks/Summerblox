using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

namespace SummerbloxLauncher;

internal static class Program
{
    private const string Version = "1.2.6";

    private const bool DebugMode = false;

    private const string BaseApiUrl = "https://www.sumrbx.xyz";

    private const string ProtocolName = "summerblox";

    private static readonly HashSet<string> AllowedYears = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "2010", "2011", "2012", "2013" };

    private static readonly string ProgramFilesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Summerblox");

    private static readonly string ClientsParentPath = Path.Combine(ProgramFilesPath, "Clients");

    private static readonly string TempDownloadPath = Path.Combine(ProgramFilesPath, "Temp");

    private static Form1 MainForm;

    private static BackgroundWorker InstallWorker;

    private static string PendingUriArgs;

    private static bool UpdateDetectedDuringUriCheck;

    private static string TargetClientYear = "2011";

    [STAThread]
    private static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(defaultValue: false);
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        try
        {
            InstallWorker = new BackgroundWorker
            {
                WorkerReportsProgress = true,
                WorkerSupportsCancellation = false
            };
            InstallWorker.DoWork += InstallWorker_DoWork;
            InstallWorker.ProgressChanged += InstallWorker_ProgressChanged;
            InstallWorker.RunWorkerCompleted += InstallWorker_RunWorkerCompleted;
            string text = null;
            foreach (string text2 in args)
            {
                string text3 = ((text2 == null) ? string.Empty : text2.Trim('"', ' '));
                if (text3.StartsWith("summerblox://", StringComparison.OrdinalIgnoreCase))
                {
                    text = text3;
                    break;
                }
            }
            if (!string.IsNullOrEmpty(text))
            {
                HandleUriBehaviour(text);
            }
            else
            {
                InitializeFormAndInstall();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Critical Error", MessageBoxButtons.OK, MessageBoxIcon.Hand);
            Application.Exit();
        }
    }

    private static string GenerateRandomAlphaNumeric(int length)
    {
        byte[] array = new byte[length];
        char[] array2 = new char[length];
        using (RandomNumberGenerator randomNumberGenerator = RandomNumberGenerator.Create())
        {
            randomNumberGenerator.GetBytes(array);
        }
        for (int i = 0; i < length; i++)
        {
            array2[i] = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789"[array[i] % "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789".Length];
        }
        return new string(array2);
    }

    private static void InitializeFormAndInstall()
    {
        MainForm = new Form1();
        MainForm.Shown += MainForm_Shown;
        Application.Run(MainForm);
    }

    private static void MainForm_Shown(object sender, EventArgs e)
    {
        if (!InstallWorker.IsBusy)
        {
            InstallWorker.RunWorkerAsync();
        }
    }

    private static void InstallWorker_DoWork(object sender, DoWorkEventArgs e)
    {
        string executablePath = Application.ExecutablePath;
        string text = Path.Combine(ProgramFilesPath, "1.2.6.exe");
        EnsureDirectory(ProgramFilesPath);
        EnsureDirectory(ClientsParentPath);
        EnsureDirectory(TempDownloadPath);
        if (!string.IsNullOrEmpty(PendingUriArgs))
        {
            Uri uri = new Uri(PendingUriArgs.Trim('"', ' '));
            string text2 = ExtractQueryParam(uri.Query, "year");
            if (!string.IsNullOrEmpty(text2))
            {
                ValidateYear(text2);
                TargetClientYear = text2;
            }
        }
        InstallWorker.ReportProgress(5, "Verifying launcher configuration...");
        if (!PathsEqual(executablePath, text))
        {
            CopyFileAtomically(executablePath, text);
        }
        InstallWorker.ReportProgress(15, "Checking for updates...");
        string rawResponse = DownloadString("https://www.sumrbx.xyz/_LAUNCHER/CheckVersion?version=" + Uri.EscapeDataString("1.2.6"));
        rawResponse = ProcessAndVerifySignedResponse(rawResponse);
        if (IsJsonValue(rawResponse, "status", "UpdateRequired") || UpdateDetectedDuringUriCheck)
        {
            InstallWorker.ReportProgress(30, "Downloading launcher update...");
            string text3 = ExtractJsonValue(rawResponse, "newVersion");
            if (string.IsNullOrEmpty(text3))
            {
                throw new InvalidOperationException("The update response did not contain a newVersion value.");
            }
            ValidateVersion(text3);
            string url = "https://www.sumrbx.xyz/_LAUNCHER/Launchers/" + Uri.EscapeDataString(text3) + ".exe";
            string text4 = Path.Combine(TempDownloadPath, text3 + "_" + Guid.NewGuid().ToString("N") + ".exe");
            string text5 = Path.Combine(ProgramFilesPath, text3 + ".exe");
            DownloadFile(url, text4);
            VerifyFileIsUsableExecutable(text4);
            CopyFileAtomically(text4, text5);
            DeleteFileQuietly(text4);
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = text5,
                Arguments = (string.IsNullOrEmpty(PendingUriArgs) ? string.Empty : ("\"" + PendingUriArgs.Replace("\"", "\\\"") + "\"")),
                UseShellExecute = false,
                WorkingDirectory = ProgramFilesPath
            };
            Process.Start(startInfo);
            Environment.Exit(0);
            return;
        }
        string[] array = new string[4] { "2010", "2011", "2012", "2013" };
        for (int i = 0; i < array.Length; i++)
        {
            string text6 = array[i];
            InstallWorker.ReportProgress(40 + (int)((float)i / (float)array.Length * 35f), "Downloading " + text6 + " client archive...");
            DownloadAndExtractClientZip(text6);
        }
        InstallWorker.ReportProgress(80, "Creating desktop shortcuts...");
        CreateDesktopShortcuts(text);
        InstallWorker.ReportProgress(90, "Performing integrity verification...");
        PerformIntegrityCheck();
        string path = Path.Combine(ClientsParentPath, TargetClientYear);
        string path2 = Path.Combine(path, "Summerblox.exe");
        if (!File.Exists(path2))
        {
            throw new InvalidOperationException("Installation failed: client executable is missing.");
        }
        InstallWorker.ReportProgress(95, "Registering URI protocol...");
        RegisterUriScheme(text);
        InstallWorker.ReportProgress(100, "Complete!");
        e.Result = "Success";
    }

    private static void InstallWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
    {
        if (MainForm != null && !MainForm.IsDisposed)
        {
            string text = e.UserState as string;
            if (!string.IsNullOrEmpty(text) && MainForm.Controls["statusLabel"] is Label label)
            {
                label.Text = text;
            }
            if (MainForm.Controls["installProgressBar"] is ProgressBar progressBar)
            {
                int value = Math.Max(0, Math.Min(100, e.ProgressPercentage));
                progressBar.Value = value;
            }
        }
    }

    private static void InstallWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
    {
        if (MainForm == null || MainForm.IsDisposed)
        {
            return;
        }
        if (e.Error != null)
        {
            MessageBox.Show(e.Error.Message, "Installation Error", MessageBoxButtons.OK, MessageBoxIcon.Hand);
            MainForm.Close();
            return;
        }
        if (e.Cancelled)
        {
            MainForm.Close();
            return;
        }
        if (MainForm.Controls["installProgressBar"] is ProgressBar progressBar)
        {
            progressBar.Value = 100;
        }
        if (MainForm.Controls["statusLabel"] is Label label)
        {
            label.Text = "Installation complete.";
        }
    }

    private static void DownloadAndExtractClientZip(string versionYear)
    {
        ValidateYear(versionYear);
        string url = "https://www.sumrbx.xyz/_CLIENTS/" + Uri.EscapeDataString(versionYear) + ".zip";
        string text = Path.Combine(TempDownloadPath, versionYear + "_" + Guid.NewGuid().ToString("N") + ".zip");
        string text2 = Path.Combine(TempDownloadPath, versionYear + "_staging_" + Guid.NewGuid().ToString("N"));
        string text3 = Path.Combine(ClientsParentPath, versionYear);
        try
        {
            DownloadFile(url, text);
            if (!File.Exists(text))
            {
                throw new InvalidOperationException("The client archive was not downloaded.");
            }
            if (new FileInfo(text).Length == 0)
            {
                throw new InvalidOperationException("The client archive is empty.");
            }
            if (Directory.Exists(text2))
            {
                Directory.Delete(text2, recursive: true);
            }
            Directory.CreateDirectory(text2);
            ExtractZipSafely(text, text2);
            string[] files = Directory.GetFiles(text2, "Summerblox.exe", SearchOption.AllDirectories);
            if (files.Length == 0)
            {
                throw new InvalidOperationException("The archive for " + versionYear + " does not contain Summerblox.exe.");
            }
            string directoryName = Path.GetDirectoryName(files[0]);
            if (string.IsNullOrEmpty(directoryName))
            {
                throw new InvalidOperationException("The archive for " + versionYear + " has an invalid executable path.");
            }
            string text4 = text3 + ".new." + Guid.NewGuid().ToString("N");
            if (Directory.Exists(text4))
            {
                Directory.Delete(text4, recursive: true);
            }
            Directory.CreateDirectory(text4);
            CopyDirectoryContents(directoryName, text4);
            string path = Path.Combine(text4, "Summerblox.exe");
            if (!File.Exists(path))
            {
                throw new InvalidOperationException("The archive for " + versionYear + " could not be prepared.");
            }
            VerifyFileIsUsableExecutable(path);
            ReplaceDirectoryAtomically(text4, text3);
        }
        finally
        {
            DeleteDirectoryQuietly(text2);
            DeleteFileQuietly(text);
        }
    }

    private static void ExtractZipSafely(string zipPath, string destination)
    {
        string value = EnsureTrailingSeparator(Path.GetFullPath(destination));
        using FileStream stream = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using ZipArchive zipArchive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
        foreach (ZipArchiveEntry entry in zipArchive.Entries)
        {
            string text = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
            if (string.IsNullOrEmpty(text))
            {
                continue;
            }
            string fullPath = Path.GetFullPath(Path.Combine(destination, text));
            if (!fullPath.StartsWith(value, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Unsafe ZIP entry detected: " + entry.FullName);
            }
            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(fullPath);
                continue;
            }
            string directoryName = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directoryName))
            {
                Directory.CreateDirectory(directoryName);
            }
            using Stream stream2 = entry.Open();
            using FileStream destination2 = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            stream2.CopyTo(destination2);
        }
    }

    private static string ProcessAndVerifySignedResponse(string rawResponse)
    {
        if (string.IsNullOrEmpty(rawResponse))
        {
            throw new InvalidDataException("The server returned an empty response.");
        }
        if (rawResponse[0] != '%')
        {
            return rawResponse;
        }
        int num = rawResponse.IndexOf('%', 1);
        if (num < 0)
        {
            throw new InvalidDataException("The server response contains an invalid signature block.");
        }
        return rawResponse.Substring(num + 1);
    }

    private static void PerformIntegrityCheck()
    {
        string executablePath = Application.ExecutablePath;
        if (!File.Exists(executablePath))
        {
            throw new FileNotFoundException("The launcher executable could not be found.", executablePath);
        }
        VerifyFileIsUsableExecutable(executablePath);
        string path = Path.Combine(ClientsParentPath, TargetClientYear);
        string text = Path.Combine(path, "Summerblox.exe");
        if (!File.Exists(text))
        {
            throw new FileNotFoundException("The installed client executable could not be found.", text);
        }
        VerifyFileIsUsableExecutable(text);
    }

    private static void VerifyFileIsUsableExecutable(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Executable not found.", path);
        }
        FileInfo fileInfo = new FileInfo(path);
        if (fileInfo.Length < 2)
        {
            throw new InvalidDataException("Executable is empty or truncated: " + path);
        }
        using FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        int num = fileStream.ReadByte();
        int num2 = fileStream.ReadByte();
        if (num != 77 || num2 != 90)
        {
            throw new InvalidDataException("File is not a valid Windows executable: " + path);
        }
    }

    private static void DownloadFile(string url, string destination)
    {
        Uri uri = new Uri(url);
        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Secure HTTPS transport is required.");
        }
        using WebClient webClient = new WebClient();
        webClient.Headers[HttpRequestHeader.UserAgent] = "SummerbloxLauncher/1.2.6";
        webClient.Headers[HttpRequestHeader.CacheControl] = "no-cache, no-store, must-revalidate";
        webClient.DownloadFile(uri, destination);
    }

    private static string DownloadString(string url)
    {
        Uri uri = new Uri(url);
        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Secure HTTPS transport is required.");
        }
        using WebClient webClient = new WebClient();
        webClient.Headers[HttpRequestHeader.UserAgent] = "SummerbloxLauncher/1.2.6";
        return webClient.DownloadString(uri);
    }

    private static void RegisterUriScheme(string targetExePath)
    {
        using RegistryKey registryKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default);
        using RegistryKey registryKey2 = registryKey.OpenSubKey("Software\\Classes", writable: true);
        if (registryKey2 == null)
        {
            throw new InvalidOperationException("Unable to access the current user's protocol registration area.");
        }
        using RegistryKey registryKey3 = registryKey2.CreateSubKey("summerblox");
        registryKey3.SetValue("", "URL:Summerblox Protocol");
        registryKey3.SetValue("URL Protocol", "");
        using RegistryKey registryKey4 = registryKey3.CreateSubKey("shell\\open\\command");
        registryKey4.SetValue("", "\"" + targetExePath + "\" \"%1\"", RegistryValueKind.String);
    }

    private static void HandleUriBehaviour(string rawUri)
    {
        try
        {
            rawUri = rawUri.Trim('"', ' ');
            Uri uri = new Uri(rawUri);
            if (!string.Equals(uri.Scheme, "summerblox", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Unsupported URI scheme.");
            }
            MainForm = new Form1();
            MainForm.Show();
            Application.DoEvents();
            if (MainForm.Controls["statusLabel"] is Label label)
            {
                label.Text = "Initializing connection...";
            }
            string query = uri.Query;
            string text = ExtractQueryParam(query, "year");
            string a = ExtractQueryParam(query, "method");
            bool flag = string.Equals(a, "studio", StringComparison.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(text))
            {
                if (!flag)
                {
                    throw new InvalidOperationException("Failed to parse the URI values.");
                }
                text = "2010";
            }
            ValidateYear(text);
            TargetClientYear = text;
            if (flag)
            {
                HandleStudioBehaviour(query);
            }
            else
            {
                HandlePlayerBehaviour(rawUri, query);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Hand);
            Application.Exit();
        }
    }

    private static void HandleStudioBehaviour(string query)
    {
        if (MainForm.Controls["statusLabel"] is Label label)
        {
            label.Text = "Launching Studio...";
        }
        Application.DoEvents();
        string text = Path.Combine(ClientsParentPath, TargetClientYear);
        string text2 = Path.Combine(text, "Summerblox.exe");
        string path = Path.Combine(text, "content");
        if (!File.Exists(text2) || !Directory.Exists(path))
        {
            PendingUriArgs = "summerblox://?method=studio&year=" + TargetClientYear + GetStudioQuerySuffix(query);
            MainForm.Close();
            InitializeFormAndInstall();
            return;
        }
        PerformIntegrityCheck();
        string text3 = "https://www.sumrbx.xyz/Game/editstudio.ashx" + GetStudioQuerySuffix(query);
        string text4 = GenerateRandomAlphaNumeric(32);
        string arguments = "-a \"https://www.sumrbx.xyz/Login/Negotiate.ashx\" -j \"" + text3 + "\" -t \"" + text4 + "\"";
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = text2,
            Arguments = arguments,
            UseShellExecute = false,
            WorkingDirectory = text
        };
        Process.Start(startInfo);
        Environment.Exit(0);
    }

    private static string GetStudioQuerySuffix(string query)
    {
        if (string.IsNullOrEmpty(query))
        {
            return string.Empty;
        }
        string text = (query.StartsWith("?") ? query.Substring(1) : query);
        string[] array = text.Split('&');
        List<string> list = new List<string>();
        string[] array2 = array;
        foreach (string text2 in array2)
        {
            if (!string.IsNullOrEmpty(text2))
            {
                int num = text2.IndexOf('=');
                string text3 = ((num >= 0) ? text2.Substring(0, num) : text2);
                string a;
                try
                {
                    a = Uri.UnescapeDataString(text3);
                }
                catch
                {
                    a = text3;
                }
                if (!string.Equals(a, "method", StringComparison.OrdinalIgnoreCase) && !string.Equals(a, "year", StringComparison.OrdinalIgnoreCase))
                {
                    list.Add(text2);
                }
            }
        }
        return (list.Count == 0) ? string.Empty : ("?" + string.Join("&", list.ToArray()));
    }

    private static void HandlePlayerBehaviour(string rawUri, string query)
    {
        Label label = MainForm.Controls["statusLabel"] as Label;
        string text = ExtractQueryParam(query, "key");
        string text2 = ExtractQueryParam(query, "universeId");
        string text3 = ExtractQueryParam(query, "userID");
        if (string.IsNullOrEmpty(text) || text.Length != 32)
        {
            throw new InvalidOperationException("Invalid key length.");
        }
        if (string.IsNullOrEmpty(text3))
        {
            throw new InvalidOperationException("Missing required userID parameter.");
        }
        string text4 = Path.Combine(ClientsParentPath, TargetClientYear);
        string text5 = Path.Combine(text4, "Summerblox.exe");
        string path = Path.Combine(text4, "content");
        bool flag = false;
        if (label != null)
        {
            label.Text = "Checking update status...";
        }
        Application.DoEvents();
        string rawResponse = DownloadString("https://www.sumrbx.xyz/_LAUNCHER/CheckVersion?version=" + Uri.EscapeDataString("1.2.6"));
        rawResponse = ProcessAndVerifySignedResponse(rawResponse);
        if (IsJsonValue(rawResponse, "status", "UpdateRequired"))
        {
            flag = true;
            UpdateDetectedDuringUriCheck = true;
        }
        if (!File.Exists(text5) || !Directory.Exists(path))
        {
            flag = true;
        }
        string text6 = "/Game/PlaceLauncher/" + TargetClientYear + ".ashx";
        if (!int.TryParse(TargetClientYear, out var result))
        {
            throw new InvalidOperationException("Invalid client year.");
        }
        string arguments;
        if (result >= 2012)
        {
            string text7 = GenerateRandomAlphaNumeric(32);
            string text8 = "https://www.sumrbx.xyz" + text6 + "?key=" + Uri.EscapeDataString(text) + "&UniverseID=" + Uri.EscapeDataString(text2 ?? string.Empty) + "&userID=" + Uri.EscapeDataString(text3);
            if (result >= 2013)
            {
                text8 = "https://www.sumrbx.xyz/game/join.ashx/?userID=" + Uri.EscapeDataString(text3) + "&key=" + Uri.EscapeDataString(text) + "&UniverseID=" + Uri.EscapeDataString(text2 ?? string.Empty) + "&year=" + result;
                if (text8.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    text8 = "http://" + text8.Substring(8);
                }
            }
            arguments = "-a \"https://www.sumrbx.xyz/Login/Negotiate.ashx\" -j \"" + text8 + "\" -t \"" + text7 + "\"";
        }
        else
        {
            string text9 = "https://www.sumrbx.xyz" + text6 + "?key=" + Uri.EscapeDataString(text) + "&UniverseID=" + Uri.EscapeDataString(text2 ?? string.Empty) + "&userID=" + Uri.EscapeDataString(text3);
            arguments = "-script \"loadfile('" + text9.Replace("'", "%27") + "')();\"";
        }
        if (flag)
        {
            PendingUriArgs = rawUri;
            MainForm.Close();
            InitializeFormAndInstall();
            return;
        }
        if (label != null)
        {
            label.Text = "Validating session key...";
        }
        Application.DoEvents();
        string rawResponse2 = DownloadString("https://www.sumrbx.xyz/_LAUNCHER/ValidateKey?key=" + Uri.EscapeDataString(text));
        rawResponse2 = ProcessAndVerifySignedResponse(rawResponse2);
        if (!IsJsonValue(rawResponse2, "success", "true"))
        {
            throw new InvalidOperationException("Key verification failed.");
        }
        if (label != null)
        {
            label.Text = "Performing integrity verification...";
        }
        Application.DoEvents();
        PerformIntegrityCheck();
        if (label != null)
        {
            label.Text = "Waiting for Server...";
        }
        Application.DoEvents();
        bool flag2 = false;
        for (int i = 0; i < 40; i++)
        {
            try
            {
                string rawResponse3 = DownloadString("https://www.sumrbx.xyz/_LAUNCHER/CheckGameStatus?gameID=" + Uri.EscapeDataString(text2 ?? string.Empty));
                rawResponse3 = ProcessAndVerifySignedResponse(rawResponse3);
                if (IsJsonValue(rawResponse3, "gameStatus", "Running") || IsJsonValue(rawResponse3, "gameStatus", "Online"))
                {
                    flag2 = true;
                    break;
                }
            }
            catch
            {
            }
            Thread.Sleep(1500);
        }
        if (!flag2)
        {
            throw new InvalidOperationException("Server took too long to start or is unresponsive.");
        }
        if (label != null)
        {
            label.Text = "Launching game...";
        }
        Application.DoEvents();
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = text5,
            Arguments = arguments,
            UseShellExecute = false,
            WorkingDirectory = text4
        };
        Process.Start(startInfo);
        Environment.Exit(0);
    }

    private static string ExtractQueryParam(string query, string paramName)
    {
        if (string.IsNullOrEmpty(query))
        {
            return null;
        }
        if (query.StartsWith("?"))
        {
            query = query.Substring(1);
        }
        string[] array = query.Split('&');
        string[] array2 = array;
        foreach (string text in array2)
        {
            int num = text.IndexOf('=');
            if (num >= 0)
            {
                string text2 = text.Substring(0, num);
                string text3 = text.Substring(num + 1);
                string a;
                string result;
                try
                {
                    a = Uri.UnescapeDataString(text2);
                    result = Uri.UnescapeDataString(text3);
                }
                catch
                {
                    a = text2;
                    result = text3;
                }
                if (string.Equals(a, paramName, StringComparison.OrdinalIgnoreCase))
                {
                    return result;
                }
            }
        }
        return null;
    }

    private static string ExtractJsonValue(string json, string key)
    {
        try
        {
            JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer();
            Dictionary<string, object> dictionary = javaScriptSerializer.Deserialize<Dictionary<string, object>>(json);
            if (dictionary != null && dictionary.TryGetValue(key, out var value))
            {
                return Convert.ToString(value);
            }
        }
        catch
        {
        }
        return string.Empty;
    }

    private static bool IsJsonValue(string json, string key, string expected)
    {
        string a = ExtractJsonValue(json, key);
        return string.Equals(a, expected, StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidateYear(string year)
    {
        if (!AllowedYears.Contains(year))
        {
            throw new InvalidOperationException("The requested client year is not supported.");
        }
        if (string.Equals(year, "2014", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The 2014 client is no longer supported.");
        }
    }

    private static void ValidateVersion(string version)
    {
        if (!System.Version.TryParse(version, out var _))
        {
            throw new InvalidOperationException("The update server returned an invalid launcher version.");
        }
    }

    private static void CopyDirectoryContents(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);
        string[] files = Directory.GetFiles(sourceDirectory, "*", SearchOption.TopDirectoryOnly);
        foreach (string text in files)
        {
            string destFileName = Path.Combine(destinationDirectory, Path.GetFileName(text));
            File.Copy(text, destFileName, overwrite: false);
        }
        string[] directories = Directory.GetDirectories(sourceDirectory, "*", SearchOption.TopDirectoryOnly);
        foreach (string text2 in directories)
        {
            string destinationDirectory2 = Path.Combine(destinationDirectory, Path.GetFileName(text2));
            CopyDirectoryContents(text2, destinationDirectory2);
        }
    }

    private static void ReplaceDirectoryAtomically(string preparedPath, string destinationPath)
    {
        string text = destinationPath + ".old." + Guid.NewGuid().ToString("N");
        if (Directory.Exists(destinationPath))
        {
            Directory.Move(destinationPath, text);
        }
        try
        {
            Directory.Move(preparedPath, destinationPath);
        }
        catch
        {
            if (!Directory.Exists(destinationPath) && Directory.Exists(text))
            {
                Directory.Move(text, destinationPath);
            }
            throw;
        }
        DeleteDirectoryQuietly(text);
    }

    private static void CopyFileAtomically(string source, string destination)
    {
        string directoryName = Path.GetDirectoryName(destination);
        if (!string.IsNullOrEmpty(directoryName))
        {
            Directory.CreateDirectory(directoryName);
        }
        string text = destination + ".tmp." + Guid.NewGuid().ToString("N");
        try
        {
            File.Copy(source, text, overwrite: false);
            if (File.Exists(destination))
            {
                File.Replace(text, destination, null, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(text, destination);
            }
        }
        finally
        {
            DeleteFileQuietly(text);
        }
    }

    private static bool PathsEqual(string left, string right)
    {
        try
        {
            return string.Equals(Path.GetFullPath(left).TrimEnd('\\'), Path.GetFullPath(right).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static string EnsureTrailingSeparator(string path)
    {
        if (path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
        {
            return path;
        }
        return path + Path.DirectorySeparatorChar;
    }

    private static void EnsureDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }

    private static void DeleteFileQuietly(string path)
    {
        try
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    private static void DeleteDirectoryQuietly(string path)
    {
        try
        {
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
        }
    }

    private static void CreateDesktopShortcuts(string launcherExePath)
    {
        string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
        {
            return;
        }
        string path = Path.Combine(ClientsParentPath, TargetClientYear);
        string text = Path.Combine(path, "Summerblox.exe");
        if (!File.Exists(text))
        {
            return;
        }
        string shortcutPath = Path.Combine(folderPath, "Summerblox Studio " + TargetClientYear + ".lnk");
        CreateShellShortcut(shortcutPath, text, string.Empty, text, 0);
        string path2 = Path.Combine(folderPath, "Play Summerblox.url");
        using StreamWriter streamWriter = new StreamWriter(path2, append: false, Encoding.UTF8);
        streamWriter.WriteLine("[InternetShortcut]");
        streamWriter.WriteLine("URL=https://www.sumrbx.xyz");
        streamWriter.WriteLine("IconFile=" + launcherExePath);
        streamWriter.WriteLine("IconIndex=0");
    }

    private static void CreateShellShortcut(string shortcutPath, string targetPath, string arguments, string iconPath, int iconIndex)
    {
        Type typeFromProgID = Type.GetTypeFromProgID("WScript.Shell");
        if (!(typeFromProgID == null))
        {
            object target = Activator.CreateInstance(typeFromProgID);
            object obj = typeFromProgID.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, target, new object[1] { shortcutPath });
            Type type = obj.GetType();
            type.InvokeMember("TargetPath", BindingFlags.SetProperty, null, obj, new object[1] { targetPath });
            type.InvokeMember("Arguments", BindingFlags.SetProperty, null, obj, new object[1] { arguments });
            type.InvokeMember("IconLocation", BindingFlags.SetProperty, null, obj, new object[1] { iconPath + "," + iconIndex });
            type.InvokeMember("Save", BindingFlags.InvokeMethod, null, obj, new object[0]);
        }
    }
}

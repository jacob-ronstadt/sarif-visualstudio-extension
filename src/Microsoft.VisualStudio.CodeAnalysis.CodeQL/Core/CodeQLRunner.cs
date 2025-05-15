using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

using Microsoft.VisualStudio.CodeAnalysis.CodeQL.Exceptions;

using static System.Net.WebRequestMethods;

namespace Microsoft.VisualStudio.CodeAnalysis.CodeQL.Runner
{

    /// <summary>
    /// The CodeQLRunner class is responsible for running CodeQL commands and managing the analysis process.
    /// </summary>
    public sealed class CodeQLRunner
    {
        /// <summary>
        /// 
        /// </summary>
        public TaskCompletionSource<bool> eventHandled;

        /// <summary>
        /// Exit code of last process to finish
        /// </summary>
        public int LastExitCode = 0;

        /// <summary>
        /// Path to most recently created Database
        /// </summary>
        public string DatabasePath = string.Empty;

        /// <summary>
        /// Where the databases will be placed
        /// </summary>
        public string DatabaseDirectory { get; set; }

        /// <summary>
        /// Where codeql will be installed
        /// </summary>
        public string InstallDirectory { get; set; }

        /// <summary>
        /// Directory for analysis output.
        /// </summary>
        private string analysisDir;

        /// <summary>
        /// Target platform (e.g., x64).
        /// </summary>
        private string platform;

        /// <summary>
        /// 
        /// </summary>
        private string sourceDir;

        /// <summary>
        /// Optional output windows pane
        /// </summary>
        private Func<string,string, Task> outputFunc;

        /// <summary>
        /// cmd command for build environment setup
        /// </summary>
        private string buildEnv;

        /// <summary>
        ///
        /// </summary>
        private static string CodeQLExe;

        /// <summary>
        ///
        /// </summary>
        public static readonly Dictionary<string, string> requiredPacks = new Dictionary<string, string>()
        {
            {"microsoft/windows-drivers", "1.5.0-beta+5" },
            {"microsoft/cpp-queries", "0.0.2" },
            {"codeql/cpp-all", "4.0.0" }
        };

        /// <summary>
        ///
        /// </summary>
        public static readonly Dictionary<string, string> packQuerySuites = new Dictionary<string, string>()
        {
            {"mustfix", "microsoft/windows-drivers@1.5.0-beta+5:windows-driver-suites/mustfix.qls"},
            {"recommended", "microsoft/windows-drivers@1.5.0-beta+5:windows-driver-suites/recommended.qls"},
            {"mustrun", "microsoft/windows-drivers@1.5.0-beta+5:windows-driver-suites/recommended.qls"}
        };

        /// <summary>
        /// 
        /// </summary>
        private readonly static string defaultCodeQLPath = "C:\\codeql-home\\codeql\\codeql.exe";

        public static async Task<string> GetLatestVersionAsync()
        {
            using (var client = new HttpClient())
            {
                string url = "https://github.com/github/codeql-cli-binaries/releases/latest";
                HttpResponseMessage response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();
                using (Stream stream = await response.Content.ReadAsStreamAsync())
                {
                    using (var reader = new StreamReader(stream))
                    {
                        string line;
                        while ((line = await reader.ReadLineAsync()) != null)
                        {
                            if (line.Contains("<title>") && line.Contains("Release"))
                            {
                                string version = Regex.Replace(line, "[^0-9`.]+", "", RegexOptions.IgnoreCase);
                                return version;
                            }
                        }
                    }
                }
                return string.Empty;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        /// <exception cref="CodeQLPacksNotFoundException"></exception>
        public static async Task InstallRequiredPacksAsync()
        {
            foreach (KeyValuePair<string, string> pack in requiredPacks)
            {
                try
                {
                    await InstallPackAsync(pack.Key, pack.Value);
                }
                catch (Exception ex)
                {
                    throw new CodeQLPacksNotFoundException("Could not install required CodeQL pack(s)", ex);
                }
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="pack"></param>
        /// <param name="version"></param>
        /// <returns></returns>
        public static async Task InstallPackAsync(string pack, string version)
        {
            string codeqlPath = GetInstalLocation();
            string output = await RunCodeQLProcAsync("pack download " + pack + "@" + version + " --allow-prerelease --force -v");
            foreach (string line in output.Split(
                               new string[] { "\r\n", "\r", "\n" },
                               StringSplitOptions.None))
            {
                if (line.Contains("Unzipping package to"))
                {
                    string path = line.Replace("Unzipping package to ", "").Replace("'", "").Trim();
                    await RunCodeQLProcAsync("pack ci " + path);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="workingDir"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public static async Task<string> RunCodeQLProcAsync(string cmd, string workingDir = null)
        {
            if (CodeQLExe == string.Empty)
            {
                CodeQLExe = GetInstalLocation();
            }
            System.Diagnostics.Process proc = new System.Diagnostics.Process();
            proc.StartInfo.FileName = CodeQLExe;
            proc.StartInfo.Arguments = cmd;
            proc.StartInfo.RedirectStandardOutput = true;
            proc.StartInfo.RedirectStandardError = true;
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.CreateNoWindow = true;
            if (workingDir != null)
            {
                proc.StartInfo.WorkingDirectory = workingDir;
            }

            proc.Start();
            string output = await proc.StandardOutput.ReadToEndAsync();
            proc.WaitForExit();
            if (proc.ExitCode != 0)
            {
                throw new InvalidOperationException("Process error: " + proc.ExitCode + " " + cmd + " " + output);
            }
            return output;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="qlpacks"></param>
        /// <returns></returns>
        public static async Task<List<string>> FindQueriesAsync(List<string> qlpacks, bool queriesNSuites = true)
        {
            List<string> queries = new List<string>();
            // make async
            List<Task> tasks = new List<Task>();
            foreach (var pack in qlpacks)
            {
                tasks.Add(Task.Run(async () => queries.AddRange(await FindQueriesAsync(pack, queriesNSuites))));
            }
            await Task.WhenAll(tasks);
            return queries;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="qlpack"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public static async Task<List<string>> FindQueriesAsync(string qlpack, bool queriesNSuites = true)
        {
          
            string output = await RunCodeQLProcAsync("pack packlist " + qlpack + " --format=json");
            List<string> queries = new List<string>();
            foreach (string line in output.Split(
                                    new string[] { "\r\n", "\r", "\n" },
                                    StringSplitOptions.None
                               ))
            {
                string ext;
                if (!queriesNSuites)
                {
                    ext = ".qls";
                }
                else
                {
                    ext = ".ql";
                }
                if (line.Trim().Trim(new char[] { ',', '\"'}).Trim().EndsWith(ext))
                {
                    string query = line.Replace("\"", "").Replace("\\\\", "/").Replace("\\", "/").Trim(',').Trim();
                    queries.Add(query);
                }

            }
            return queries; // TODO do this with json
        }

        /// <summary>
        /// Finds all CodeQL packs installed on the system.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public static async Task<List<string>> FindPacksAsync()
        {
            string output = await RunCodeQLProcAsync("resolve packs --show-hidden-packs --format=json");
            List<string> packs = new List<string>();
            foreach (string line in output.Split(
                                    new string[] { "\r\n", "\r", "\n" },
                                    StringSplitOptions.None
                               ))
            {
                if (line.Contains("legacy-upgrades"))
                {
                    continue;
                }
                if (line.Contains("\"path\"") && line.Contains("qlpack.yml"))
                {
                    packs.Add(line.Replace("\"path\" : ", "").Replace("\"", "").Replace("\\\\", "/").Replace("\\", "/").Trim());
                }
            }
            return packs; // TODO do this with json
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public static async Task<List<string>> FindMissingPacksAsync()
        {
            List<string> packList = await FindPacksAsync();
            List<string> missingPacks = requiredPacks.Keys.ToList();
            foreach (string packPath in packList)
            {
                foreach (KeyValuePair<string, string> reqPack in requiredPacks)
                {
                    if (packPath.Contains(reqPack.Key) && packPath.Contains(reqPack.Value))
                    {
                        missingPacks.Remove(reqPack.Key);
                    }
                }
            }
            return missingPacks;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        /// <exception cref="CodeQLExeNotFoundException"></exception>
        private static string GetInstalLocation()
        {
            if (File.Exists(defaultCodeQLPath))
            {
                return defaultCodeQLPath;
            }
            else
            {
                System.Diagnostics.Process proc = new System.Diagnostics.Process();

                proc.StartInfo.FileName = "cmd.exe";
                proc.StartInfo.Arguments = "/C " + "codeql resolve extractor --language=cpp";
                proc.StartInfo.RedirectStandardOutput = true;
                proc.StartInfo.RedirectStandardError = true;
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.CreateNoWindow = true;

                proc.Start();
                string output = "";
                while (!proc.StandardOutput.EndOfStream)
                {
                    output = proc.StandardOutput.ReadLine() ?? "";
                }
                proc.WaitForExit();

                if (output == null || string.IsNullOrEmpty(output))
                {
                    throw new CodeQLExeNotFoundException("CodeQL not installed or not part of PATH");
                }
                if (proc.ExitCode != 0)
                {
                    throw new CodeQLExeNotFoundException("CodeQL not installed or not part of PATH");
                }
                // output should be the extractor location for cpp, need the exe location one directory up
                string[] outputParts = output.Split(Path.PathSeparator);
                string path = string.Join(Path.PathSeparator.ToString(), outputParts.Take(outputParts.Length - 1)) + "codeql.exe";

                if (!File.Exists(path))
                {
                    throw new CodeQLExeNotFoundException("CodeQL not installed or not part of PATH");
                }
                return path;
            }

        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="arch"></param>
        /// <param name="sourceDir"></param>
        /// <param name="owp"></param>
        /// <param name="buildEnv"></param>
        /// <param name="dbDir"></param>
        public CodeQLRunner(string arch, string sourceDir, Func<string,string, Task> owp = null, string buildEnv = "", string dbDir = "")
        {
            outputFunc = owp;
            platform = arch;
            this.sourceDir = sourceDir;
            this.buildEnv = buildEnv;
            if(dbDir != string.Empty)
            {
                analysisDir = dbDir;
            }
            else
            {
                analysisDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "codeql_databases", sourceDir.GetHashCode().ToString());
            }
            if (!Directory.Exists(analysisDir))
            {
                Directory.CreateDirectory(analysisDir);
            }

            CodeQLExe = GetInstalLocation();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void processExited(object sender, System.EventArgs e)
        {
            eventHandled.TrySetResult(true);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<int> CountDBSourceCodeLinesAsync()
        {
            string output = await RunCodeQLProcAsync("database print-baseline codeql_db", workingDir: analysisDir);
            foreach (string line in output.Split(Environment.NewLine.ToCharArray()[0]))
            {
                if (line.EndsWith("cpp."))
                {
                    int lines = int.Parse(line.Replace("Counted a baseline of", "").Replace("lines of code for cpp.", "").Trim());
                    return lines;
                }
                // TODO other languages
            }
            throw new Exception("Database not found or no source code found");
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <param name="sourceId"></param>
        /// <returns></returns>
        public async Task AddDBDiagInfoAsync(string message, int sourceId)
        {
            string output = await RunCodeQLProcAsync("database add-diagnostic --plaintext-message=\"" +message + "\" --source-id="+ sourceId.ToString() + " --source-name=CodeqlVSExt", workingDir: analysisDir);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public async Task<string> GetDBDiagAsync()
        {
            return await RunCodeQLProcAsync("database export-diagnostics .\\codeql_db\\ --format=raw", workingDir: analysisDir);
        }

        /// <summary>
        ///
        /// </summary>
        public async Task RunCMDProcAsync(string strCmdText, Action<object, System.EventArgs> proccessExitedFunc, CancellationToken ct)
        {
            using(System.Diagnostics.Process codeqlProc = new System.Diagnostics.Process())
            using (ct.Register(() =>
            {
                if(codeqlProc != null)
                {
                    KillProcessAndChildren(codeqlProc.Id);
                }
                eventHandled.TrySetCanceled();
            }))
            {
                try
                {
                    codeqlProc.StartInfo.FileName = "cmd.exe";
                    codeqlProc.StartInfo.Arguments = "/C " + "\"" + strCmdText + "\"";
                    codeqlProc.StartInfo.UseShellExecute = false;
                    codeqlProc.StartInfo.CreateNoWindow = true;
                    codeqlProc.StartInfo.RedirectStandardError = false;

                    if (outputFunc != null)
                    {
                        codeqlProc.StartInfo.RedirectStandardOutput = true;
                        codeqlProc.StartInfo.RedirectStandardError = true;
                        codeqlProc.StartInfo.RedirectStandardInput = true;
                        codeqlProc.OutputDataReceived += (sender, e) =>
                        {
                            if (!string.IsNullOrEmpty(e.Data))
                            {
                                _ = outputFunc("CodeQL",e.Data);
                            }
                        };
                        codeqlProc.ErrorDataReceived += (sender, e) =>
                        {
                            if (!string.IsNullOrEmpty(e.Data))
                            {
                                _ = outputFunc("CodeQL", e.Data);
                            }
                        };
                    }
                    else
                    {
                        codeqlProc.StartInfo.RedirectStandardOutput = false;
                    }
                    codeqlProc.EnableRaisingEvents = true;
                    codeqlProc.Exited += new EventHandler(proccessExitedFunc);

                    codeqlProc.Start();
                    if (outputFunc != null)
                    {
                        codeqlProc.BeginOutputReadLine();
                        codeqlProc.BeginErrorReadLine();
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception(ex.ToString());
                }
                await Task.WhenAny(eventHandled.Task);

                if (codeqlProc.ExitCode != 0)
                {
                    if (ct != null && ct.IsCancellationRequested)
                    {
                        if (outputFunc != null)
                        {
                            _ = outputFunc("CodeQL","CodeQL process killed");
                        }
                    }
                    else
                    {
                        throw new CodeQLException("CodeQL Error. Process exited with code " + codeqlProc.ExitCode);
                    }
                }
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="pid"></param>
        private void KillProcessAndChildren(int pid)
        {
            // Cannot close 'system idle process'.
            if (pid == 0)
            {
                return;
            }
            ManagementObjectSearcher searcher = new ManagementObjectSearcher
                    ("Select * From Win32_Process Where ParentProcessID=" + pid);
            ManagementObjectCollection moc = searcher.Get();
            foreach (ManagementObject mo in moc)
            {
                KillProcessAndChildren(Convert.ToInt32(mo["ProcessID"]));
            }
            try
            {
                System.Diagnostics.Process proc = System.Diagnostics.Process.GetProcessById(pid);
                proc.Kill();
            }
            catch (ArgumentException)
            {
                // Process already exited.
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="suite"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public async Task<List<string>>GetQueriesFromSuiteAsync(string suite)
        {
            if(packQuerySuites.TryGetValue(suite, out string queryPath))
            {
                List<string> queries = new List<string>();
             
                string output = await RunCodeQLProcAsync("resolve queries " + queryPath);
                foreach (string line in output.Split(Environment.NewLine.ToCharArray()[0]))
                {
                    string query = line.Replace("\"", "").Replace("\\\\", "/").Replace("\\", "/").Trim();
                    if (line.EndsWith(".ql"))
                    {
                        queries.Add(query);
                    }
                }
                return queries;
            }
            else
            {
                throw new ArgumentException("Invalid query suite name: " + suite);
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        /// <exception cref="CodeQLPacksNotFoundException"></exception>
        public async Task CheckCodeQLPacksInstalledAsync()
        {
            List<string> missingPacks = await FindMissingPacksAsync();
            if (missingPacks.Count != 0)
            {
                throw new CodeQLPacksNotFoundException("Could not find required CodeQL pack(s)");
            }
        }

        
        /// <summary>
        /// Generates a CodeQL database for the specified project file.
        /// </summary>
        /// <returns>The path to the generated database.</returns>
        /// <exception cref="Exception">Thrown if the analysis directory does not exist.</exception>
        public async Task GenerateDatabaseAsync(string buildCommand, CancellationToken ct, Action<object, System.EventArgs> proccessExitedFunc = null)
        {
            if (ct.IsCancellationRequested )
            {
                return;
            }
            eventHandled = new TaskCompletionSource<bool>();
         
            if (proccessExitedFunc == null)
            {
                // use default if none provided
                proccessExitedFunc = processExited;
            }

            string strCmdText = string.Empty;
          
            try
            {
                string dbPath = Path.Combine(analysisDir, "codeql_db");
                
                string[] procArr = {CodeQLExe, "database",
                "create","\"" +  dbPath + "\"",
                "--force-overwrite",
                "--language=cpp",
                "--source-root="+"\"" + sourceDir + "\"",
                "--command="+ "\"" + buildCommand + "\""};
                strCmdText = string.Join(" ", procArr);
                if (!string.IsNullOrWhiteSpace(buildEnv))
                {
                    strCmdText = buildEnv+ " " + strCmdText;
                }
                await RunCMDProcAsync(strCmdText, proccessExitedFunc, ct);
            }
            catch (Exception ex)
            {
                throw new Exception("Create DB Failed with command: " + strCmdText + " | " + ex.ToString(), ex);
            }

        }

        /// <summary>
        ///
        /// </summary>
        public async Task<string> RunCodeQLQuerySetAsync(string querySet, CancellationToken ct, Action<object, System.EventArgs> proccessExitedFunc = null)
        {
            if (ct.IsCancellationRequested)
            {
                return null;
            }
            eventHandled = new TaskCompletionSource<bool>();
           
            string strCmdText = string.Empty;
            if (proccessExitedFunc == null)
            {
                // use default if none provided
                proccessExitedFunc = processExited;
            }

            try
            {
                string dbPath = Path.Combine(analysisDir, "codeql_db");
                if (!Directory.Exists(dbPath))
                {
                    throw new DatabaseNotFinalizedException("Database not created for " + sourceDir);
                }
                else
                {
                    var lastLine = File.ReadLines(Path.Combine(dbPath, "codeql-database.yml")).Last();
                    if (!lastLine.Contains("true"))
                    {
                        throw new DatabaseNotFinalizedException("CodeQL database error");
                    }
                }
                string resultsDir = Path.Combine(sourceDir, ".sarif");
                if(!Directory.Exists(resultsDir))
                {
                    Directory.CreateDirectory(resultsDir);
                }
                string resultsPath = Path.Combine(resultsDir, "results.sarif");

                string suiteFile;
                if(!packQuerySuites.TryGetValue(querySet, out suiteFile))
                {
                    if(!File.Exists(querySet))
                    {
                        throw new ArgumentException("Invalid query suite name: " + querySet);
                    }
                    else
                    {
                        suiteFile = querySet;
                    }
                }
                string[] procArr = {CodeQLExe,"database",
                    "analyze","\"" +  dbPath + "\"",
                    "-v",
                    "--format=sarifv2.1.0",
                    "--output="+"\"" + resultsPath + "\"",
                    suiteFile}; 

                strCmdText = string.Join(" ", procArr);
                if (!string.IsNullOrWhiteSpace(buildEnv))
                {
                    strCmdText = buildEnv + " " + strCmdText;
                }
                await RunCMDProcAsync(strCmdText, proccessExitedFunc, ct);
                return resultsPath;
            }
            catch (Exception ex)
            {
                throw new CodeQLException("Analyze DB Failed with command: " + strCmdText + " | " + ex.ToString(), ex);
            }
        }
    }
}

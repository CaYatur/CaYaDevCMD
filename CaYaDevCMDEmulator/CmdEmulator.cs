using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net.NetworkInformation;
using System.Management;

namespace CaYaDevCMDEmulator
{
    public partial class CmdEmulator : Form
    {
        private List<string> commandHistory = new List<string>();
        private string currentDirectory = Environment.CurrentDirectory;
        private Dictionary<string, Func<string[], bool>> internalCommands;

        // Çalıştırılan komutların çıktılarını kilitleme için
        private readonly object outputLock = new object();

        // Konsol modu için çıktıyı saklamak
        private StringBuilder consoleOutput = new StringBuilder();
        private bool isConsoleMode = false;

        // Constructor
        public CmdEmulator(bool consoleMode = false)
        {
            isConsoleMode = consoleMode;

            if (!isConsoleMode)
            {
                InitializeComponent();
                // Başlangıç mesajı
                outputBox.AppendText("Microsoft Windows [CaYaDev Emulated Version CommandLine]\n");
                outputBox.AppendText($"(c) 2025 CaYaDev. Kullanıcı: {Environment.UserName}\n\n");
                outputBox.AppendText($"{currentDirectory}>\n\n");
            }

            // Dahili komutları başlat
            InitializeInternalCommands();
        }

        private void InitializeInternalCommands()
        {
            internalCommands = new Dictionary<string, Func<string[], bool>>(StringComparer.OrdinalIgnoreCase)
            {
                { "dir", ListDirectory },
                { "ls", ListDirectory },
                { "cd", ChangeDirectory },
                { "cls", ClearScreen },
                { "clear", ClearScreen },
                { "echo", EchoText },
                { "tasklist", ListProcesses },
                { "taskkill", KillProcess },
                { "ipconfig", ShowIpConfig },
                { "ping", PingHost },
                { "systeminfo", ShowSystemInfo },
                { "type", TypeFile },
                { "copy", CopyFile },
                { "move", MoveFile },
                { "del", DeleteFile },
                { "delete", DeleteFile },
                { "mkdir", CreateDirectory },
                { "rmdir", RemoveDirectory },
                { "start", StartProcess },
                { "help", ShowHelp },
                { "?", ShowHelp }
            };
        }

        private void ClearButton_Click(object sender, EventArgs e)
        {
            outputBox.Clear();
        }

        private void HistoryBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (historyBox.SelectedIndex != -1)
                inputBox.Text = historyBox.SelectedItem.ToString();
        }

        /// <summary>
        /// Komut satırından bir komutu doğrudan çalıştırır ve sonucu döndürür
        /// </summary>
        public string ExecuteCommandLine(string command)
        {
            consoleOutput.Clear();

            try
            {
                // Komutu işle
                if (command.Contains("&&&"))
                {
                    // Paralel çalıştırma
                    string[] commands = command.Split(new[] { "&&&" }, StringSplitOptions.RemoveEmptyEntries);
                    ExecuteCommandsParallelConsole(commands);
                }
                else if (command.Contains("&&"))
                {
                    // Sıralı çalıştırma
                    string[] commands = command.Split(new[] { "&&" }, StringSplitOptions.RemoveEmptyEntries);
                    ExecuteCommandsSequentialConsole(commands);
                }
                else
                {
                    // Tek komut çalıştırma
                    ProcessSingleCommandConsole(command.Trim());
                }

                return consoleOutput.ToString();
            }
            catch (Exception ex)
            {
                return $"Hata: {ex.Message}\n";
            }
        }

        private void InputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                ExecuteCommand();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down)
            {
                // Komut geçmişinde gezinme
                if (commandHistory.Count > 0)
                {
                    int currentIndex = historyBox.SelectedIndex;

                    if (e.KeyCode == Keys.Up && currentIndex > 0)
                    {
                        historyBox.SelectedIndex = currentIndex - 1;
                    }
                    else if (e.KeyCode == Keys.Down && currentIndex < historyBox.Items.Count - 1)
                    {
                        historyBox.SelectedIndex = currentIndex + 1;
                    }
                    else if (e.KeyCode == Keys.Up && currentIndex == -1)
                    {
                        historyBox.SelectedIndex = historyBox.Items.Count - 1;
                    }
                }

                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private void ExecuteButton_Click(object sender, EventArgs e)
        {
            ExecuteCommand();
        }

        private void ExecuteCommand()
        {
            string command = inputBox.Text.Trim();

            if (string.IsNullOrEmpty(command))
                return;

            // Komutu geçmişe ekle
            if (!commandHistory.Contains(command))
            {
                commandHistory.Add(command);
                historyBox.Items.Add(command);
            }

            // Komutu ekrana yaz
            outputBox.AppendText($"{currentDirectory}> {command}\n");
            inputBox.Clear();

            // Çoklu komutları işle (&& veya &&& içerenler)
            if (command.Contains("&&&"))
            {
                // Paralel çalıştırma
                string[] commands = command.Split(new[] { "&&&" }, StringSplitOptions.RemoveEmptyEntries);
                ExecuteCommandsParallel(commands);

                // Paralel işlemler arka planda yürütüldüğünden, 
                // burada komut istemcisi hazır göstergesini hemen gösterme
                // Bunun yerine, tüm işlemler tamamlandığında gösterilecek
            }
            else if (command.Contains("&&"))
            {
                // Sıralı çalıştırma
                string[] commands = command.Split(new[] { "&&" }, StringSplitOptions.RemoveEmptyEntries);
                ExecuteCommandsSequential(commands);

                // Sıralı işlemler tamamlandığında komut istemini göster
                outputBox.AppendText($"\n{currentDirectory}> ");
            }
            else
            {
                // Tek komut çalıştırma
                ProcessSingleCommand(command);

                // İşlem tamamlandığında komut istemini göster
                outputBox.AppendText($"\n{currentDirectory}> ");
            }
        }

        #region Command Execution Methods

        private void ExecuteCommandsSequential(string[] commands)
        {
            foreach (string cmd in commands)
            {
                ProcessSingleCommand(cmd.Trim());
            }
        }

        private void ExecuteCommandsSequentialConsole(string[] commands)
        {
            foreach (string cmd in commands)
            {
                ProcessSingleCommandConsole(cmd.Trim());
            }
        }

        private void ExecuteCommandsParallel(string[] commands)
        {
            // Çalıştırılacak görevlerin listesini oluştur
            List<Task> tasks = new List<Task>();

            // Her komut için ayrı bir task oluştur
            foreach (string cmd in commands)
            {
                string trimmedCmd = cmd.Trim();
                tasks.Add(Task.Run(() => {
                    try
                    {
                        // Her task kendi içinde komutu işlesin
                        ProcessSingleCommandSafe(trimmedCmd);
                    }
                    catch (Exception ex)
                    {
                        SafeWriteError($"Komut çalıştırma hatası: {ex.Message}");
                    }
                }));
            }

            // UI thread'i bloke etmeden task'ları takip et
            Task.Run(async () => {
                try
                {
                    await Task.WhenAll(tasks.ToArray());
                    // Tüm görevler tamamlandığında UI'yi güncelle
                    this.BeginInvoke(new Action(() => {
                        // İşlemler bitince hazır olduğunu belirt
                        AppendOutput("\nParalel işlemler tamamlandı.\n");
                    }));
                }
                catch (Exception ex)
                {
                    this.BeginInvoke(new Action(() => {
                        WriteError($"Paralel çalıştırma hatası: {ex.Message}");
                    }));
                }
            });
        }

        // Thread-safe komut işleme metodu
        private void ProcessSingleCommandSafe(string command)
        {
            // Komutu parçalara ayır
            string[] parts = SplitCommandLine(command);

            if (parts.Length == 0)
                return;

            string cmd = parts[0].ToLower();
            string[] args = parts.Skip(1).ToArray();

            // İç komut mu kontrol et ve çalıştır
            if (internalCommands.ContainsKey(cmd))
            {
                try
                {
                    bool success = false;

                    // UI thread'inde çalıştırılması gereken kod için
                    if (this.InvokeRequired)
                    {
                        // Senkron olarak UI thread'inde çalışması için
                        this.Invoke(new Action(() => {
                            try
                            {
                                success = internalCommands[cmd](args);
                                if (!success)
                                {
                                    WriteError($"'{cmd}' komutu hatalı parametre ile kullanıldı.");
                                }
                            }
                            catch (Exception ex)
                            {
                                WriteError($"Hata: {ex.Message}");
                            }
                        }));
                    }
                    else
                    {
                        // Zaten UI thread'indeyse direkt çalıştır
                        success = internalCommands[cmd](args);
                        if (!success)
                        {
                            WriteError($"'{cmd}' komutu hatalı parametre ile kullanıldı.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    SafeWriteError($"Hata: {ex.Message}");
                }
            }
            else if (cmd == "sc")
            {
                // SC komutu için özel işleme (thread-safe olmalı)
                SafeManageService(args);
            }
            else
            {
                try
                {
                    // Önce dosya olarak mevcut mu kontrol et
                    string exePath = FindExecutableInPath(cmd);
                    if (File.Exists(exePath))
                    {
                        // Harici komut çalıştırma denemesi (thread-safe olmalı)
                        ExecuteExternalCommandSafe(command);
                    }
                    else
                    {
                        // Tanınmayan komut
                        SafeWriteError($"'{cmd}' dahili veya harici bir komut veya çalıştırılabilir program olarak tanınmıyor.");
                    }
                }
                catch (Exception ex)
                {
                    SafeWriteError($"Hata: {ex.Message}");
                }
            }
        }

        // Güvenli hata yazma metodu 
        private void SafeWriteError(string message)
        {
            try
            {
                if (this.InvokeRequired)
                {
                    this.BeginInvoke(new Action(() => WriteError(message)));
                }
                else
                {
                    WriteError(message);
                }
            }
            catch (ObjectDisposedException)
            {
                // Form kapatılmış olabilir, sessizce görmezden gel
            }
            catch (InvalidOperationException)
            {
                // Form kapatılmış olabilir, sessizce görmezden gel
            }
        }

        // Güvenli dış komut çalıştırma
        private void ExecuteExternalCommandSafe(string command)
        {
            try
            {
                string[] parts = SplitCommandLine(command);
                string executable = parts[0];
                string arguments = string.Join(" ", parts.Skip(1));

                // Tam yol belirtilmemişse PATH içinde ara
                if (!File.Exists(executable) && !executable.Contains("\\") && !executable.Contains("/"))
                {
                    executable = FindExecutableInPath(executable);
                }

                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = executable;
                psi.Arguments = arguments;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.WorkingDirectory = currentDirectory;

                using (Process process = new Process())
                {
                    process.StartInfo = psi;

                    process.OutputDataReceived += (sender, e) => {
                        if (e.Data != null)
                        {
                            SafeAppendOutput(e.Data + "\n");
                        }
                    };

                    process.ErrorDataReceived += (sender, e) => {
                        if (e.Data != null)
                        {
                            SafeWriteError(e.Data);
                        }
                    };

                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    process.WaitForExit();
                }
            }
            catch (Exception ex)
            {
                SafeWriteError($"Hata: {ex.Message}");
            }
        }

        // Güvenli çıktı ekleme metodu
        private void SafeAppendOutput(string message)
        {
            try
            {
                if (this.InvokeRequired)
                {
                    this.BeginInvoke(new Action(() => AppendOutput(message)));
                }
                else
                {
                    AppendOutput(message);
                }
            }
            catch (ObjectDisposedException)
            {
                // Form kapatılmış olabilir, sessizce görmezden gel
            }
            catch (InvalidOperationException)
            {
                // Form kapatılmış olabilir, sessizce görmezden gel
            }
        }

        // Servis komutları için thread-safe metot
        private void SafeManageService(string[] args)
        {
            try
            {
                if (this.InvokeRequired)
                {
                    this.Invoke(new Action(() => ManageService(args)));
                }
                else
                {
                    ManageService(args);
                }
            }
            catch (Exception ex)
            {
                SafeWriteError($"Servis komutu hatası: {ex.Message}");
            }
        }

        private void ExecuteCommandsParallelConsole(string[] commands)
        {
            List<Task> tasks = new List<Task>();

            foreach (string cmd in commands)
            {
                string trimmedCmd = cmd.Trim();
                tasks.Add(Task.Run(() => ProcessSingleCommandConsole(trimmedCmd)));
            }

            try
            {
                Task.WaitAll(tasks.ToArray());
            }
            catch (Exception ex)
            {
                ConsoleWriteError($"Paralel çalıştırma hatası: {ex.Message}");
            }
        }

        private void ProcessSingleCommand(string command)
        {
            // Komutu parçalara ayır
            string[] parts = SplitCommandLine(command);

            if (parts.Length == 0)
                return;

            string cmd = parts[0].ToLower();
            string[] args = parts.Skip(1).ToArray();

            // İç komut mu kontrol et ve çalıştır
            if (internalCommands.ContainsKey(cmd))
            {
                try
                {
                    bool success = internalCommands[cmd](args);
                    if (!success)
                    {
                        WriteError($"'{cmd}' komutu hatalı parametre ile kullanıldı.");
                    }
                }
                catch (Exception ex)
                {
                    WriteError($"Hata: {ex.Message}");
                }
            }
            else if (cmd == "sc")
            {
                // SC komutu için özel işleme
                ManageService(args);
            }
            else
            {
                try
                {
                    // Önce dosya olarak mevcut mu kontrol et
                    string exePath = FindExecutableInPath(cmd);
                    if (File.Exists(exePath))
                    {
                        // Harici komut çalıştırma denemesi
                        ExecuteExternalCommand(command);
                    }
                    else
                    {
                        // Tanınmayan komut
                        WriteError($"'{cmd}' dahili veya harici bir komut veya çalıştırılabilir program olarak tanınmıyor.");
                    }
                }
                catch (Exception ex)
                {
                    WriteError($"Hata: {ex.Message}");
                }
            }
        }

        private void ProcessSingleCommandConsole(string command)
        {
            // Komutu parçalara ayır
            string[] parts = SplitCommandLine(command);

            if (parts.Length == 0)
                return;

            string cmd = parts[0].ToLower();
            string[] args = parts.Skip(1).ToArray();

            // İç komut mu kontrol et ve çalıştır
            if (internalCommands.ContainsKey(cmd))
            {
                try
                {
                    // Konsol modu için çıktı yakalama işlevi geçici olarak değiştiriliyor
                    bool success = internalCommands[cmd](args);
                    if (!success)
                    {
                        ConsoleWriteError($"'{cmd}' komutu hatalı parametre ile kullanıldı.");
                    }
                }
                catch (Exception ex)
                {
                    ConsoleWriteError($"Hata: {ex.Message}");
                }
            }
            else if (cmd == "sc")
            {
                // SC komutu için özel işleme
                ManageService(args);
            }
            else
            {
                try
                {
                    // Önce dosya olarak mevcut mu kontrol et
                    string exePath = FindExecutableInPath(cmd);
                    if (File.Exists(exePath))
                    {
                        // Harici komut çalıştırma denemesi
                        ExecuteExternalCommandConsole(command);
                    }
                    else
                    {
                        // Tanınmayan komut
                        ConsoleWriteError($"'{cmd}' dahili veya harici bir komut veya çalıştırılabilir program olarak tanınmıyor.");
                    }
                }
                catch (Exception ex)
                {
                    ConsoleWriteError($"Hata: {ex.Message}");
                }
            }
        }

        private string[] SplitCommandLine(string commandLine)
        {
            List<string> parts = new List<string>();
            bool inQuote = false;
            StringBuilder currentPart = new StringBuilder();

            for (int i = 0; i < commandLine.Length; i++)
            {
                char c = commandLine[i];

                if (c == '"')
                {
                    inQuote = !inQuote;
                    // Tırnaklı bölümleri de dahil et
                    currentPart.Append(c);
                }
                else if (c == ' ' && !inQuote)
                {
                    if (currentPart.Length > 0)
                    {
                        parts.Add(currentPart.ToString());
                        currentPart.Clear();
                    }
                }
                else
                {
                    currentPart.Append(c);
                }
            }

            if (currentPart.Length > 0)
            {
                parts.Add(currentPart.ToString());
            }

            return parts.ToArray();
        }

        #endregion

        #region Internal Commands

        private bool ShowHelp(string[] args)
        {
            string helpText = "\nCaYaDev CMD Emulator Yardım Menüsü\n" +
                             "============================\n\n" +
                             "Dosya ve Dizin Komutları:\n" +
                             "  dir/ls       - Dizin içeriğini listeler\n" +
                             "  cd           - Dizin değiştirir veya mevcut dizini gösterir\n" +
                             "  mkdir        - Yeni dizin oluşturur\n" +
                             "  rmdir        - Dizin siler\n" +
                             "  copy         - Dosya kopyalar\n" +
                             "  move         - Dosya taşır\n" +
                             "  del/delete   - Dosya siler\n" +
                             "  type         - Dosya içeriğini gösterir\n\n" +

                             "Sistem Komutları:\n" +
                             "  tasklist     - Çalışan süreçleri gösterir\n" +
                             "  taskkill     - Süreç sonlandırır\n" +
                             "  systeminfo   - Sistem bilgilerini gösterir\n" +
                             "  ipconfig     - Ağ yapılandırmasını gösterir\n" +
                             "  ping         - Ağ bağlantısını test eder\n" +
                             "  sc           - Servis yönetimi için kullanılır\n" +
                             "    sc query   - Servis bilgilerini gösterir\n" +
                             "    sc start   - Servisi başlatır\n" +
                             "    sc stop    - Servisi durdurur\n" +
                             "    sc create  - Yeni servis oluşturur\n" +
                             "    sc delete  - Servis siler\n\n" +

                             "Diğer Komutlar:\n" +
                             "  echo         - Ekrana metin yazdırır\n" +
                             "  cls/clear    - Ekranı temizler\n" +
                             "  start        - Program başlatır\n" +
                             "  help/?       - Bu yardım menüsünü gösterir\n\n" +

                             "Çoklu Komut Kullanımı:\n" +
                             "  komut1 && komut2    - Komutları sırayla çalıştırır\n" +
                             "  komut1 &&& komut2   - Komutları paralel çalıştırır\n" +

                             "Tarih: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\n" +
                             "Kullanıcı: " + Environment.UserName + "\n";

            if (isConsoleMode)
                ConsoleAppendOutput(helpText);
            else
                AppendOutput(helpText);

            return true;
        }

        private bool ClearScreen(string[] args)
        {
            if (!isConsoleMode)
                outputBox.Clear();
            else
                consoleOutput.Clear();

            return true;
        }

        private bool ListDirectory(string[] args)
        {
            string targetDir = currentDirectory;

            if (args.Length > 0)
            {
                targetDir = ResolvePath(args[0]);
                if (!Directory.Exists(targetDir))
                {
                    if (isConsoleMode)
                        ConsoleWriteError($"Dizin bulunamadı: {targetDir}");
                    else
                        WriteError($"Dizin bulunamadı: {targetDir}");
                    return false;
                }
            }

            try
            {
                string header = $"\n {Path.GetFullPath(targetDir)} dizininin içeriği\n\n";

                if (isConsoleMode)
                    ConsoleAppendOutput(header);
                else
                    AppendOutput(header);

                // Dizinleri listele
                foreach (string dir in Directory.GetDirectories(targetDir))
                {
                    DirectoryInfo info = new DirectoryInfo(dir);
                    string dirOutput = $"{info.LastWriteTime.ToString("dd.MM.yyyy  HH:mm")}    <DIR>          {Path.GetFileName(dir)}\n";

                    if (isConsoleMode)
                        ConsoleAppendOutput(dirOutput);
                    else
                        WriteColoredOutput(dirOutput, Color.Yellow);
                }

                // Dosyaları listele
                long totalSize = 0;
                int fileCount = 0;
                foreach (string file in Directory.GetFiles(targetDir))
                {
                    FileInfo info = new FileInfo(file);
                    totalSize += info.Length;
                    fileCount++;
                    string fileOutput = $"{info.LastWriteTime.ToString("dd.MM.yyyy  HH:mm")}         {info.Length,10:n0} {Path.GetFileName(file)}\n";

                    if (isConsoleMode)
                        ConsoleAppendOutput(fileOutput);
                    else
                        AppendOutput(fileOutput);
                }

                string summary = $"\n               {fileCount} Dosya  {totalSize:n0} bayt\n" +
                                $"               {Directory.GetDirectories(targetDir).Length} Dizin\n";

                if (isConsoleMode)
                    ConsoleAppendOutput(summary);
                else
                    AppendOutput(summary);

                return true;
            }
            catch (Exception ex)
            {
                if (isConsoleMode)
                    ConsoleWriteError($"Hata: {ex.Message}");
                else
                    WriteError($"Hata: {ex.Message}");
                return false;
            }
        }

        private bool ChangeDirectory(string[] args)
        {
            try
            {
                string newDir;
                if (args.Length == 0)
                {
                    // Sadece cd yazılmışsa, mevcut dizini göster
                    if (isConsoleMode)
                        ConsoleAppendOutput($"{currentDirectory}\n");
                    else
                        AppendOutput($"{currentDirectory}\n");
                    return true;
                }
                else
                {
                    // Dizin yolunu al
                    newDir = ResolvePath(args[0]);
                }

                // Dizinin var olup olmadığını kontrol et
                if (Directory.Exists(newDir))
                {
                    currentDirectory = newDir;
                    return true;
                }
                else
                {
                    if (isConsoleMode)
                        ConsoleWriteError($"Dizin bulunamadı: {newDir}");
                    else
                        WriteError($"Dizin bulunamadı: {newDir}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                if (isConsoleMode)
                    ConsoleWriteError($"Hata: {ex.Message}");
                else
                    WriteError($"Hata: {ex.Message}");
                return false;
            }
        }

        private bool EchoText(string[] args)
        {
            if (args.Length == 0)
            {
                if (isConsoleMode)
                    ConsoleAppendOutput("ECHO açık.\n");
                else
                    AppendOutput("ECHO açık.\n");
                return true;
            }

            string output = string.Join(" ", args) + "\n";

            if (isConsoleMode)
                ConsoleAppendOutput(output);
            else
                AppendOutput(output);

            return true;
        }

        private bool ListProcesses(string[] args)
        {
            try
            {
                Process[] processes = Process.GetProcesses();

                string header = "\nGörüntü Adı                     PID   Bellek Kullanımı\n" +
                               "========================= ======== ================\n";

                if (isConsoleMode)
                    ConsoleAppendOutput(header);
                else
                    AppendOutput(header);

                foreach (Process p in processes)
                {
                    try
                    {
                        string name = p.ProcessName;
                        if (name.Length > 25)
                            name = name.Substring(0, 22) + "...";
                        else
                            name = name.PadRight(25);

                        string pid = p.Id.ToString().PadRight(8);
                        string memory = (p.WorkingSet64 / 1024).ToString("N0") + " KB";
                        string processOutput = $"{name} {pid} {memory}\n";

                        if (isConsoleMode)
                            ConsoleAppendOutput(processOutput);
                        else
                            AppendOutput(processOutput);
                    }
                    catch
                    {
                        // Bazı süreçler erişim izni vermeyebilir, bunları atla
                    }
                }

                string summary = $"\nToplam {processes.Length} süreç.\n";

                if (isConsoleMode)
                    ConsoleAppendOutput(summary);
                else
                    AppendOutput(summary);

                return true;
            }
            catch (Exception ex)
            {
                if (isConsoleMode)
                    ConsoleWriteError($"Hata: {ex.Message}");
                else
                    WriteError($"Hata: {ex.Message}");
                return false;
            }
        }

        private bool KillProcess(string[] args)
        {
            if (args.Length == 0)
            {
                if (isConsoleMode)
                    ConsoleWriteError("TASKKILL [/F] [/IM process_name | /PID process_id]");
                else
                    WriteError("TASKKILL [/F] [/IM process_name | /PID process_id]");
                return false;
            }

            bool force = false;
            string targetName = "";
            int targetPid = -1;

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i].ToLower();

                if (arg == "/f")
                {
                    force = true;
                }
                else if (arg == "/im" && i + 1 < args.Length)
                {
                    targetName = args[++i].Replace("\"", "");
                }
                else if (arg == "/pid" && i + 1 < args.Length)
                {
                    int.TryParse(args[++i], out targetPid);
                }
            }

            try
            {
                if (!string.IsNullOrEmpty(targetName))
                {
                    // .exe uzantısını kaldır
                    if (targetName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        targetName = Path.GetFileNameWithoutExtension(targetName);
                    }

                    Process[] processes = Process.GetProcessesByName(targetName);

                    if (processes.Length == 0)
                    {
                        if (isConsoleMode)
                            ConsoleWriteError($"'{targetName}' isimli işlem bulunamadı.");
                        else
                            WriteError($"'{targetName}' isimli işlem bulunamadı.");
                        return false;
                    }

                    int terminatedCount = 0;
                    foreach (Process p in processes)
                    {
                        try
                        {
                            if (force)
                                p.Kill();
                            else
                                p.CloseMainWindow();

                            terminatedCount++;
                        }
                        catch
                        {
                            // İşlem sonlandırılamadı, devam et
                        }
                    }

                    string result = $"Başarı: {terminatedCount} süreç sonlandırıldı.\n";

                    if (isConsoleMode)
                        ConsoleAppendOutput(result);
                    else
                        AppendOutput(result);

                    return true;
                }
                else if (targetPid > 0)
                {
                    Process p = Process.GetProcessById(targetPid);

                    if (force)
                        p.Kill();
                    else
                        p.CloseMainWindow();

                    string result = $"Başarı: PID {targetPid} sonlandırıldı.\n";

                    if (isConsoleMode)
                        ConsoleAppendOutput(result);
                    else
                        AppendOutput(result);

                    return true;
                }
                else
                {
                    if (isConsoleMode)
                        ConsoleWriteError("Geçerli bir süreç belirtilmedi.");
                    else
                        WriteError("Geçerli bir süreç belirtilmedi.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                if (isConsoleMode)
                    ConsoleWriteError($"Hata: {ex.Message}");
                else
                    WriteError($"Hata: {ex.Message}");
                return false;
            }
        }

        private bool ManageService(string[] args)
        {
            if (args.Length < 1)
            {
                if (isConsoleMode)
                    ConsoleWriteError("SC [start|stop|query|create|delete] service_name");
                else
                    WriteError("SC [start|stop|query|create|delete] service_name");
                return false;
            }

            string operation = args[0].ToLower();

            if (operation == "query" && args.Length >= 2)
            {
                return QueryService(args[1]);
            }
            else if ((operation == "start" || operation == "stop") && args.Length >= 2)
            {
                return ControlService(operation, args[1]);
            }
            else if (operation == "create" && args.Length >= 3)
            {
                return CreateService(args);
            }
            else if (operation == "delete" && args.Length >= 2)
            {
                return DeleteService(args[1]);
            }
            else
            {
                if (isConsoleMode)
                    ConsoleWriteError("Desteklenen komutlar: sc [start|stop|query|create|delete] service_name");
                else
                    WriteError("Desteklenen komutlar: sc [start|stop|query|create|delete] service_name");
                return false;
            }
        }

        private bool QueryService(string serviceName)
        {
            try
            {
                // sc.exe ile servis bilgilerini sorgula
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "sc.exe";
                psi.Arguments = $"query {serviceName}";
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;

                Process process = Process.Start(psi);
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    if (isConsoleMode)
                        ConsoleAppendOutput("\n" + output + "\n");
                    else
                        AppendOutput("\n" + output + "\n");
                    return true;
                }
                else
                {
                    if (isConsoleMode)
                        ConsoleWriteError($"Servis sorgu hatası: {error}");
                    else
                        WriteError($"Servis sorgu hatası: {error}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                if (isConsoleMode)
                    ConsoleWriteError($"Hata: {ex.Message}");
                else
                    WriteError($"Hata: {ex.Message}");
                return false;
            }
        }

        private bool ControlService(string operation, string serviceName)
        {
            try
            {
                // sc.exe ile servisi başlat veya durdur
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "sc.exe";
                psi.Arguments = $"{operation} {serviceName}";
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;

                Process process = Process.Start(psi);
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    string message = operation == "start" ?
                        $"{serviceName} servisi başlatıldı.\n" :
                        $"{serviceName} servisi durduruldu.\n";

                    if (isConsoleMode)
                        ConsoleAppendOutput(message);
                    else
                        AppendOutput(message);

                    return true;
                }
                else
                {
                    if (isConsoleMode)
                        ConsoleWriteError($"Servis {operation} hatası: {error}");
                    else
                        WriteError($"Servis {operation} hatası: {error}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                if (isConsoleMode)
                    ConsoleWriteError($"Hata: {ex.Message}");
                else
                    WriteError($"Hata: {ex.Message}");
                return false;
            }
        }

        private bool CreateService(string[] args)
        {
            try
            {
                if (args.Length < 3)
                {
                    if (isConsoleMode)
                        ConsoleWriteError("Kullanım: sc create <servis_adı> binPath= <çalıştırılacak_dosya>");
                    else
                        WriteError("Kullanım: sc create <servis_adı> binPath= <çalıştırılacak_dosya>");
                    return false;
                }

                string serviceName = args[1];
                string binPath = "";

                // binPath parametresini bul
                for (int i = 2; i < args.Length; i++)
                {
                    if (args[i].StartsWith("binPath="))
                    {
                        binPath = args[i].Substring(8).Trim();
                        break;
                    }
                }

                if (string.IsNullOrEmpty(binPath))
                {
                    if (isConsoleMode)
                        ConsoleWriteError("binPath= parametresi gereklidir.");
                    else
                        WriteError("binPath= parametresi gereklidir.");
                    return false;
                }

                // sc.exe ile servisi oluştur
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "sc.exe";
                psi.Arguments = $"create {serviceName} binPath= \"{binPath}\"";
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;

                Process process = Process.Start(psi);
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    if (isConsoleMode)
                        ConsoleAppendOutput($"{serviceName} servisi başarıyla oluşturuldu.\n");
                    else
                        AppendOutput($"{serviceName} servisi başarıyla oluşturuldu.\n");
                    return true;
                }
                else
                {
                    if (isConsoleMode)
                        ConsoleWriteError($"Servis oluşturma hatası: {error}");
                    else
                        WriteError($"Servis oluşturma hatası: {error}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                if (isConsoleMode)
                    ConsoleWriteError($"Hata: {ex.Message}");
                else
                    WriteError($"Hata: {ex.Message}");
                return false;
            }
        }

        private bool DeleteService(string serviceName)
        {
            try
            {
                // sc.exe ile servisi sil
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "sc.exe";
                psi.Arguments = $"delete {serviceName}";
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;

                Process process = Process.Start(psi);
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    if (isConsoleMode)
                        ConsoleAppendOutput($"{serviceName} servisi başarıyla silindi.\n");
                    else
                        AppendOutput($"{serviceName} servisi başarıyla silindi.\n");
                    return true;
                }
                else
                {
                    if (isConsoleMode)
                        ConsoleWriteError($"Servis silme hatası: {error}");
                    else
                        WriteError($"Servis silme hatası: {error}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                if (isConsoleMode)
                    ConsoleWriteError($"Hata: {ex.Message}");
                else
                    WriteError($"Hata: {ex.Message}");
                return false;
            }
        }

        private bool ShowIpConfig(string[] args)
        {
            try
            {
                NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();

                foreach (NetworkInterface adapter in interfaces)
                {
                    IPInterfaceProperties properties = adapter.GetIPProperties();

                    string header = $"\nAdaptör Adı: {adapter.Name}\n" +
                                    $"   Açıklama: {adapter.Description}\n" +
                                    $"   Fiziksel Adres: {adapter.GetPhysicalAddress()}\n" +
                                    $"   Operasyonel Durum: {adapter.OperationalStatus}\n";

                    if (isConsoleMode)
                    {
                        ConsoleAppendOutput(header);
                    }
                    else
                    {
                        WriteColoredOutput($"Adaptör Adı: {adapter.Name}\n", Color.Yellow);
                        AppendOutput($"   Açıklama: {adapter.Description}\n");
                        AppendOutput($"   Fiziksel Adres: {adapter.GetPhysicalAddress()}\n");
                        AppendOutput($"   Operasyonel Durum: {adapter.OperationalStatus}\n");
                    }

                    // IPv4 adresleri
                    foreach (var ip in properties.UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        {
                            string ipInfo = $"   IPv4 Adresi: {ip.Address}\n" +
                                           $"   Alt Ağ Maskesi: {ip.IPv4Mask}\n";

                            if (isConsoleMode)
                                ConsoleAppendOutput(ipInfo);
                            else
                                AppendOutput(ipInfo);
                        }
                    }

                    // DNS sunucuları
                    if (properties.DnsAddresses.Count > 0)
                    {
                        if (isConsoleMode)
                            ConsoleAppendOutput("   DNS Sunucuları:\n");
                        else
                            AppendOutput("   DNS Sunucuları:\n");

                        foreach (var dns in properties.DnsAddresses)
                        {
                            if (isConsoleMode)
                                ConsoleAppendOutput($"      {dns}\n");
                            else
                                AppendOutput($"      {dns}\n");
                        }
                    }

                    // Ağ geçidi
                    if (properties.GatewayAddresses.Count > 0)
                    {
                        if (isConsoleMode)
                            ConsoleAppendOutput("   Ağ Geçitleri:\n");
                        else
                            AppendOutput("   Ağ Geçitleri:\n");

                        foreach (var gateway in properties.GatewayAddresses)
                        {
                            if (isConsoleMode)
                                ConsoleAppendOutput($"      {gateway.Address}\n");
                            else
                                AppendOutput($"      {gateway.Address}\n");
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                if (isConsoleMode)
                    ConsoleWriteError($"Hata: {ex.Message}");
                else
                    WriteError($"Hata: {ex.Message}");
                return false;
            }
        }

        private bool PingHost(string[] args)
        {
            if (args.Length < 1)
            {
                if (isConsoleMode)
                    ConsoleWriteError("Kullanım: ping <host>");
                else
                    WriteError("Kullanım: ping <host>");
                return false;
            }

            try
            {
                string host = args[0];
                Ping ping = new Ping();

                string introText = $"\n{host} adresi ping'leniyor...\n\n";

                if (isConsoleMode)
                    ConsoleAppendOutput(introText);
                else
                    AppendOutput(introText);

                for (int i = 0; i < 4; i++)
                {
                    PingReply reply = ping.Send(host);
                    string replyText;

                    if (reply.Status == IPStatus.Success)
                    {
                        replyText = $"Yanıt: bayt={reply.Buffer.Length} süre={reply.RoundtripTime}ms TTL={reply.Options?.Ttl ?? 64}\n";
                    }
                    else
                    {
                        replyText = $"Yanıt: {reply.Status}\n";
                    }

                    if (isConsoleMode)
                        ConsoleAppendOutput(replyText);
                    else
                        AppendOutput(replyText);

                    Thread.Sleep(1000);
                }

                return true;
            }
            catch (Exception ex)
            {
                if (isConsoleMode)
                    ConsoleWriteError($"Hata: {ex.Message}");
                else
                    WriteError($"Hata: {ex.Message}");
                return false;
            }
        }

        private bool ShowSystemInfo(string[] args)
        {
            try
            {
                string sysInfo = "\nSistem Bilgisi:\n\n" +
                                $"Bilgisayar Adı: {Environment.MachineName}\n" +
                                $"Kullanıcı Adı: {Environment.UserName}\n" +
                                $"İşletim Sistemi: {Environment.OSVersion}\n" +
                                $"İşletim Sistemi Mimarisi: {(Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit")}\n" +
                                $"Sistem Dizini: {Environment.SystemDirectory}\n" +
                                $"İşlemci Sayısı: {Environment.ProcessorCount}\n" +
                                $"Toplam Fiziksel Bellek: {GetTotalPhysicalMemory()} MB\n" +
                                $"Tarih/Saat: {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}\n";

                if (isConsoleMode)
                    ConsoleAppendOutput(sysInfo);
                else
                    AppendOutput(sysInfo);

                return true;
            }
            catch (Exception ex)
            {
                if (isConsoleMode)
                    ConsoleWriteError($"Hata: {ex.Message}");
                else
                    WriteError($"Hata: {ex.Message}");
                return false;
            }
        }

        private bool TypeFile(string[] args)
        {
            if (args.Length < 1)
            {
                if (isConsoleMode)
                    ConsoleWriteError("Kullanım: type <dosya_yolu>");
                else
                    WriteError("Kullanım: type <dosya_yolu>");
                return false;
            }

            try
            {
                string filePath = ResolvePath(args[0]);

                if (!File.Exists(filePath))
                {
                    if (isConsoleMode)
                        ConsoleWriteError($"Dosya bulunamadı: {filePath}");
                    else
                        WriteError($"Dosya bulunamadı: {filePath}");
                    return false;
                }

                string content = File.ReadAllText(filePath);

                if (isConsoleMode)
                    ConsoleAppendOutput($"\n{content}\n");
                else
                    AppendOutput($"\n{content}\n");

                return true;
            }
            catch (Exception ex)
            {
                if (isConsoleMode)
                    ConsoleWriteError($"Hata: {ex.Message}");
                else
                    WriteError($"Hata: {ex.Message}");
                return false;
            }
        }

        private bool CopyFile(string[] args)
        {
            if (args.Length < 2)
            {
                if (isConsoleMode)
                    ConsoleWriteError("Kullanım: copy <kaynak> <hedef>");
                else
                    WriteError("Kullanım: copy <kaynak> <hedef>");
                return false;
            }

            try
            {
                string source = ResolvePath(args[0]);
                string destination = ResolvePath(args[1]);

                if (!File.Exists(source))
                {
                    if (isConsoleMode)
                        ConsoleWriteError($"Kaynak dosya bulunamadı: {source}");
                    else
                        WriteError($"Kaynak dosya bulunamadı: {source}");
                    return false;
                }

                File.Copy(source, destination, true);

                string result = $"        1 dosya kopyalandı.\n";

                if (isConsoleMode)
                    ConsoleAppendOutput(result);
                else
                    AppendOutput(result);

                return true;
            }
            catch (Exception ex)
            {
                if (isConsoleMode)
                    ConsoleWriteError($"Hata: {ex.Message}");
                else
                    WriteError($"Hata: {ex.Message}");
                return false;
            }
        }

        private bool MoveFile(string[] args)
        {
            if (args.Length < 2)
            {
                if (isConsoleMode)
                    ConsoleWriteError("Kullanım: move <kaynak> <hedef>");
                else
                    WriteError("Kullanım: move <kaynak> <hedef>");
                return false;
            }

            try
            {
                string source = ResolvePath(args[0]);
                string destination = ResolvePath(args[1]);

                if (!File.Exists(source))
                {
                    if (isConsoleMode)
                        ConsoleWriteError($"Kaynak dosya bulunamadı: {source}");
                    else
                        WriteError($"Kaynak dosya bulunamadı: {source}");
                    return false;
                }

                File.Move(source, destination);

                string result = $"        1 dosya taşındı.\n";

                if (isConsoleMode)
                    ConsoleAppendOutput(result);
                else
                    AppendOutput(result);

                return true;
            }
            catch (Exception ex)
            {
                if (isConsoleMode)
                    ConsoleWriteError($"Hata: {ex.Message}");
                else
                    WriteError($"Hata: {ex.Message}");
                return false;
            }
        }

        private bool DeleteFile(string[] args)
        {
            if (args.Length < 1)
            {
                if (isConsoleMode)
                    ConsoleWriteError("Kullanım: delete/del <dosya>");
                else
                    WriteError("Kullanım: delete/del <dosya>");
                return false;
            }

            try
            {
                string filePath = ResolvePath(args[0]);

                if (!File.Exists(filePath))
                {
                    if (isConsoleMode)
                        ConsoleWriteError($"Dosya bulunamadı: {filePath}");
                    else
                        WriteError($"Dosya bulunamadı: {filePath}");
                    return false;
                }

                File.Delete(filePath);

                string result = $"{filePath} silindi.\n";

                if (isConsoleMode)
                    ConsoleAppendOutput(result);
                else
                    AppendOutput(result);

                return true;
            }
            catch (Exception ex)
            {
                if (isConsoleMode)
                    ConsoleWriteError($"Hata: {ex.Message}");
                else
                    WriteError($"Hata: {ex.Message}");
                return false;
            }
        }

        private bool CreateDirectory(string[] args)
        {
            if (args.Length < 1)
            {
                if (isConsoleMode)
                    ConsoleWriteError("Kullanım: mkdir <dizin>");
                else
                    WriteError("Kullanım: mkdir <dizin>");
                return false;
            }

            try
            {
                string dirPath = ResolvePath(args[0]);

                if (Directory.Exists(dirPath))
                {
                    if (isConsoleMode)
                        ConsoleWriteError($"Dizin zaten var: {dirPath}");
                    else
                        WriteError($"Dizin zaten var: {dirPath}");
                    return false;
                }

                Directory.CreateDirectory(dirPath);

                string result = $"{dirPath} dizini oluşturuldu.\n";

                if (isConsoleMode)
                    ConsoleAppendOutput(result);
                else
                    AppendOutput(result);

                return true;
            }
            catch (Exception ex)
            {
                if (isConsoleMode)
                    ConsoleWriteError($"Hata: {ex.Message}");
                else
                    WriteError($"Hata: {ex.Message}");
                return false;
            }
        }

        private bool RemoveDirectory(string[] args)
        {
            if (args.Length < 1)
            {
                if (isConsoleMode)
                    ConsoleWriteError("Kullanım: rmdir <dizin>");
                else
                    WriteError("Kullanım: rmdir <dizin>");
                return false;
            }

            try
            {
                string dirPath = ResolvePath(args[0]);

                if (!Directory.Exists(dirPath))
                {
                    if (isConsoleMode)
                        ConsoleWriteError($"Dizin bulunamadı: {dirPath}");
                    else
                        WriteError($"Dizin bulunamadı: {dirPath}");
                    return false;
                }

                bool recursive = false;
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i].ToLower() == "/s")
                    {
                        recursive = true;
                        break;
                    }
                }

                Directory.Delete(dirPath, recursive);

                string result = $"{dirPath} dizini silindi.\n";

                if (isConsoleMode)
                    ConsoleAppendOutput(result);
                else
                    AppendOutput(result);

                return true;
            }
            catch (Exception ex)
            {
                if (isConsoleMode)
                    ConsoleWriteError($"Hata: {ex.Message}");
                else
                    WriteError($"Hata: {ex.Message}");
                return false;
            }
        }

        private bool StartProcess(string[] args)
        {
            if (args.Length < 1)
            {
                if (isConsoleMode)
                    ConsoleWriteError("Kullanım: start <program> [parametreler]");
                else
                    WriteError("Kullanım: start <program> [parametreler]");
                return false;
            }

            try
            {
                string program = args[0];
                string arguments = args.Length > 1 ? string.Join(" ", args.Skip(1)) : "";

                // Tam yol belirtilmemişse PATH içinde ara
                if (!File.Exists(program) && !program.Contains("\\") && !program.Contains("/"))
                {
                    program = FindExecutableInPath(program);
                }

                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = program;
                psi.Arguments = arguments;
                psi.UseShellExecute = true;
                psi.WorkingDirectory = currentDirectory;

                Process.Start(psi);

                string result = $"{program} başlatıldı.\n";

                if (isConsoleMode)
                    ConsoleAppendOutput(result);
                else
                    AppendOutput(result);

                return true;
            }
            catch (Exception ex)
            {
                if (isConsoleMode)
                    ConsoleWriteError($"Hata: {ex.Message}");
                else
                    WriteError($"Hata: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Helper Methods

        private void WriteError(string message)
        {
            if (isConsoleMode)
            {
                ConsoleWriteError(message);
                return;
            }

            try
            {
                if (this.InvokeRequired)
                {
                    this.Invoke(new Action(() =>
                    {
                        lock (outputLock)
                        {
                            outputBox.SelectionStart = outputBox.TextLength;
                            outputBox.SelectionLength = 0;
                            outputBox.SelectionColor = Color.Red;
                            outputBox.AppendText(message + "\n");
                            outputBox.SelectionColor = outputBox.ForeColor;
                        }
                    }));
                }
                else
                {
                    lock (outputLock)
                    {
                        outputBox.SelectionStart = outputBox.TextLength;
                        outputBox.SelectionLength = 0;
                        outputBox.SelectionColor = Color.Red;
                        outputBox.AppendText(message + "\n");
                        outputBox.SelectionColor = outputBox.ForeColor;
                    }
                }
            }
            catch (InvalidOperationException)
            {
                // Form kapatılmışsa veya thread sonlanmışsa hata oluşabilir
                ConsoleWriteError(message);
            }
        }

        private void WriteColoredOutput(string text, Color color)
        {
            if (isConsoleMode)
            {
                ConsoleAppendOutput(text);
                return;
            }

            try
            {
                if (this.InvokeRequired)
                {
                    this.Invoke(new Action(() =>
                    {
                        lock (outputLock)
                        {
                            outputBox.SelectionStart = outputBox.TextLength;
                            outputBox.SelectionLength = 0;
                            outputBox.SelectionColor = color;
                            outputBox.AppendText(text);
                            outputBox.SelectionColor = outputBox.ForeColor;
                        }
                    }));
                }
                else
                {
                    lock (outputLock)
                    {
                        outputBox.SelectionStart = outputBox.TextLength;
                        outputBox.SelectionLength = 0;
                        outputBox.SelectionColor = color;
                        outputBox.AppendText(text);
                        outputBox.SelectionColor = outputBox.ForeColor;
                    }
                }
            }
            catch (InvalidOperationException)
            {
                // Form kapatılmışsa veya thread sonlanmışsa
                ConsoleAppendOutput(text);
            }
        }

        private void AppendOutput(string message)
        {
            if (isConsoleMode)
            {
                ConsoleAppendOutput(message);
                return;
            }

            try
            {
                if (this.InvokeRequired)
                {
                    this.Invoke(new Action(() =>
                    {
                        lock (outputLock)
                        {
                            outputBox.AppendText(message);
                            // Otomatik kaydırma
                            outputBox.SelectionStart = outputBox.Text.Length;
                            outputBox.ScrollToCaret();
                        }
                    }));
                }
                else
                {
                    lock (outputLock)
                    {
                        outputBox.AppendText(message);
                        // Otomatik kaydırma
                        outputBox.SelectionStart = outputBox.Text.Length;
                        outputBox.ScrollToCaret();
                    }
                }
            }
            catch (InvalidOperationException)
            {
                // Form kapatılmışsa veya thread sonlanmışsa
                ConsoleAppendOutput(message);
            }
        }

        private void ConsoleWriteError(string message)
        {
            lock (outputLock)
            {
                consoleOutput.AppendLine("HATA: " + message);
            }
        }

        private void ConsoleAppendOutput(string message)
        {
            lock (outputLock)
            {
                consoleOutput.Append(message);
            }
        }

        private string ResolvePath(string path)
        {
            if (Path.IsPathRooted(path))
                return path;

            return Path.GetFullPath(Path.Combine(currentDirectory, path));
        }

        private void ExecuteExternalCommand(string command)
        {
            try
            {
                string[] parts = SplitCommandLine(command);
                string executable = parts[0];
                string arguments = string.Join(" ", parts.Skip(1));

                // Tam yol belirtilmemişse PATH içinde ara
                if (!File.Exists(executable) && !executable.Contains("\\") && !executable.Contains("/"))
                {
                    executable = FindExecutableInPath(executable);
                }

                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = executable;
                psi.Arguments = arguments;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.WorkingDirectory = currentDirectory;

                using (Process process = new Process())
                {
                    process.StartInfo = psi;

                    try
                    {
                        process.Start();

                        // Eşzamansız okuma için olay işleyicileri
                        process.OutputDataReceived += (sender, e) =>
                        {
                            if (e.Data != null)
                                AppendOutput(e.Data + "\n");
                        };

                        process.ErrorDataReceived += (sender, e) =>
                        {
                            if (e.Data != null)
                                WriteError(e.Data);
                        };

                        process.BeginOutputReadLine();
                        process.BeginErrorReadLine();
                        process.WaitForExit();
                    }
                    catch (Exception ex)
                    {
                        WriteError($"Komut çalıştırılamadı: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                WriteError($"Hata: {ex.Message}");
            }
        }

        private void ExecuteExternalCommandConsole(string command)
        {
            try
            {
                string[] parts = SplitCommandLine(command);
                string executable = parts[0];
                string arguments = string.Join(" ", parts.Skip(1));

                // Tam yol belirtilmemişse PATH içinde ara
                if (!File.Exists(executable) && !executable.Contains("\\") && !executable.Contains("/"))
                {
                    executable = FindExecutableInPath(executable);
                }

                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = executable;
                psi.Arguments = arguments;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.WorkingDirectory = currentDirectory;

                using (Process process = new Process())
                {
                    process.StartInfo = psi;

                    try
                    {
                        process.Start();
                        string output = process.StandardOutput.ReadToEnd();
                        string error = process.StandardError.ReadToEnd();
                        process.WaitForExit();

                        ConsoleAppendOutput(output);

                        if (!string.IsNullOrEmpty(error))
                        {
                            ConsoleWriteError(error);
                        }
                    }
                    catch (Exception ex)
                    {
                        ConsoleWriteError($"Komut çalıştırılamadı: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                ConsoleWriteError($"Hata: {ex.Message}");
            }
        }

        private string FindExecutableInPath(string exeName)
        {
            // .exe uzantısı yoksa ekle
            if (!exeName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                exeName += ".exe";

            // Çevre değişkenlerinden PATH'i al
            string pathVar = Environment.GetEnvironmentVariable("PATH");

            if (pathVar != null)
            {
                string[] paths = pathVar.Split(';');

                foreach (string path in paths)
                {
                    if (string.IsNullOrEmpty(path))
                        continue;

                    try
                    {
                        string fullPath = Path.Combine(path, exeName);
                        if (File.Exists(fullPath))
                            return fullPath;
                    }
                    catch
                    {
                        // Geçersiz PATH girişleri olabilir, atla
                    }
                }
            }

            return exeName; // Bulunamadıysa orijinal adı dön
        }

        private long GetTotalPhysicalMemory()
        {
            try
            {
                // Windows Management API ile bellek bilgisini al (en güvenilir yöntem)
                using (var searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        return Convert.ToInt64(obj["TotalPhysicalMemory"]) / (1024 * 1024);
                    }
                }

                // Eğer yukarıdaki yöntem başarısız olursa alternatif yöntem dene
                return GC.GetTotalMemory(false) / (1024 * 1024);
            }
            catch
            {
                // Herhangi bir hata durumunda tahmini değer döndür
                return 4096; // 4 GB varsayılan
            }
        }

        #endregion
    }
}
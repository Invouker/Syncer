using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using Microsoft.Win32;
using Renci.SshNet;
using MessageBox = System.Windows.MessageBox;

namespace SyncData
{
    public partial class MainWindow {
        private string host;
        private string login;
        private string password;
        private string remotePath;
        private string localPath;

        private FileSystemWatcher watcher;
        private readonly string configFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SyncData/SyncAppSettings.data");

        public MainWindow()
        {
            InitializeComponent();
            LoadSettings();
            InitializeFileSystemWatcher();
            
            Closing += MainWindow_Closing;
        }
        
        private void MainWindow_Closing(object sender, CancelEventArgs e) {
            // Prevent the application from closing
            e.Cancel = true;
            // Minimize the window instead
            WindowState = WindowState.Minimized;
        }
        
        private void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            host = HostTextBox.Text;
            login = LoginTextBox.Text;
            password = PasswordBox.Password;
            remotePath = RemotePathTextBox.Text;
            localPath = LocalPathTextBox.Text;

            SaveSettings();
            StatusText.Text = "Settings has been saved.";
        }

        private void StartSync_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(login) || string.IsNullOrEmpty(password)
                || string.IsNullOrEmpty(remotePath) || string.IsNullOrEmpty(localPath))
            {
                MessageBox.Show("Please enter all required information.");
                return;
            }

            StatusText.Text = "Sync in progress...";
            // Spustiť synchronizáciu na pozadí
            Task.Run(() => PerformSync());
        }

        private void InitializeFileSystemWatcher()
        {
            string localFolderPath = LocalPathTextBox.Text;

            // Create a new FileSystemWatcher
            watcher = new FileSystemWatcher(localFolderPath);
            
            // Subscribe to events
            watcher.Created += OnFileChanged;
            watcher.Changed += OnFileChanged;
            watcher.Deleted += OnFileChanged;
            watcher.Renamed += OnFileChanged;

            // Begin watching
            watcher.EnableRaisingEvents = true;
        }
        
        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            // Perform synchronization when a file changes
            PerformSync();
        }
        
        private void PerformSync()
        {
            using (var client = new SftpClient(host, login, password)) {
                try {
                    client.Connect();
                    // Upload file to VPS
                    UploadFolder(client, localPath, remotePath);
                    Dispatcher.Invoke(() => StatusText.Text = "Sync completed successfully.");
                    client.Disconnect();
                }catch(Exception ex) {
                    client.Disconnect();
                    Dispatcher.Invoke(() => StatusText.Text = $"Error while sync data: {ex.Message}");
                }
            }
        }
        
        private void UploadFolder(SftpClient client, string localFolderPath, string vpsFolderPath)
        {
            // Get a list of files and subdirectories in the local folder
            string[] files = Directory.GetFiles(localFolderPath);
            string[] subDirectories = Directory.GetDirectories(localFolderPath);

            // Upload files in the current folder
            foreach (string filePath in files)
            {
                string fileName = Path.GetFileName(filePath);

                using (var fileStream = new FileStream(filePath, FileMode.Open))
                {
                    client.UploadFile(fileStream, $"{vpsFolderPath}{fileName}");
                }
            }

            // Recursively upload files in subdirectories
            foreach (string subDirectory in subDirectories)
            {
                string subDirectoryName = Path.GetFileName(subDirectory);
                string subDirectoryVpsPath = $"{vpsFolderPath}{subDirectoryName}/";

                // Create the subdirectory on the server if it doesn't exist
                if (!client.Exists(subDirectoryVpsPath))
                {
                    client.CreateDirectory(subDirectoryVpsPath);
                }

                // Recursively upload files in the subdirectory
                UploadFolder(client, subDirectory, subDirectoryVpsPath);
            }
        }
        
        private void SaveSettings()
        {
            string dataToSave = $"{host};{login};{password};{remotePath};{localPath}";
            // Convert the string to bytes
            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(dataToSave);

            // Use DPAPI to protect the data
            byte[] encryptedData = ProtectedData.Protect(buffer, null, DataProtectionScope.CurrentUser);

            // Save settings to the configuration file
            File.WriteAllBytes(configFilePath, encryptedData);
        }

        private void LoadSettings()
        {
            // Load settings from the configuration file
            if (File.Exists(configFilePath))
            {
                // Use DPAPI to unprotect the data
                byte[] decryptedData = ProtectedData.Unprotect(File.ReadAllBytes(configFilePath), null, DataProtectionScope.CurrentUser);

                // Convert the decrypted bytes back to a string
                string result = System.Text.Encoding.UTF8.GetString(decryptedData);
                
                string[] settings = result.Split(';');

                if (settings.Length == 5)
                {
                    host = settings[0];
                    login = settings[1];
                    password = settings[2];
                    remotePath = settings[3];
                    localPath = settings[4];

                    // Update UI with loaded settings
                    HostTextBox.Text = host;
                    LoginTextBox.Text = login;
                    PasswordBox.Password = password;
                    RemotePathTextBox.Text = remotePath;
                    LocalPathTextBox.Text = localPath;
                }
            }
        }
        
        private void SetAutoStart() {
            string appName = "Syncer";

            // Specify your application's executable path
            //string appPath = @"C:\Path\To\Your\Application.exe";
            string appPath = @"E:\Github\Sync\SyncData\bin\Debug2\SyncData.exe";

            RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
            // Check if the auto-start is currently enabled
            bool isAutoStartEnabled = registryKey.GetValue(appName) != null;
            
            if (!isAutoStartEnabled)
            {
                // Enable auto-start
                registryKey.SetValue(appName, appPath);
                StatusText.Text = "Auto app start enabled.";
            }else {
                // Disable auto-start
                registryKey.DeleteValue(appName, false);
                StatusText.Text = "Auto app start disabled.";
            }
        }

        private void BrowseLocalPath_Click(object sender, RoutedEventArgs e)
        {
            using (var folderBrowserDialog = new FolderBrowserDialog())
            {
                DialogResult result = folderBrowserDialog.ShowDialog();

                if (result == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(folderBrowserDialog.SelectedPath))
                {
                    LocalPathTextBox.Text = folderBrowserDialog.SelectedPath;
                }
            }
        }

        private void ToggleAutoStart_Click(object sender, RoutedEventArgs e)
        {
            // Toggle the auto-start feature
            SetAutoStart();
        }

    }
}
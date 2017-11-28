using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;

//--< using >-- 
using System.Windows.Threading;
using System.Windows.Forms;
using System.Diagnostics;
using System.ComponentModel;
using nQuant;
using System.Drawing;
using System.Drawing.Imaging;
//--</ using >-- 

namespace tiny_image
{
    /// <summary>
    /// MainWindow.xaml 的互動邏輯
    /// </summary>
    public partial class MainWindow : Window
    {
        private List<ImageFile> imgFiles;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
        }

        private void BtnSelect_Folder_Click(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog folderDialog = new FolderBrowserDialog
            {
                ShowNewFolderButton = false,
                SelectedPath = AppDomain.CurrentDomain.BaseDirectory
            };
            DialogResult result = folderDialog.ShowDialog();

            imgFiles = new List<ImageFile>();

            if (result == System.Windows.Forms.DialogResult.OK)
            {
                String sPath = folderDialog.SelectedPath;

                DirectoryInfo folder = new DirectoryInfo(sPath);
                if (folder.Exists)
                {
                    string[] extensions = { "jpg", "png" };

                    foreach (FileInfo fileInfo in folder.GetFiles("*.jpg", SearchOption.AllDirectories))
                    {
                        imgFiles.Add(new ImageFile() { Path = fileInfo.FullName, Ext = fileInfo.Extension, OldSize = fileInfo.Length, Status = "waiting" });
                    }

                    foreach (FileInfo fileInfo in folder.GetFiles("*.png", SearchOption.AllDirectories))
                    {
                        imgFiles.Add(new ImageFile() { Path = fileInfo.FullName, Ext = fileInfo.Extension, OldSize = fileInfo.Length, Status = "waiting" });
                    }
                    imgList.ItemsSource = imgFiles;
                }
            }
            if(imgFiles.Count > 0)
            {
                Btn_Cmpression.IsEnabled = true;
            }
            else
            {
                Btn_Cmpression.IsEnabled = false;
            }
        }

        private void Btn_Cmpression_Click(object sender, RoutedEventArgs e)
        {
            CmpressionProgressBar.Minimum = 0;
            CmpressionProgressBar.Maximum = imgFiles.Count;

            BackgroundWorker worker = new BackgroundWorker
            {
                WorkerReportsProgress = true
            };
            worker.DoWork += Worker_DoWork;
            worker.ProgressChanged += Worker_ProgressChanged;
            worker.RunWorkerCompleted += Worker_RunWorkerCompleted;

            worker.RunWorkerAsync();
        }

        private void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            int i = 0;
            foreach (ImageFile imageFile in imgFiles)
            {
                i++;

                switch (imageFile.Ext.ToLower())
                {
                    case ".jpg":
                    case ".jpeg":
                        OptimizeJPEG(imageFile);
                        break;
                    case ".png":
                        OptimizePNG(imageFile);
                        break;
                }
                (sender as BackgroundWorker).ReportProgress(i);

                Action methodDelegate = delegate ()
                {
                    imgList.ItemsSource = null;
                    imgList.ItemsSource = imgFiles;
                };
                imgList.Dispatcher.BeginInvoke(methodDelegate);
                System.Threading.Thread.Sleep(100);
            }
        }

        private void Worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if ((e.Cancelled == true))
            {
                System.Windows.MessageBox.Show("取消!");
            }
            else if (!(e.Error == null))
            {
                System.Windows.MessageBox.Show("Error: " + e.Error.Message);
            }
            else
            {
                imgList.ItemsSource = null;
                imgList.ItemsSource = imgFiles;
                System.Windows.MessageBox.Show("完成!");
            }
        }

        private void Worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            CmpressionProgressBar.Value = e.ProgressPercentage;
        }

        private void OptimizeJPEG(ImageFile imageFile)
        {
            string pathToExe = Directory.GetCurrentDirectory() + "\\jpegtran.exe";

            var proc = new Process
            {
                StartInfo =
                {
                    Arguments = "-copy none -optimize -progressive -outfile \""+imageFile.Path+"\" \""+imageFile.Path+"\"",
                    FileName = pathToExe,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                }
            };

            Process jpegTranProcess = proc;

            jpegTranProcess.Start();
            jpegTranProcess.WaitForExit();

            imageFile.Status = "OK";
            imageFile.NewSize = new FileInfo(imageFile.Path).Length;
            imageFile.Rate = imageFile.OldSize - imageFile.NewSize;
            imageFile.Rate /= imageFile.OldSize;
            imageFile.Rate *= 100;
        }

        private void OptimizePNG(ImageFile imageFile)
        {
            try
            {
                string tempFile = Path.GetDirectoryName(imageFile.Path) + @"\temp-" + Path.GetFileName(imageFile.Path);
                int alphaTransparency = 10;
                int alphaFader = 70;
                var quantizer = new WuQuantizer();
                using (var bitmap = new Bitmap(imageFile.Path))
                {
                    using (var quantized = quantizer.QuantizeImage(bitmap, alphaTransparency, alphaFader))
                    {
                        quantized.Save(tempFile, ImageFormat.Png);
                    }
                }
                File.Delete(imageFile.Path);
                File.Move(tempFile, imageFile.Path);

                imageFile.Status = "OK";
                imageFile.NewSize = new FileInfo(imageFile.Path).Length;
                imageFile.Rate = imageFile.OldSize - imageFile.NewSize;
                imageFile.Rate /= imageFile.OldSize;
                imageFile.Rate *= 100;
            } catch (Exception e)
            {
                imageFile.Status = e.Message;
            }
        }
    }

    public class ImageFile
    {
        public string Ext { get; set; }
        public string Path { get; set; }
        public double? Rate { get; set; }
        public long? OldSize { get; set; }
        public long? NewSize { get; set; }
        public string Status { get; set; }
    }
}

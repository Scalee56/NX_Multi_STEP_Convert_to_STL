// Designcenter 2512
// Journal Enhanced with Preview, Timer and Loading Spinner
// Created by AndreaScalenghe on Tue Sep 15 10:59:29 2026 ora legale Europa occidentale

using System;
using System.Windows.Forms;
using System.Drawing;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using NXOpen;
using NXOpen.Gateway;

public class NXJournalEnhanced
{
    private Form mainForm;
    private PictureBox previewBox;
    private Label timerLabel;
    private Label statusLabel;
    private PictureBox loadingSpinner;
    private TextBox searchBox;
    private Button exportButton;
    private Button fitButton;
    private Stopwatch exportTimer;
    private int rotationAngle = 0;
    private Timer animationTimer;
    private bool isExporting = false;

    public static void Main(string[] args)
    {
        NXJournalEnhanced journal = new NXJournalEnhanced();
        journal.ShowUI();
    }

    private void ShowUI()
    {
        mainForm = new Form();
        mainForm.Text = "NX Export Manager - Preview & Export";
        mainForm.Size = new Size(1000, 700);
        mainForm.StartPosition = FormStartPosition.CenterScreen;
        mainForm.BackColor = Color.FromArgb(240, 240, 240);
        mainForm.FormClosing += (s, e) => e.Cancel = true; // Prevent window from closing

        // Search Bar
        searchBox = new TextBox();
        searchBox.Location = new Point(10, 10);
        searchBox.Size = new Size(450, 30);
        searchBox.Font = new Font("Segoe UI", 10);
        searchBox.PlaceholderText = "Cerca file o percorso esportazione...";
        mainForm.Controls.Add(searchBox);

        // Fit Button
        fitButton = new Button();
        fitButton.Location = new Point(470, 10);
        fitButton.Size = new Size(100, 30);
        fitButton.Text = "Fit View";
        fitButton.Click += FitViewClick;
        mainForm.Controls.Add(fitButton);

        // Export Button
        exportButton = new Button();
        exportButton.Location = new Point(580, 10);
        exportButton.Size = new Size(120, 30);
        exportButton.Text = "Esporta";
        exportButton.BackColor = Color.FromArgb(76, 175, 80);
        exportButton.ForeColor = Color.White;
        exportButton.Click += ExportClick;
        mainForm.Controls.Add(exportButton);

        // Preview Box
        previewBox = new PictureBox();
        previewBox.Location = new Point(10, 50);
        previewBox.Size = new Size(650, 500);
        previewBox.BorderStyle = BorderStyle.Fixed3D;
        previewBox.BackColor = Color.Black;
        previewBox.SizeMode = PictureBoxSizeMode.Zoom;
        mainForm.Controls.Add(previewBox);

        // Loading Spinner
        loadingSpinner = new PictureBox();
        loadingSpinner.Location = new Point(670, 50);
        loadingSpinner.Size = new Size(300, 300);
        loadingSpinner.BackColor = Color.White;
        loadingSpinner.BorderStyle = BorderStyle.Fixed3D;
        loadingSpinner.Paint += DrawLoadingSpinner;
        loadingSpinner.Visible = false;
        mainForm.Controls.Add(loadingSpinner);

        // Status Label
        statusLabel = new Label();
        statusLabel.Location = new Point(10, 560);
        statusLabel.Size = new Size(650, 30);
        statusLabel.Font = new Font("Segoe UI", 9);
        statusLabel.Text = "Pronto per l'esportazione";
        statusLabel.BackColor = Color.White;
        statusLabel.BorderStyle = BorderStyle.Fixed3D;
        statusLabel.Padding = new Padding(5);
        mainForm.Controls.Add(statusLabel);

        // Timer Label
        timerLabel = new Label();
        timerLabel.Location = new Point(670, 360);
        timerLabel.Size = new Size(300, 80);
        timerLabel.Font = new Font("Segoe UI", 24, FontStyle.Bold);
        timerLabel.Text = "00:00:00";
        timerLabel.TextAlign = ContentAlignment.MiddleCenter;
        timerLabel.BackColor = Color.White;
        timerLabel.BorderStyle = BorderStyle.Fixed3D;
        timerLabel.Visible = false;
        mainForm.Controls.Add(timerLabel);

        // Animation Timer
        animationTimer = new Timer();
        animationTimer.Interval = 50;
        animationTimer.Tick += AnimationTick;

        // Load preview
        LoadPreview();

        mainForm.ShowDialog();
    }

    private void LoadPreview()
    {
        try
        {
            Session theSession = Session.GetSession();
            Part workPart = theSession.Parts.Work;

            if (workPart != null)
            {
                statusLabel.Text = "Preview della parte caricata: " + workPart.FullPath;
                previewBox.BackColor = Color.DarkGray;
                previewBox.Text = "Preview: " + workPart.Name;
            }
        }
        catch (Exception ex)
        {
            statusLabel.Text = "Errore nel caricamento preview: " + ex.Message;
        }
    }

    private void FitViewClick(object sender, EventArgs e)
    {
        try
        {
            Session theSession = Session.GetSession();
            Part workPart = theSession.Parts.Work;

            workPart.ModelingViews.WorkView.Fit();
            statusLabel.Text = "Vista adattata al modello";
        }
        catch (Exception ex)
        {
            statusLabel.Text = "Errore Fit: " + ex.Message;
        }
    }

    private void ExportClick(object sender, EventArgs e)
    {
        if (!isExporting)
        {
            ExportAsync();
        }
    }

    private async void ExportAsync()
    {
        isExporting = true;
        exportButton.Enabled = false;
        fitButton.Enabled = false;

        previewBox.Visible = false;
        loadingSpinner.Visible = true;
        timerLabel.Visible = true;

        exportTimer = Stopwatch.StartNew();
        animationTimer.Start();

        await Task.Run(() => PerformExport());

        animationTimer.Stop();
        exportTimer.Stop();

        previewBox.Visible = true;
        loadingSpinner.Visible = false;

        statusLabel.Text = $"Esportazione completata in {exportTimer.Elapsed.TotalSeconds:F2} secondi";
        timerLabel.Visible = false;

        isExporting = false;
        exportButton.Enabled = true;
        fitButton.Enabled = true;
    }

    private void PerformExport()
    {
        try
        {
            Session theSession = Session.GetSession();
            Part workPart = theSession.Parts.Work;

            // Undo mark
            Session.UndoMarkId markId = theSession.SetUndoMark(
                Session.MarkVisibility.Visible, "Export Image");

            // Create image export builder
            ImageExportBuilder imageExportBuilder = workPart.Views.CreateImageExportBuilder();

            imageExportBuilder.RegionMode = false;

            int[] regionTopLeftPoint = new int[2];
            regionTopLeftPoint[0] = 0;
            regionTopLeftPoint[1] = 0;
            imageExportBuilder.SetRegionTopLeftPoint(regionTopLeftPoint);

            imageExportBuilder.RegionWidth = 1;
            imageExportBuilder.RegionHeight = 1;
            imageExportBuilder.DeviceWidth = 1162;
            imageExportBuilder.DeviceHeight = 812;

            imageExportBuilder.FileFormat = ImageExportBuilder.FileFormats.Jpg;
            imageExportBuilder.FileName = "C:\\Users\\AndreaScalenghe\\Desktop\\Assieme_4_CamLidar_step_exported.jpg";
            imageExportBuilder.BackgroundOption = ImageExportBuilder.BackgroundOptions.Original;
            imageExportBuilder.EnhanceEdges = false;

            NXObject nXObject = imageExportBuilder.Commit();

            theSession.DeleteUndoMark(markId, "Export Image");
            imageExportBuilder.Destroy();
            theSession.CleanUpFacetedFacesAndEdges();

            mainForm.Invoke(new Action(() =>
            {
                statusLabel.Text = "Immagine esportata con successo!";
            }));
        }
        catch (Exception ex)
        {
            mainForm.Invoke(new Action(() =>
            {
                statusLabel.Text = "Errore esportazione: " + ex.Message;
            }));
        }
    }

    private void AnimationTick(object sender, EventArgs e)
    {
        // Update timer label
        if (exportTimer != null)
        {
            timerLabel.Text = exportTimer.Elapsed.ToString(@"hh\:mm\:ss");
        }

        // Update spinner rotation
        rotationAngle += 6;
        if (rotationAngle >= 360) rotationAngle = 0;
        loadingSpinner.Invalidate();
    }

    private void DrawLoadingSpinner(object sender, PaintEventArgs e)
    {
        PictureBox pb = sender as PictureBox;
        int centerX = pb.Width / 2;
        int centerY = pb.Height / 2;
        int radius = 60;

        // Draw background
        e.Graphics.Clear(Color.White);

        // Draw rotating spinner
        using (Pen pen = new Pen(Color.FromArgb(76, 175, 80), 4))
        {
            pen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
            pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;

            e.Graphics.TranslateTransform(centerX, centerY);
            e.Graphics.RotateTransform(rotationAngle);

            // Draw arc (spinner effect)
            e.Graphics.DrawArc(pen, -radius, -radius, radius * 2, radius * 2, 0, 90);

            e.Graphics.ResetTransform();
        }

        // Draw text
        string loadingText = "Caricamento...";
        SizeF textSize = e.Graphics.MeasureString(loadingText, new Font("Segoe UI", 12));
        e.Graphics.DrawString(loadingText, new Font("Segoe UI", 12), Brushes.Black,
            centerX - textSize.Width / 2, centerY + radius + 20);
    }

    public static int GetUnloadOption(string dummy)
    {
        return (int)Session.LibraryUnloadOption.Immediately;
    }
}

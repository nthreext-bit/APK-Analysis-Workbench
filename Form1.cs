namespace ApkAssistant;

public enum LogLevel
{
    Info,
    Success,
    Warning,
    Error,
    Debug
}

public class Form1 : Form
{
    // Controls
    private TextBox txtApkPath = null!;
    private Button btnBrowse = null!;
    private TabControl tabMain = null!;
    private TabPage tabDecompile = null!;
    private TabPage tabSearch = null!;
    private TabPage tabLogs = null!;
    private RichTextBox rtbLog = null!;
    private Button btnClearLog = null!;
    private TextBox txtOutputDir = null!;
    private Button btnOutputBrowse = null!;
    private Button btnApktoolDecode = null!;
    private Button btnJadxDecode = null!;
    private Button btnRecompile = null!;
    private Button btnCancel = null!;
    private TextBox txtKeywords = null!;
    private Button btnSearch = null!;
    private DataGridView dgvResults = null!;
    private CheckBox chkScanVpn = null!;
    private CheckBox chkScanPinning = null!;
    private Button btnKnownScan = null!;
    private TextBox txtApktoolPath = null!;
    private TextBox txtJadxPath = null!;
    private TextBox txtApksignerPath = null!;
    private TextBox txtAdbPath = null!;
    private Button btnBrowseApktool = null!;
    private Button btnBrowseJadx = null!;
    private Button btnBrowseApksigner = null!;
    private Button btnBrowseAdb = null!;

    // Automation tab
    private TabPage tabAutomation = null!;
    private CheckBox chkAutoApplyNsc = null!;
    private CheckBox chkAutoAdbInstall = null!;
    private TextBox txtAutoDeviceSerial = null!;
    private Button btnFullAutomation = null!;

    // Logcat tab
    private TabPage tabLogcat = null!;
    private TextBox txtLogcatDeviceSerial = null!;
    private TextBox txtLogcatFilter = null!;
    private Button btnLogcatStart = null!;
    private Button btnLogcatStop = null!;
    private Button btnLogcatClear = null!;
    private RichTextBox rtbLogcat = null!;

    // Diff tab
    private TabPage tabDiff = null!;
    private TextBox txtDiffLeft = null!;
    private TextBox txtDiffRight = null!;
    private Button btnDiffLeftBrowse = null!;
    private Button btnDiffRightBrowse = null!;
    private Button btnDiffCompare = null!;
    private DataGridView dgvDiffResults = null!;
    private TextBox txtDiffDetail = null!;

    // Patching tab
    private TabPage tabPatch = null!;
    private TextBox txtNscPath = null!;
    private Button btnNscBrowse = null!;
    private Button btnNscAutoFind = null!;
    private Button btnNscPatch = null!;
    private Button btnRestoreBackup = null!;
    private TextBox txtSmaliPath = null!;
    private Button btnSmaliBrowse = null!;
    private TextBox txtSmaliSearch = null!;
    private TextBox txtSmaliReplace = null!;
    private CheckBox chkSmaliCaseSensitive = null!;
    private Button btnSmaliReplaceLiteral = null!;
    private Button btnSmaliReplaceRegex = null!;

    // Signing tab
    private TabPage tabSign = null!;
    private TextBox txtZipalignPath = null!;
    private Button btnBrowseZipalign = null!;
    private TextBox txtKeytoolPath = null!;
    private Button btnBrowseKeytool = null!;
    private TextBox txtKeystorePath = null!;
    private Button btnBrowseKeystore = null!;
    private Button btnCreateDebugKeystore = null!;
    private TextBox txtKeystoreAlias = null!;
    private TextBox txtKeystoreStorePass = null!;
    private TextBox txtKeystoreKeyPass = null!;
    private CheckBox chkSignUseZipalign = null!;
    private Button btnSignAndVerify = null!;
    private Label lblVerifyStatus = null!;
    private ListBox lstVerifyDetails = null!;

    // Async state
    private readonly CancellationTokenSource _globalCts = new();
    private CancellationTokenSource? _currentOperationCts;
    private bool _isBusy;

    public Form1()
    {
        BuildUi();
    }

    // ------------------------------------------------------------ UI
    private void BuildUi()
    {
        Text = "Android APK Analiz ve Test Otomasyon Yardımcısı";
        Width = 1100;
        Height = 750;
        MinimumSize = new Size(900, 600);
        StartPosition = FormStartPosition.CenterScreen;

        // Top: APK path
        var topPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 45,
            ColumnCount = 3,
            RowCount = 1,
            Padding = new Padding(8, 8, 8, 4)
        };
        topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));

        var lblApkPath = new Label { Text = "APK Dosyası:", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill };
        txtApkPath = new TextBox { Dock = DockStyle.Fill, ReadOnly = true };
        btnBrowse = new Button { Text = "Gözat...", Dock = DockStyle.Fill };
        btnBrowse.Click += BtnBrowse_Click;
        topPanel.Controls.Add(lblApkPath, 0, 0);
        topPanel.Controls.Add(txtApkPath, 1, 0);
        topPanel.Controls.Add(btnBrowse, 2, 0);

        // Bottom: tool paths (settings)
        var toolsPanel = new GroupBox
        {
            Text = "Araç Yolları (Ayarlar — oturumlar arası kalıcıdır)",
            Dock = DockStyle.Bottom,
            Height = 170,
            Padding = new Padding(8)
        };
        var toolsLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 4
        };
        toolsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        toolsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        toolsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        for (int i = 0; i < 4; i++)
            toolsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 25f));

        (txtApktoolPath, btnBrowseApktool) = CreateToolRow(toolsLayout, 0, "apktool (.bat/.jar):");
        (txtJadxPath, btnBrowseJadx) = CreateToolRow(toolsLayout, 1, "jadx-cli (.bat/.exe):");
        (txtApksignerPath, btnBrowseApksigner) = CreateToolRow(toolsLayout, 2, "apksigner (.bat/.exe):");
        (txtAdbPath, btnBrowseAdb) = CreateToolRow(toolsLayout, 3, "adb (.exe, ops.):");

        btnBrowseApktool.Click += (_, _) => BrowseToolFile(txtApktoolPath, "apktool|apktool*.*;*.jar|Tüm dosyalar|*.*");
        btnBrowseJadx.Click += (_, _) => BrowseToolFile(txtJadxPath, "jadx|jadx*.*|Tüm dosyalar|*.*");
        btnBrowseApksigner.Click += (_, _) => BrowseToolFile(txtApksignerPath, "apksigner|apksigner*.*|Tüm dosyalar|*.*");
        btnBrowseAdb.Click += (_, _) => BrowseToolFile(txtAdbPath, "adb|adb*.*|Tüm dosyalar|*.*");
        txtAdbPath.PlaceholderText = "boş bırakılırsa PATH üzerinden 'adb' aranır";
        toolsPanel.Controls.Add(toolsLayout);

        // Middle: tabs
        tabMain = new TabControl { Dock = DockStyle.Fill };
        tabDecompile = new TabPage("Decompile/Recompile");
        tabSearch = new TabPage("Static Code Search");
        tabLogs = new TabPage("Logs");
        tabPatch = new TabPage("Patching");
        tabSign = new TabPage("Signing");
        tabAutomation = new TabPage("Otomasyon");
        tabLogcat = new TabPage("Logcat");
        tabDiff = new TabPage("Diff");
        BuildDecompileTab();
        BuildSearchTab();
        BuildLogsTab();
        BuildPatchTab();
        BuildSignTab();
        BuildAutomationTab();
        BuildLogcatTab();
        BuildDiffTab();
        tabMain.TabPages.Add(tabDecompile);
        tabMain.TabPages.Add(tabSearch);
        tabMain.TabPages.Add(tabLogs);
        tabMain.TabPages.Add(tabPatch);
        tabMain.TabPages.Add(tabSign);
        tabMain.TabPages.Add(tabAutomation);
        tabMain.TabPages.Add(tabLogcat);
        tabMain.TabPages.Add(tabDiff);

        // Dock sırası: Fill önce, kenar panelleri sonra
        Controls.Add(tabMain);
        Controls.Add(toolsPanel);
        Controls.Add(topPanel);
    }

    private (TextBox, Button) CreateToolRow(TableLayoutPanel layout, int row, string label)
    {
        var lbl = new Label { Text = label, TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill };
        var txt = new TextBox { Dock = DockStyle.Fill, Anchor = AnchorStyles.Left | AnchorStyles.Right };
        var btn = new Button { Text = "Gözat...", Dock = DockStyle.Fill };
        layout.Controls.Add(lbl, 0, row);
        layout.Controls.Add(txt, 1, row);
        layout.Controls.Add(btn, 2, row);
        return (txt, btn);
    }

    private void BuildDecompileTab()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 6,
            Padding = new Padding(12)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 5; i++)
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        layout.Controls.Add(new Label { Text = "Çıktı Klasörü:", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill }, 0, 0);
        var outputPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        outputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        outputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        txtOutputDir = new TextBox { Dock = DockStyle.Fill };
        btnOutputBrowse = new Button { Text = "Gözat...", Dock = DockStyle.Fill };
        btnOutputBrowse.Click += BtnOutputBrowse_Click;
        outputPanel.Controls.Add(txtOutputDir, 0, 0);
        outputPanel.Controls.Add(btnOutputBrowse, 1, 0);
        layout.Controls.Add(outputPanel, 1, 0);

        btnApktoolDecode = AddWideButton(layout, 1, "1) Apktool ile Smali'ye Ayrıştır");
        btnApktoolDecode.Click += BtnApktoolDecode_Click;
        btnJadxDecode = AddWideButton(layout, 2, "2) JADX ile Java Kaynaklarına Ayrıştır");
        btnJadxDecode.Click += BtnJadxDecode_Click;
        btnRecompile = AddWideButton(layout, 3, "3) Değiştirilmiş Klasörü Yeniden Derle");
        btnRecompile.Click += BtnRecompile_Click;
        btnCancel = AddWideButton(layout, 4, "İşlemi İptal Et");
        btnCancel.Enabled = false;
        btnCancel.Click += BtnCancel_Click;

        tabDecompile.Controls.Add(layout);
    }

    private static Button AddWideButton(TableLayoutPanel layout, int row, string text)
    {
        var btn = new Button { Text = text, Dock = DockStyle.Fill };
        layout.Controls.Add(btn, 0, row);
        layout.SetColumnSpan(btn, 2);
        return btn;
    }

    private void BuildSearchTab()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3,
            Padding = new Padding(12)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        txtKeywords = new TextBox
        {
            Dock = DockStyle.Fill,
            Text = "TRANSPORT_VPN, NetworkInterface, checkSignature, com.openvpn",
            PlaceholderText = "Virgülle ayrılmış kelimeler"
        };
        btnSearch = new Button { Text = "Ara", Dock = DockStyle.Fill };
        btnSearch.Click += BtnSearch_Click;

        // --- Bilinen kalıp taraması: yalnızca elle incelemek için aday
        // konum listeler, hiçbir dosyayı değiştirmez. ---
        var knownScanPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = false
        };
        chkScanVpn = new CheckBox { Text = "VPN Tespiti kalıpları", AutoSize = true, Checked = true, Margin = new Padding(0, 6, 16, 0) };
        chkScanPinning = new CheckBox { Text = "SSL Pinning kalıpları", AutoSize = true, Checked = true, Margin = new Padding(0, 6, 16, 0) };
        knownScanPanel.Controls.Add(chkScanVpn);
        knownScanPanel.Controls.Add(chkScanPinning);

        btnKnownScan = new Button { Text = "Bilinen Kalıpları Tara (salt okunur)", Dock = DockStyle.Fill };
        btnKnownScan.Click += BtnKnownScan_Click;

        dgvResults = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect
        };
        dgvResults.Columns.Add("FilePath", "Dosya");
        dgvResults.Columns.Add("LineNumber", "Satır");
        dgvResults.Columns.Add("Match", "Eşleşen");
        dgvResults.Columns.Add("LineContent", "İçerik");
        dgvResults.Columns.Add("Category", "Kategori / Etiket");

        layout.Controls.Add(txtKeywords, 0, 0);
        layout.Controls.Add(btnSearch, 1, 0);
        layout.Controls.Add(knownScanPanel, 0, 1);
        layout.Controls.Add(btnKnownScan, 1, 1);
        layout.Controls.Add(dgvResults, 0, 2);
        layout.SetColumnSpan(dgvResults, 2);
        tabSearch.Controls.Add(layout);
    }

    private void BuildLogsTab()
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var top = new Panel { Dock = DockStyle.Fill, Padding = new Padding(4) };
        btnClearLog = new Button { Text = "Temizle", Dock = DockStyle.Right, Width = 90 };
        btnClearLog.Click += (_, _) => rtbLog.Clear();
        top.Controls.Add(btnClearLog);

        rtbLog = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BackColor = Color.White,
            Font = new Font("Consolas", 9.5f),
            WordWrap = false,
            HideSelection = false
        };

        layout.Controls.Add(top, 0, 0);
        layout.Controls.Add(rtbLog, 0, 1);
        tabLogs.Controls.Add(layout);
    }

    private void BuildPatchTab()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(10)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 210));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // ----- Üst: network_security_config.xml -----
        var nscGroup = new GroupBox
        {
            Text = "network_security_config.xml — pinning/trust temizle, cleartext aç",
            Dock = DockStyle.Fill
        };
        var nscLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 3,
            Padding = new Padding(8)
        };
        nscLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        nscLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        nscLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        for (int i = 0; i < 3; i++) nscLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

        nscLayout.Controls.Add(new Label { Text = "Dosya:", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill }, 0, 0);
        txtNscPath = new TextBox { Dock = DockStyle.Fill };
        nscLayout.Controls.Add(txtNscPath, 1, 0);
        btnNscBrowse = new Button { Text = "Gözat...", Dock = DockStyle.Fill };
        btnNscBrowse.Click += BtnNscBrowse_Click;
        nscLayout.Controls.Add(btnNscBrowse, 2, 0);

        btnNscAutoFind = new Button { Text = "Çıktı Klasöründe Otomatik Bul", Dock = DockStyle.Fill };
        btnNscAutoFind.Click += BtnNscAutoFind_Click;
        nscLayout.Controls.Add(btnNscAutoFind, 0, 1);
        nscLayout.SetColumnSpan(btnNscAutoFind, 2);

        btnNscPatch = new Button { Text = "Yamala", Dock = DockStyle.Fill, BackColor = Color.LightGreen };
        btnNscPatch.Click += BtnNscPatch_Click;
        nscLayout.Controls.Add(btnNscPatch, 2, 1);

        btnRestoreBackup = new Button { Text = "Yedekten Geri Al (NSC)", Dock = DockStyle.Fill };
        btnRestoreBackup.Click += BtnRestoreBackup_Click;
        nscLayout.Controls.Add(btnRestoreBackup, 0, 2);
        nscLayout.SetColumnSpan(btnRestoreBackup, 3);

        nscGroup.Controls.Add(nscLayout);

        // ----- Alt: Smali replace -----
        var smaliGroup = new GroupBox { Text = "Smali / Metin — literal veya regex değiştir", Dock = DockStyle.Fill };
        var smaliLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 5,
            Padding = new Padding(8)
        };
        smaliLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        smaliLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        smaliLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        for (int i = 0; i < 5; i++) smaliLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

        smaliLayout.Controls.Add(new Label { Text = "Dosya:", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill }, 0, 0);
        txtSmaliPath = new TextBox { Dock = DockStyle.Fill };
        smaliLayout.Controls.Add(txtSmaliPath, 1, 0);
        btnSmaliBrowse = new Button { Text = "Gözat...", Dock = DockStyle.Fill };
        btnSmaliBrowse.Click += BtnSmaliBrowse_Click;
        smaliLayout.Controls.Add(btnSmaliBrowse, 2, 0);

        smaliLayout.Controls.Add(new Label { Text = "Ara:", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill }, 0, 1);
        txtSmaliSearch = new TextBox { Dock = DockStyle.Fill };
        smaliLayout.Controls.Add(txtSmaliSearch, 1, 1);
        chkSmaliCaseSensitive = new CheckBox { Text = "Büyük/küçük harf", Dock = DockStyle.Fill };
        smaliLayout.Controls.Add(chkSmaliCaseSensitive, 2, 1);

        smaliLayout.Controls.Add(new Label { Text = "Değiştir:", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill }, 0, 2);
        txtSmaliReplace = new TextBox { Dock = DockStyle.Fill };
        smaliLayout.Controls.Add(txtSmaliReplace, 1, 2);

        btnSmaliReplaceLiteral = new Button { Text = "Değiştir (Literal)", Dock = DockStyle.Fill, BackColor = Color.LightYellow };
        btnSmaliReplaceLiteral.Click += (_, _) => RunSmaliReplace(isRegex: false);
        smaliLayout.Controls.Add(btnSmaliReplaceLiteral, 1, 3);

        btnSmaliReplaceRegex = new Button { Text = "Değiştir (Regex)", Dock = DockStyle.Fill, BackColor = Color.LightYellow };
        btnSmaliReplaceRegex.Click += (_, _) => RunSmaliReplace(isRegex: true);
        smaliLayout.Controls.Add(btnSmaliReplaceRegex, 2, 3);

        smaliGroup.Controls.Add(smaliLayout);

        root.Controls.Add(nscGroup, 0, 0);
        root.Controls.Add(smaliGroup, 0, 1);
        tabPatch.Controls.Add(root);
    }

    private void BuildSignTab()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(10)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 170));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 220));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // --- Tools ---
        var toolsGroup = new GroupBox { Text = "İmzalama Araçları", Dock = DockStyle.Fill };
        var toolsLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 2, Padding = new Padding(8) };
        toolsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        toolsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        toolsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        toolsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        toolsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

        toolsLayout.Controls.Add(new Label { Text = "zipalign (.exe):", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill }, 0, 0);
        txtZipalignPath = new TextBox { Dock = DockStyle.Fill };
        btnBrowseZipalign = new Button { Text = "Gözat...", Dock = DockStyle.Fill };
        btnBrowseZipalign.Click += (_, _) => BrowseToolFile(txtZipalignPath, "zipalign|zipalign*|*.exe|Tümü|*.*");
        toolsLayout.Controls.Add(txtZipalignPath, 1, 0);
        toolsLayout.Controls.Add(btnBrowseZipalign, 2, 0);

        toolsLayout.Controls.Add(new Label { Text = "keytool (.exe):", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill }, 0, 1);
        txtKeytoolPath = new TextBox { Dock = DockStyle.Fill, Text = "keytool" };
        btnBrowseKeytool = new Button { Text = "Gözat...", Dock = DockStyle.Fill };
        btnBrowseKeytool.Click += (_, _) => BrowseToolFile(txtKeytoolPath, "keytool|keytool*|*.exe|Tümü|*.*");
        toolsLayout.Controls.Add(txtKeytoolPath, 1, 1);
        toolsLayout.Controls.Add(btnBrowseKeytool, 2, 1);

        toolsGroup.Controls.Add(toolsLayout);

        // --- Keystore ---
        var ksGroup = new GroupBox { Text = "Keystore (imza anahtarı)", Dock = DockStyle.Fill };
        var ksLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 4, Padding = new Padding(8) };
        ksLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        ksLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        ksLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        for (int i = 0; i < 4; i++) ksLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 25));

        ksLayout.Controls.Add(new Label { Text = "Keystore:", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill }, 0, 0);
        txtKeystorePath = new TextBox { Dock = DockStyle.Fill, Text = Signer.GetDefaultDebugKeystorePath() };
        btnBrowseKeystore = new Button { Text = "Gözat...", Dock = DockStyle.Fill };
        btnBrowseKeystore.Click += BtnBrowseKeystore_Click;
        ksLayout.Controls.Add(txtKeystorePath, 1, 0);
        ksLayout.Controls.Add(btnBrowseKeystore, 2, 0);

        ksLayout.Controls.Add(new Label { Text = "Alias:", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill }, 0, 1);
        txtKeystoreAlias = new TextBox { Dock = DockStyle.Fill, Text = Signer.DefaultDebugAlias };
        btnCreateDebugKeystore = new Button { Text = "Debug Üret", Dock = DockStyle.Fill };
        btnCreateDebugKeystore.Click += BtnCreateDebugKeystore_Click;
        ksLayout.Controls.Add(txtKeystoreAlias, 1, 1);
        ksLayout.Controls.Add(btnCreateDebugKeystore, 2, 1);

        ksLayout.Controls.Add(new Label { Text = "Store Şifre:", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill }, 0, 2);
        txtKeystoreStorePass = new TextBox { Dock = DockStyle.Fill, Text = Signer.DefaultDebugPass, UseSystemPasswordChar = true };
        ksLayout.Controls.Add(txtKeystoreStorePass, 1, 2);

        ksLayout.Controls.Add(new Label { Text = "Key Şifre:", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill }, 0, 3);
        txtKeystoreKeyPass = new TextBox { Dock = DockStyle.Fill, Text = Signer.DefaultDebugPass, UseSystemPasswordChar = true };
        ksLayout.Controls.Add(txtKeystoreKeyPass, 1, 3);

        ksGroup.Controls.Add(ksLayout);

        // --- Action + Result ---
        var actionGroup = new GroupBox { Text = "İmzala & Doğrula", Dock = DockStyle.Fill };
        var actionLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(8) };
        actionLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        actionLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        actionLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        actionLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        chkSignUseZipalign = new CheckBox { Text = "Önce zipalign uygula (4-byte hizalama)", Checked = true, Dock = DockStyle.Fill };
        actionLayout.Controls.Add(chkSignUseZipalign, 0, 0);

        btnSignAndVerify = new Button { Text = "İmzala & Doğrula", Dock = DockStyle.Fill, BackColor = Color.LightSkyBlue };
        btnSignAndVerify.Click += BtnSignAndVerify_Click;
        actionLayout.Controls.Add(btnSignAndVerify, 0, 1);

        lblVerifyStatus = new Label
        {
            Text = "Durum: —",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font(Font, FontStyle.Bold)
        };
        actionLayout.Controls.Add(lblVerifyStatus, 0, 2);

        lstVerifyDetails = new ListBox { Dock = DockStyle.Fill };
        actionLayout.Controls.Add(lstVerifyDetails, 0, 3);

        actionGroup.Controls.Add(actionLayout);

        root.Controls.Add(toolsGroup, 0, 0);
        root.Controls.Add(ksGroup, 0, 1);
        root.Controls.Add(actionGroup, 0, 2);
        tabSign.Controls.Add(root);
    }

    private void BuildAutomationTab()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 5,
            Padding = new Padding(12)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var info = new Label
        {
            Text =
                "Tam otomasyon: Decompile (apktool) → (opsiyonel) network_security_config yaması → " +
                "Recompile → Zipalign + İmzala + Doğrula → (opsiyonel) adb install.\n" +
                "Üstteki APK yolu / Araç Yolları ve Signing sekmesindeki keystore bilgileri kullanılır.",
            Dock = DockStyle.Fill,
            AutoSize = false
        };
        layout.Controls.Add(info, 0, 0);
        layout.SetColumnSpan(info, 2);

        chkAutoApplyNsc = new CheckBox
        {
            Text = "network_security_config.xml bulunursa yamayı uygula",
            AutoSize = true,
            Checked = true
        };
        layout.Controls.Add(chkAutoApplyNsc, 0, 1);
        layout.SetColumnSpan(chkAutoApplyNsc, 2);

        chkAutoAdbInstall = new CheckBox
        {
            Text = "İmzalama sonrası adb ile bağlı cihaza/emülatöre yükle",
            AutoSize = true,
            Checked = false
        };
        layout.Controls.Add(chkAutoAdbInstall, 0, 2);
        layout.SetColumnSpan(chkAutoAdbInstall, 2);

        layout.Controls.Add(new Label
        {
            Text = "Cihaz Seri No (ops.):",
            TextAlign = ContentAlignment.MiddleLeft,
            Dock = DockStyle.Fill
        }, 0, 3);
        txtAutoDeviceSerial = new TextBox
        {
            Dock = DockStyle.Fill,
            PlaceholderText = "örn. emulator-5554 — boş bırakılırsa tek bağlı cihaz kullanılır"
        };
        layout.Controls.Add(txtAutoDeviceSerial, 1, 3);

        btnFullAutomation = new Button
        {
            Text = "▶ Tam Otomasyonu Başlat (Decompile → Patch → Recompile → İmzala → [Yükle])",
            Dock = DockStyle.Fill
        };
        btnFullAutomation.Click += BtnFullAutomation_Click;
        layout.Controls.Add(btnFullAutomation, 0, 4);
        layout.SetColumnSpan(btnFullAutomation, 2);

        tabAutomation.Controls.Add(layout);
    }

    private void BuildLogcatTab()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 2,
            Padding = new Padding(8)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var topPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));

        txtLogcatDeviceSerial = new TextBox { Dock = DockStyle.Fill, PlaceholderText = "Cihaz Seri No (ops.)" };
        txtLogcatFilter = new TextBox { Dock = DockStyle.Fill, PlaceholderText = "adb logcat filtresi (ops., örn. MyApp:D *:S)" };
        topPanel.Controls.Add(txtLogcatDeviceSerial, 0, 0);
        topPanel.Controls.Add(txtLogcatFilter, 1, 0);

        btnLogcatStart = new Button { Text = "Başlat", Dock = DockStyle.Fill };
        btnLogcatStart.Click += BtnLogcatStart_Click;
        btnLogcatStop = new Button { Text = "Durdur", Dock = DockStyle.Fill, Enabled = false };
        btnLogcatStop.Click += BtnLogcatStop_Click;
        btnLogcatClear = new Button { Text = "Temizle", Dock = DockStyle.Fill };
        btnLogcatClear.Click += (_, _) => rtbLogcat.Clear();

        rtbLogcat = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BackColor = Color.Black,
            ForeColor = Color.LightGreen,
            Font = new Font("Consolas", 9f),
            WordWrap = false,
            HideSelection = false
        };

        layout.Controls.Add(topPanel, 0, 0);
        layout.Controls.Add(btnLogcatStart, 1, 0);
        layout.Controls.Add(btnLogcatStop, 2, 0);
        layout.Controls.Add(btnLogcatClear, 3, 0);
        layout.Controls.Add(rtbLogcat, 0, 1);
        layout.SetColumnSpan(rtbLogcat, 4);

        tabLogcat.Controls.Add(layout);
    }

    private void BuildDiffTab()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 4,
            Padding = new Padding(12)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 45));

        layout.Controls.Add(new Label
        {
            Text = "Sol Klasör (eski):",
            TextAlign = ContentAlignment.MiddleLeft,
            Dock = DockStyle.Fill
        }, 0, 0);
        txtDiffLeft = new TextBox { Dock = DockStyle.Fill };
        btnDiffLeftBrowse = new Button { Text = "Gözat...", Dock = DockStyle.Fill };
        btnDiffLeftBrowse.Click += (_, _) => BrowseFolderInto(txtDiffLeft);
        layout.Controls.Add(txtDiffLeft, 1, 0);
        layout.Controls.Add(btnDiffLeftBrowse, 2, 0);

        layout.Controls.Add(new Label
        {
            Text = "Sağ Klasör (yeni):",
            TextAlign = ContentAlignment.MiddleLeft,
            Dock = DockStyle.Fill
        }, 0, 1);
        txtDiffRight = new TextBox { Dock = DockStyle.Fill };
        btnDiffRightBrowse = new Button { Text = "Gözat...", Dock = DockStyle.Fill };
        btnDiffRightBrowse.Click += (_, _) => BrowseFolderInto(txtDiffRight);
        layout.Controls.Add(txtDiffRight, 1, 1);
        layout.Controls.Add(btnDiffRightBrowse, 2, 1);

        dgvDiffResults = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false
        };
        dgvDiffResults.Columns.Add("RelativePath", "Dosya (göreli yol)");
        dgvDiffResults.Columns.Add("Status", "Durum");
        dgvDiffResults.SelectionChanged += DgvDiffResults_SelectionChanged;

        btnDiffCompare = new Button { Text = "Karşılaştır", Dock = DockStyle.Fill };
        btnDiffCompare.Click += BtnDiffCompare_Click;
        layout.Controls.Add(btnDiffCompare, 0, 2);
        layout.Controls.Add(dgvDiffResults, 1, 2);
        layout.SetColumnSpan(dgvDiffResults, 2);

        txtDiffDetail = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            Font = new Font("Consolas", 9f),
            WordWrap = false
        };
        layout.Controls.Add(txtDiffDetail, 0, 3);
        layout.SetColumnSpan(txtDiffDetail, 3);

        tabDiff.Controls.Add(layout);
    }

    private void BrowseFolderInto(TextBox target)
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = "Klasör seçin",
            UseDescriptionForTitle = true
        };
        if (dlg.ShowDialog(this) == DialogResult.OK)
            target.Text = dlg.SelectedPath;
    }

    // ------------------------------------------------------------ Logging
    private void AppendLog(string message, LogLevel level = LogLevel.Info)
    {
        if (rtbLog.InvokeRequired)
        {
            rtbLog.BeginInvoke(new Action(() => AppendLog(message, level)));
            return;
        }
        rtbLog.SelectionStart = rtbLog.TextLength;
        rtbLog.SelectionLength = 0;
        rtbLog.SelectionColor = ColorFor(level);
        rtbLog.AppendText($"[{DateTime.Now:HH:mm:ss}] [{level}] {message}{Environment.NewLine}");
        rtbLog.SelectionColor = rtbLog.ForeColor;
        rtbLog.ScrollToCaret();
    }

    private static Color ColorFor(LogLevel level) => level switch
    {
        LogLevel.Info    => Color.Green,
        LogLevel.Success => Color.DarkGreen,
        LogLevel.Warning => Color.DarkOrange,
        LogLevel.Error   => Color.Red,
        LogLevel.Debug   => Color.Gray,
        _                => Color.Black
    };

    // ------------------------------------------------------------ Async altyapı
    private async Task RunBusyAsync(Func<CancellationToken, Task> action, string operationName)
    {
        if (_isBusy)
        {
            AppendLog("Başka bir işlem sürüyor.", LogLevel.Warning);
            return;
        }

        SetBusy(true);
        _currentOperationCts = CancellationTokenSource.CreateLinkedTokenSource(_globalCts.Token);
        try
        {
            AppendLog($"▶ {operationName} başladı...");
            await action(_currentOperationCts.Token);
            AppendLog($"✓ {operationName} tamamlandı.", LogLevel.Success);
        }
        catch (OperationCanceledException)
        {
            AppendLog($"✗ {operationName} iptal edildi.", LogLevel.Warning);
        }
        catch (Exception ex)
        {
            AppendLog($"✗ {operationName} hatası: {ex.Message}", LogLevel.Error);
        }
        finally
        {
            _currentOperationCts?.Dispose();
            _currentOperationCts = null;
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        _isBusy = busy;
        btnBrowse.Enabled = !busy;
        btnApktoolDecode.Enabled = !busy;
        btnJadxDecode.Enabled = !busy;
        btnRecompile.Enabled = !busy;
        btnSearch.Enabled = !busy;
        btnOutputBrowse.Enabled = !busy;
        btnBrowseApktool.Enabled = !busy;
        btnBrowseJadx.Enabled = !busy;
        btnBrowseApksigner.Enabled = !busy;
        btnNscBrowse.Enabled = !busy;
        btnNscAutoFind.Enabled = !busy;
        btnNscPatch.Enabled = !busy;
        btnRestoreBackup.Enabled = !busy;
        btnSmaliBrowse.Enabled = !busy;
        btnSmaliReplaceLiteral.Enabled = !busy;
        btnSmaliReplaceRegex.Enabled = !busy;
        btnBrowseZipalign.Enabled = !busy;
        btnBrowseKeytool.Enabled = !busy;
        btnBrowseKeystore.Enabled = !busy;
        btnCreateDebugKeystore.Enabled = !busy;
        btnSignAndVerify.Enabled = !busy;
        btnKnownScan.Enabled = !busy;
        btnBrowseAdb.Enabled = !busy;
        btnFullAutomation.Enabled = !busy;
        btnDiffCompare.Enabled = !busy;
        btnDiffLeftBrowse.Enabled = !busy;
        btnDiffRightBrowse.Enabled = !busy;
        btnLogcatStart.Enabled = !busy;
        btnLogcatStop.Enabled = busy;
        btnCancel.Enabled = busy;
    }

    // ------------------------------------------------------------ Handlers
    private void BtnBrowse_Click(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Title = "APK Dosyası Seç",
            Filter = "APK Dosyaları|*.apk|Tüm Dosyalar|*.*",
            CheckFileExists = true
        };
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            txtApkPath.Text = dlg.FileName;
            AppendLog($"APK seçildi: {dlg.FileName}");
        }
    }

    private void BtnOutputBrowse_Click(object? sender, EventArgs e)
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = "Çıktı klasörünü seçin",
            UseDescriptionForTitle = true
        };
        if (dlg.ShowDialog(this) == DialogResult.OK)
            txtOutputDir.Text = dlg.SelectedPath;
    }

    private void BrowseToolFile(TextBox target, string filter)
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Araç dosyasını seç",
            Filter = filter,
            CheckFileExists = true
        };
        if (dlg.ShowDialog(this) == DialogResult.OK)
            target.Text = dlg.FileName;
    }

    private bool ValidateApkPath()
    {
        var path = txtApkPath.Text.Trim();
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            AppendLog("Önce geçerli bir APK dosyası seçin.", LogLevel.Warning);
            MessageBox.Show(this, "Lütfen geçerli bir APK dosyası seçin.", "Uyarı",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
        if (!path.EndsWith(".apk", StringComparison.OrdinalIgnoreCase))
            AppendLog("Seçilen dosya .apk uzantılı değil.", LogLevel.Warning);
        return true;
    }

    // ------------------------------------------------------------ ApkManager köprüsü
    private ApkManager CreateApkManager() => new(
        apktoolPath:   txtApktoolPath.Text.Trim(),
        jadxPath:      txtJadxPath.Text.Trim(),
        apksignerPath: txtApksignerPath.Text.Trim(),
        adbPath:       txtAdbPath.Text.Trim());

    /// <summary>Arka plan satırlarını UI thread'ine taşıyıp loga yazar (Progress&lt;T&gt; SyncContext yakalar).</summary>
    private IProgress<string> CreateProgress() =>
        new Progress<string>(line => AppendLog(line, LogLevel.Debug));

    private bool ValidateToolPath(string path, string friendlyName)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            AppendLog($"Araç yolu geçersiz/boş: {friendlyName}. Ayarlar panelinden seçin.", LogLevel.Warning);
            MessageBox.Show(this,
                $"{friendlyName} için geçerli bir dosya yolu seçin (Ayarlar paneli).",
                "Eksik Ayar", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
        return true;
    }

    private async void BtnApktoolDecode_Click(object? sender, EventArgs e)
    {
        if (!ValidateApkPath()) return;
        if (!ValidateToolPath(txtApktoolPath.Text.Trim(), "apktool")) return;

        var apk = txtApkPath.Text.Trim();
        var outDir = string.IsNullOrWhiteSpace(txtOutputDir.Text)
            ? Path.Combine(Path.GetDirectoryName(apk)!, Path.GetFileNameWithoutExtension(apk) + "_apktool")
            : txtOutputDir.Text.Trim();

        await RunBusyAsync(async ct =>
        {
            AppendLog($"apktool → d -f -o \"{outDir}\" \"{apk}\"", LogLevel.Debug);
            await CreateApkManager().DecompileApkToolAsync(apk, outDir, CreateProgress(), ct);
            if (!Directory.Exists(outDir))
                throw new InvalidOperationException("apktool çıktı klasörü oluşmadı — araç çıktısını inceleyin.");
        }, "Apktool decompile");
    }

    private async void BtnJadxDecode_Click(object? sender, EventArgs e)
    {
        if (!ValidateApkPath()) return;
        if (!ValidateToolPath(txtJadxPath.Text.Trim(), "jadx")) return;

        var apk = txtApkPath.Text.Trim();
        var outDir = string.IsNullOrWhiteSpace(txtOutputDir.Text)
            ? Path.Combine(Path.GetDirectoryName(apk)!, Path.GetFileNameWithoutExtension(apk) + "_jadx")
            : txtOutputDir.Text.Trim();

        await RunBusyAsync(async ct =>
        {
            AppendLog($"jadx → -d \"{outDir}\" \"{apk}\"", LogLevel.Debug);
            await CreateApkManager().DecompileJadxAsync(apk, outDir, CreateProgress(), ct);
            if (!Directory.Exists(outDir))
                throw new InvalidOperationException("jadx çıktı klasörü oluşmadı — araç çıktısını inceleyin.");
        }, "JADX decompile");
    }

    private async void BtnRecompile_Click(object? sender, EventArgs e)
    {
        if (!ValidateToolPath(txtApktoolPath.Text.Trim(), "apktool")) return;

        var sourceDir = txtOutputDir.Text.Trim();
        if (string.IsNullOrEmpty(sourceDir) || !Directory.Exists(sourceDir))
        {
            AppendLog("Yeniden derlemek için önce geçerli bir çıktı klasörü gerekli.", LogLevel.Warning);
            return;
        }

        var apkFromUi = txtApkPath.Text.Trim();
        var outputApk = string.IsNullOrEmpty(apkFromUi)
            ? sourceDir.TrimEnd('\\', '/') + "_rebuilt.apk"
            : Path.Combine(Path.GetDirectoryName(apkFromUi)!,
                           Path.GetFileNameWithoutExtension(apkFromUi) + "_rebuilt.apk");

        await RunBusyAsync(async ct =>
        {
            AppendLog($"apktool → b \"{sourceDir}\" -o \"{outputApk}\"", LogLevel.Debug);
            await CreateApkManager().RecompileApkAsync(sourceDir, outputApk, CreateProgress(), ct);
            if (!File.Exists(outputApk))
                throw new InvalidOperationException("Yeniden derleme çıktısı oluşmadı.");
            AppendLog($"Çıktı APK: {outputApk}", LogLevel.Success);
        }, "Recompile");
    }

    private async void BtnSearch_Click(object? sender, EventArgs e)
    {
        var keywordsRaw = txtKeywords.Text.Trim();
        if (string.IsNullOrEmpty(keywordsRaw))
        {
            AppendLog("Aranacak kelime listesi boş.", LogLevel.Warning);
            return;
        }
        var rootDir = txtOutputDir.Text.Trim();
        if (string.IsNullOrEmpty(rootDir) || !Directory.Exists(rootDir))
        {
            AppendLog("Önce geçerli bir çıktı klasörü belirleyin (arama için).", LogLevel.Warning);
            return;
        }

        var keywords = keywordsRaw.Split(',',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        dgvResults.Rows.Clear();

        await RunBusyAsync(async ct =>
        {
            var analyzer = new CodeAnalyzer();
            var results = await analyzer.SearchAsync(rootDir, keywords, CreateProgress(), ct);

            // await sonrası UI thread'indeyiz
            dgvResults.SuspendLayout();
            try
            {
                foreach (var r in results)
                    dgvResults.Rows.Add(r.FilePath, r.LineNumber, r.Match, r.LineContent, "");
            }
            finally
            {
                dgvResults.ResumeLayout();
            }

            AppendLog($"{results.Count} sonuç bulundu.", LogLevel.Success);
        }, "Kod taraması");
    }

    private async void BtnKnownScan_Click(object? sender, EventArgs e)
    {
        var rootDir = txtOutputDir.Text.Trim();
        if (string.IsNullOrEmpty(rootDir) || !Directory.Exists(rootDir))
        {
            AppendLog("Önce geçerli bir çıktı klasörü belirleyin (tarama için).", LogLevel.Warning);
            return;
        }

        bool includeVpn = chkScanVpn.Checked;
        bool includePinning = chkScanPinning.Checked;
        if (!includeVpn && !includePinning)
        {
            AppendLog("En az bir kategori seçmelisiniz: VPN Tespiti veya SSL Pinning.", LogLevel.Warning);
            return;
        }

        dgvResults.Rows.Clear();

        AppendLog(
            "Bilinen kalıp taraması salt okunurdur: hiçbir dosyayı değiştirmez, " +
            "yalnızca elle incelenmesi gereken aday satırları listeler. " +
            "Her bulguyu kendiniz değerlendirip karar vermeniz gerekir.",
            LogLevel.Info);

        await RunBusyAsync(async ct =>
        {
            var scanner = new KnownPatternScanner();
            var hits = await scanner.ScanAsync(rootDir, includeVpn, includePinning, CreateProgress(), ct);

            dgvResults.SuspendLayout();
            try
            {
                foreach (var h in hits)
                    dgvResults.Rows.Add(h.FilePath, h.LineNumber, h.Match, h.LineContent, $"{h.Category}: {h.Label}");
            }
            finally
            {
                dgvResults.ResumeLayout();
            }

            AppendLog($"{hits.Count} aday satır bulundu (yalnızca inceleme için).", LogLevel.Success);
        }, "Bilinen kalıp taraması");
    }

    // ------------------------------------------------------------ Adım 4: Patching
    private void BtnNscBrowse_Click(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Title = "network_security_config.xml seç",
            Filter = "XML|*.xml|Tümü|*.*",
            CheckFileExists = true
        };
        if (dlg.ShowDialog(this) == DialogResult.OK)
            txtNscPath.Text = dlg.FileName;
    }

    private void BtnNscAutoFind_Click(object? sender, EventArgs e)
    {
        var root = txtOutputDir.Text.Trim();
        if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
        {
            AppendLog("Otomatik bulma için önce geçerli bir çıktı klasörü gerekli.", LogLevel.Warning);
            return;
        }

        try
        {
            var candidates = Directory.GetFiles(root, "network_security_config.xml", SearchOption.AllDirectories);
            if (candidates.Length == 0)
            {
                AppendLog("network_security_config.xml bulunamadı.", LogLevel.Warning);
                return;
            }

            txtNscPath.Text = candidates[0];
            AppendLog($"Bulundu: {candidates[0]}", LogLevel.Success);
            if (candidates.Length > 1)
                AppendLog($"Uyarı: {candidates.Length} aday vardı, ilki seçildi.", LogLevel.Warning);
        }
        catch (Exception ex)
        {
            AppendLog($"Arama hatası: {ex.Message}", LogLevel.Error);
        }
    }

    private async void BtnNscPatch_Click(object? sender, EventArgs e)
    {
        var path = txtNscPath.Text.Trim();
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            AppendLog("Geçerli bir network_security_config.xml seç.", LogLevel.Warning);
            return;
        }

        await RunBusyAsync(async ct =>
        {
            var mgr = new PatchManager();
            var result = await mgr.PatchNetworkSecurityConfigAsync(path, ct);
            AppendLog($"Değişiklik: {result.ChangesMade} — {result.Summary}", LogLevel.Info);
            AppendLog($"Yedek: {result.BackupPath}", LogLevel.Debug);
        }, "NSC yaması");
    }

    private async void BtnRestoreBackup_Click(object? sender, EventArgs e)
    {
        var path = txtNscPath.Text.Trim();
        if (string.IsNullOrEmpty(path))
        {
            AppendLog("Geri alınacak dosya yolu boş.", LogLevel.Warning);
            return;
        }

        await RunBusyAsync(async _ =>
        {
            await Task.Yield();
            var mgr = new PatchManager();
            if (mgr.TryRestoreFromBackup(path, out var used))
                AppendLog($"Geri alındı: {path} ← {used}", LogLevel.Success);
            else
                throw new FileNotFoundException("Uygun yedek bulunamadı: " + path);
        }, "Geri alma");
    }

    private void BtnSmaliBrowse_Click(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Smali / metin dosyası seç",
            Filter = "Smali|*.smali|Java|*.java|Metin|*.txt;*.xml|Tümü|*.*",
            CheckFileExists = true
        };
        if (dlg.ShowDialog(this) == DialogResult.OK)
            txtSmaliPath.Text = dlg.FileName;
    }

    private async void RunSmaliReplace(bool isRegex)
    {
        var path = txtSmaliPath.Text.Trim();
        var search = txtSmaliSearch.Text;
        var replace = txtSmaliReplace.Text;

        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            AppendLog("Geçerli bir smali/metin dosyası seç.", LogLevel.Warning);
            return;
        }
        if (string.IsNullOrEmpty(search))
        {
            AppendLog("Aranacak metin boş.", LogLevel.Warning);
            return;
        }

        var caseSensitive = chkSmaliCaseSensitive.Checked;

        await RunBusyAsync(async ct =>
        {
            var mgr = new PatchManager();
            var result = await mgr.ReplaceInFileAsync(path, search, replace, isRegex, caseSensitive, ct);
            AppendLog($"Değişiklik: {result.ChangesMade} — {result.Summary}", LogLevel.Info);
            if (result.ChangesMade > 0)
                AppendLog($"Yedek: {result.BackupPath}", LogLevel.Debug);
        }, isRegex ? "Regex replace" : "Literal replace");
    }

    // ------------------------------------------------------------ Adım 5: Signing
    private void BtnBrowseKeystore_Click(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Keystore seç",
            Filter = "Keystore|*.keystore;*.jks|Tümü|*.*",
            CheckFileExists = true
        };
        if (dlg.ShowDialog(this) == DialogResult.OK)
            txtKeystorePath.Text = dlg.FileName;
    }

    private async void BtnCreateDebugKeystore_Click(object? sender, EventArgs e)
    {
        var path = txtKeystorePath.Text.Trim();
        if (string.IsNullOrEmpty(path))
        {
            AppendLog("Keystore yolu boş.", LogLevel.Warning);
            return;
        }

        await RunBusyAsync(async ct =>
        {
            var s = new Signer(
                zipalignPath: txtZipalignPath.Text.Trim(),
                apksignerPath: txtApksignerPath.Text.Trim(),
                keytoolPath: txtKeytoolPath.Text.Trim());
            var created = await s.CreateDebugKeystoreAsync(path, ct: ct);
            AppendLog($"Debug keystore oluşturuldu: {created}", LogLevel.Success);
            AppendLog($"Alias: {Signer.DefaultDebugAlias} | Şifre: {Signer.DefaultDebugPass}", LogLevel.Info);
        }, "Debug keystore üretimi");
    }

    private async void BtnSignAndVerify_Click(object? sender, EventArgs e)
    {
        if (!ValidateApkPath()) return;

        var inputApk = txtApkPath.Text.Trim();
        if (!ValidateToolPath(txtApksignerPath.Text.Trim(), "apksigner")) return;
        if (chkSignUseZipalign.Checked && !ValidateToolPath(txtZipalignPath.Text.Trim(), "zipalign")) return;

        var ksPath = txtKeystorePath.Text.Trim();
        if (!File.Exists(ksPath))
        {
            AppendLog("Keystore bulunamadı. 'Debug Üret' ile oluştur.", LogLevel.Warning);
            return;
        }
        var alias = txtKeystoreAlias.Text.Trim();
        var storePass = txtKeystoreStorePass.Text;
        var keyPass = txtKeystoreKeyPass.Text;

        await RunBusyAsync(async ct =>
        {
            var s = new Signer(
                zipalignPath: txtZipalignPath.Text.Trim(),
                apksignerPath: txtApksignerPath.Text.Trim(),
                keytoolPath: txtKeytoolPath.Text.Trim());

            var dir = Path.GetDirectoryName(inputApk)!;
            var baseName = Path.GetFileNameWithoutExtension(inputApk);
            string signingTarget = inputApk;

            if (chkSignUseZipalign.Checked)
            {
                var alignedApk = Path.Combine(dir, baseName + "_aligned.apk");
                AppendLog($"▶ zipalign -f -p 4 \"{inputApk}\" \"{alignedApk}\"", LogLevel.Debug);
                await s.ZipalignAsync(inputApk, alignedApk, CreateProgress(), ct);
                if (!File.Exists(alignedApk))
                    throw new InvalidOperationException("zipalign çıktısı oluşmadı.");
                signingTarget = alignedApk;
            }

            AppendLog($"▶ apksigner sign → {signingTarget}", LogLevel.Debug);
            await s.SignAsync(signingTarget, ksPath, alias, storePass, keyPass, CreateProgress(), ct);

            AppendLog("▶ apksigner verify --print-certs", LogLevel.Debug);
            var result = await s.VerifyAsync(signingTarget, CreateProgress(), ct);

            lblVerifyStatus.Text = result.IsVerified ? "Durum: ✓ İMZALI ve DOĞRULANDI" : "Durum: ✗ DOĞRULANAMADI";
            lblVerifyStatus.ForeColor = result.IsVerified ? Color.DarkGreen : Color.Red;

            lstVerifyDetails.Items.Clear();
            lstVerifyDetails.Items.Add($"v1 (JAR):   {result.V1Signed}");
            lstVerifyDetails.Items.Add($"v2 (APK):   {result.V2Signed}");
            lstVerifyDetails.Items.Add($"v3 (APK):   {result.V3Signed}");
            lstVerifyDetails.Items.Add($"Signer sayısı: {result.SignerCount}");
            foreach (var dn in result.CertificateDNs)
                lstVerifyDetails.Items.Add("DN: " + dn);

            if (!result.IsVerified)
                throw new InvalidOperationException("İmza doğrulanamadı — apksigner çıktısını inceleyin.");

            AppendLog($"✓ İmzalı APK: {signingTarget}", LogLevel.Success);
        }, "İmzala & Doğrula");
    }

    private void BtnCancel_Click(object? sender, EventArgs e)
    {
        if (_currentOperationCts is { IsCancellationRequested: false })
        {
            AppendLog("İptal isteği gönderildi...", LogLevel.Warning);
            _currentOperationCts.Cancel();
        }
    }

    // ------------------------------------------------------------ Otomasyon (tek buton)
    private async void BtnFullAutomation_Click(object? sender, EventArgs e)
    {
        if (!ValidateApkPath()) return;
        if (!ValidateToolPath(txtApktoolPath.Text.Trim(), "apktool")) return;
        if (!ValidateToolPath(txtApksignerPath.Text.Trim(), "apksigner")) return;
        if (chkSignUseZipalign.Checked && !ValidateToolPath(txtZipalignPath.Text.Trim(), "zipalign")) return;

        var ksPath = txtKeystorePath.Text.Trim();
        if (!File.Exists(ksPath))
        {
            AppendLog(
                "Keystore bulunamadı. Önce Signing sekmesinden 'Debug Üret' ile oluşturun ya da kendi keystore'unuzu seçin.",
                LogLevel.Warning);
            return;
        }

        var apk = txtApkPath.Text.Trim();
        var outDir = string.IsNullOrWhiteSpace(txtOutputDir.Text)
            ? Path.Combine(Path.GetDirectoryName(Path.GetFullPath(apk))!, Path.GetFileNameWithoutExtension(apk) + "_auto")
            : txtOutputDir.Text.Trim();

        var alias = txtKeystoreAlias.Text.Trim();
        var storePass = txtKeystoreStorePass.Text;
        var keyPass = txtKeystoreKeyPass.Text;
        bool applyNsc = chkAutoApplyNsc.Checked;
        bool doInstall = chkAutoAdbInstall.Checked;
        var serial = txtAutoDeviceSerial.Text.Trim();

        await RunBusyAsync(async ct =>
        {
            var apkMgr = CreateApkManager();

            AppendLog("[1/5] Decompile (apktool)...", LogLevel.Info);
            await apkMgr.DecompileApkToolAsync(apk, outDir, CreateProgress(), ct);

            if (applyNsc)
            {
                var nsc = FindNetworkSecurityConfig(outDir);
                if (nsc != null)
                {
                    AppendLog("[2/5] network_security_config.xml yaması uygulanıyor: " + nsc, LogLevel.Info);
                    var pm = new PatchManager();
                    var patchResult = await pm.PatchNetworkSecurityConfigAsync(nsc, ct);
                    AppendLog($"Yama: {patchResult.ChangesMade} değişiklik — {patchResult.Summary}", LogLevel.Info);
                }
                else
                {
                    AppendLog("[2/5] network_security_config.xml bulunamadı, atlanıyor.", LogLevel.Warning);
                }
            }
            else
            {
                AppendLog("[2/5] NSC yaması devre dışı bırakıldı (seçenek kapalı).", LogLevel.Debug);
            }

            var outputApk = Path.GetFullPath(Path.Combine(
                outDir, "..", Path.GetFileNameWithoutExtension(apk) + "_recompiled.apk"));
            AppendLog("[3/5] Recompile (apktool)...", LogLevel.Info);
            await apkMgr.RecompileApkAsync(outDir, outputApk, CreateProgress(), ct);
            if (!File.Exists(outputApk))
                throw new InvalidOperationException("Yeniden derleme çıktısı oluşmadı.");

            AppendLog("[4/5] Zipalign + İmzala + Doğrula...", LogLevel.Info);
            var signer = new Signer(
                zipalignPath: txtZipalignPath.Text.Trim(),
                apksignerPath: txtApksignerPath.Text.Trim(),
                keytoolPath: txtKeytoolPath.Text.Trim());

            string signingTarget = outputApk;
            if (chkSignUseZipalign.Checked)
            {
                var aligned = Path.Combine(
                    Path.GetDirectoryName(outputApk)!, Path.GetFileNameWithoutExtension(outputApk) + "_aligned.apk");
                await signer.ZipalignAsync(outputApk, aligned, CreateProgress(), ct);
                if (!File.Exists(aligned))
                    throw new InvalidOperationException("zipalign çıktısı oluşmadı.");
                signingTarget = aligned;
            }

            await signer.SignAsync(signingTarget, ksPath, alias, storePass, keyPass, CreateProgress(), ct);
            var verify = await signer.VerifyAsync(signingTarget, CreateProgress(), ct);
            if (!verify.IsVerified)
                throw new InvalidOperationException("İmza doğrulanamadı — apksigner çıktısını inceleyin.");
            AppendLog($"✓ İmzalı APK: {signingTarget}", LogLevel.Success);

            if (doInstall)
            {
                AppendLog("[5/5] adb install...", LogLevel.Info);
                await apkMgr.AdbInstallAsync(
                    signingTarget, string.IsNullOrEmpty(serial) ? null : serial,
                    replaceExisting: true, CreateProgress(), ct);
                AppendLog("✓ Cihaza/emülatöre yüklendi.", LogLevel.Success);
            }
            else
            {
                AppendLog("[5/5] adb install devre dışı bırakıldı (seçenek kapalı).", LogLevel.Debug);
            }
        }, "Tam Otomasyon");
    }

    private static string? FindNetworkSecurityConfig(string root)
    {
        try
        {
            var candidates = Directory.GetFiles(root, "network_security_config.xml", SearchOption.AllDirectories);
            return candidates.Length > 0 ? candidates[0] : null;
        }
        catch
        {
            return null;
        }
    }

    // ------------------------------------------------------------ ADB Logcat
    private async void BtnLogcatStart_Click(object? sender, EventArgs e)
    {
        var serial = txtLogcatDeviceSerial.Text.Trim();
        var filterText = txtLogcatFilter.Text.Trim();
        var extraArgs = filterText.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        await RunBusyAsync(async ct =>
        {
            var apkMgr = CreateApkManager();
            var progress = new Progress<string>(AppendLogcatLine);
            await apkMgr.RunLogcatAsync(string.IsNullOrEmpty(serial) ? null : serial, extraArgs, progress, ct);
        }, "ADB Logcat");
    }

    private void BtnLogcatStop_Click(object? sender, EventArgs e) => BtnCancel_Click(sender, e);

    private void AppendLogcatLine(string line)
    {
        if (rtbLogcat.InvokeRequired)
        {
            rtbLogcat.BeginInvoke(new Action(() => AppendLogcatLine(line)));
            return;
        }

        rtbLogcat.AppendText(line + Environment.NewLine);

        const int maxLines = 5000;
        const int keepLines = 3000;
        if (rtbLogcat.Lines.Length > maxLines)
            rtbLogcat.Lines = rtbLogcat.Lines.Skip(rtbLogcat.Lines.Length - keepLines).ToArray();

        rtbLogcat.SelectionStart = rtbLogcat.TextLength;
        rtbLogcat.ScrollToCaret();
    }

    // ------------------------------------------------------------ Diff modu
    private async void BtnDiffCompare_Click(object? sender, EventArgs e)
    {
        var left = txtDiffLeft.Text.Trim();
        var right = txtDiffRight.Text.Trim();
        if (string.IsNullOrEmpty(left) || !Directory.Exists(left))
        {
            AppendLog("Sol klasör geçersiz.", LogLevel.Warning);
            return;
        }
        if (string.IsNullOrEmpty(right) || !Directory.Exists(right))
        {
            AppendLog("Sağ klasör geçersiz.", LogLevel.Warning);
            return;
        }

        dgvDiffResults.Rows.Clear();
        txtDiffDetail.Text = "";

        await RunBusyAsync(async ct =>
        {
            var dm = new DiffManager();
            var entries = await dm.CompareDirectoriesAsync(left, right, ct);

            dgvDiffResults.SuspendLayout();
            try
            {
                foreach (var entry in entries.OrderBy(x => x.RelativePath, StringComparer.OrdinalIgnoreCase))
                    dgvDiffResults.Rows.Add(entry.RelativePath, StatusText(entry.Status));
            }
            finally
            {
                dgvDiffResults.ResumeLayout();
            }

            AppendLog($"{entries.Count} farklı dosya bulundu.", LogLevel.Success);
        }, "Klasör karşılaştırma");
    }

    private static string StatusText(DiffStatus status) => status switch
    {
        DiffStatus.Added => "Eklendi (sadece sağda)",
        DiffStatus.Removed => "Silindi (sadece solda)",
        DiffStatus.Modified => "Değişti",
        _ => status.ToString()
    };

    private void DgvDiffResults_SelectionChanged(object? sender, EventArgs e)
    {
        if (dgvDiffResults.SelectedRows.Count == 0)
        {
            txtDiffDetail.Text = "";
            return;
        }

        var row = dgvDiffResults.SelectedRows[0];
        var rel = (string)row.Cells[0].Value!;
        var statusText = (string)row.Cells[1].Value!;

        if (!statusText.StartsWith("Değişti", StringComparison.Ordinal))
        {
            txtDiffDetail.Text =
                $"({statusText} — satır bazlı fark yalnızca 'Değişti' durumundaki dosyalar için gösterilir.)";
            return;
        }

        var left = txtDiffLeft.Text.Trim();
        var right = txtDiffRight.Text.Trim();
        var leftFile = Path.Combine(left, rel.Replace('/', Path.DirectorySeparatorChar));
        var rightFile = Path.Combine(right, rel.Replace('/', Path.DirectorySeparatorChar));

        if (!File.Exists(leftFile) || !File.Exists(rightFile))
        {
            txtDiffDetail.Text = "(Dosyalardan biri artık bulunamıyor.)";
            return;
        }

        if (DiffManager.LooksLikeBinary(leftFile) || DiffManager.LooksLikeBinary(rightFile))
        {
            txtDiffDetail.Text = "(İkili dosya — satır bazlı fark gösterilmiyor.)";
            return;
        }

        try
        {
            var dm = new DiffManager();
            txtDiffDetail.Text = dm.BuildLineDiff(leftFile, rightFile);
        }
        catch (Exception ex)
        {
            txtDiffDetail.Text = "(Fark gösterilemedi: " + ex.Message + ")";
        }
    }

    // ------------------------------------------------------------ Lifecycle
    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        LoadSettings();
        AppendLog("Uygulama hazır.", LogLevel.Info);
        AppendLog("Renk testi: uyarı mesajı.", LogLevel.Warning);
        AppendLog("Renk testi: hata mesajı.", LogLevel.Error);
        AppendLog("Renk testi: başarı mesajı.", LogLevel.Success);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        SaveSettings();
        _globalCts.Cancel();
        base.OnFormClosing(e);
    }

    private void LoadSettings()
    {
        try
        {
            var path = SettingsManager.GetDefaultPath();
            var settings = SettingsManager.Load(path);
            txtApktoolPath.Text = settings.ApktoolPath;
            txtJadxPath.Text = settings.JadxPath;
            txtApksignerPath.Text = settings.ApksignerPath;
            txtZipalignPath.Text = settings.ZipalignPath;
            txtKeytoolPath.Text = settings.KeytoolPath;
            txtAdbPath.Text = settings.AdbPath;
            if (!string.IsNullOrEmpty(settings.LastApkPath)) txtApkPath.Text = settings.LastApkPath;
            if (!string.IsNullOrEmpty(settings.LastOutputDir)) txtOutputDir.Text = settings.LastOutputDir;
            AppendLog("Ayarlar yüklendi: " + path, LogLevel.Debug);
        }
        catch (Exception ex)
        {
            AppendLog("Ayarlar yüklenemedi: " + ex.Message, LogLevel.Warning);
        }
    }

    private void SaveSettings()
    {
        try
        {
            var settings = new AppSettings
            {
                ApktoolPath = txtApktoolPath.Text.Trim(),
                JadxPath = txtJadxPath.Text.Trim(),
                ApksignerPath = txtApksignerPath.Text.Trim(),
                ZipalignPath = txtZipalignPath.Text.Trim(),
                KeytoolPath = txtKeytoolPath.Text.Trim(),
                AdbPath = txtAdbPath.Text.Trim(),
                LastApkPath = txtApkPath.Text.Trim(),
                LastOutputDir = txtOutputDir.Text.Trim()
            };
            SettingsManager.Save(settings, SettingsManager.GetDefaultPath());
        }
        catch
        {
            // Ayar kaydı başarısız olsa bile kapanışı engellememeli.
        }
    }
}

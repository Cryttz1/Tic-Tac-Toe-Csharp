using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

static class Program
{
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
    }
}

// ─── HLAVNÍ FORMULÁŘ (Uživatelské rozhraní) ──────────────────────────────────
class MainForm : Form
{
    static readonly Color BG         = Color.FromArgb(13,  17,  23);
    static readonly Color PANEL_BG   = Color.FromArgb(22,  27,  34);
    static readonly Color BORDER     = Color.FromArgb(48,  54,  61);
    static readonly Color X_COLOR    = Color.FromArgb(88, 166, 255);
    static readonly Color O_COLOR    = Color.FromArgb(255, 123, 114);
    static readonly Color TEXT_MUTED = Color.FromArgb(139, 148, 158);
    static readonly Color TEXT_LIGHT = Color.FromArgb(230, 237, 243);

    const int FORM_W    = 540;
    const int BOARD_X   = 20;
    const int BOARD_TOP = 444; // Posunuto dolů kvůli novému panelu
    const int CELL_SIZE = 110;
    const int GAP       = 8;
    const int BTN_H     = 40;
    const int BOTTOM    = 12;

    GameEngine engine;
    int scoreX = 0, scoreO = 0, draws = 0;

    CellButton[] cells = null!;
    Label lblTurn = null!, lblScoreX = null!, lblScoreO = null!, lblDraws = null!;
    Button btnReset = null!, btnResetAll = null!;
    Panel boardPanel = null!, sizePanel = null!, modePanel = null!, diffPanel = null!, startPanel = null!, timerPanel = null!;

    bool   lineActive = false;
    PointF lineFrom, lineTo;
    Color  lineColor;
    float  lineProgress = 0f;
    Timer? lineTimer;

    // Nové: Správa herního odpočtu
    Timer? turnTimer;
    int remainingSeconds;

    public MainForm()
    {
        engine = new GameEngine();
        
        engine.MoveMade += Engine_OnMoveMade;
        engine.GameWon += Engine_OnGameWon;
        engine.GameDrawn += Engine_OnGameDrawn;
        engine.TurnChanged += Engine_OnTurnChanged;

        // Inicializace tikání časovače
        turnTimer = new Timer { Interval = 1000 };
        turnTimer.Tick += TurnTimer_Tick;

        InitializeStaticUI();
        ApplySettings();
    }

    void InitializeStaticUI()
    {
        Text = "Neon Tic Tac Toe (Minimax Edition)";
        BackColor = BG;
        ForeColor = TEXT_LIGHT;
        Font = new Font("Segoe UI", 10f);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        DoubleBuffered = true;

        Controls.Add(new Label
        {
            Text = "TIC TAC TOE",
            Font = new Font("Segoe UI", 22f, FontStyle.Bold),
            ForeColor = TEXT_LIGHT, AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Bounds = new Rectangle(0, 14, FORM_W, 46),
            BackColor = Color.Transparent
        });

        // ─── Skóre
        var scoreBox = new Panel { Bounds = new Rectangle(BOARD_X, 68, FORM_W - BOARD_X * 2, 66), BackColor = PANEL_BG };
        scoreBox.Paint += (s, e) => DrawRoundedBorder(e.Graphics, scoreBox.ClientRectangle, BORDER, 10);
        Controls.Add(scoreBox);

        int sw = (scoreBox.Width - 2) / 3;
        lblScoreX = MakeScoreLabel("X\n0",      X_COLOR,    new Rectangle(0,      4, sw, 58));
        lblDraws  = MakeScoreLabel("REMÍZY\n0", TEXT_MUTED, new Rectangle(sw+1,   4, sw, 58));
        lblScoreO = MakeScoreLabel("O\n0",      O_COLOR,    new Rectangle(sw*2+2, 4, sw, 58));
        scoreBox.Controls.Add(lblScoreX);
        scoreBox.Controls.Add(lblDraws);
        scoreBox.Controls.Add(lblScoreO);
        scoreBox.Controls.Add(new Panel { Bounds = new Rectangle(sw,     8, 1, 50), BackColor = BORDER });
        scoreBox.Controls.Add(new Panel { Bounds = new Rectangle(sw*2+1, 8, 1, 50), BackColor = BORDER });

        // ─── Panel velikosti mřížky
        sizePanel = new Panel { Bounds = new Rectangle(BOARD_X, 144, FORM_W - BOARD_X * 2, 44), BackColor = Color.Transparent };
        Controls.Add(sizePanel);
        sizePanel.Controls.Add(new Label { Text = "Velikost:", Font = new Font("Segoe UI", 10f), ForeColor = TEXT_MUTED, AutoSize = true, Location = new Point(0, 12) });
        
        string[] sizeLabels = { "3 × 3", "4 × 4", "5 × 5" };
        int[]    gridSizes  = { 3, 4, 5 };
        for (int i = 0; i < 3; i++)
        {
            int gs = gridSizes[i];
            var btn = new ToggleButton(sizeLabels[i], gs == engine.GridSize) { Bounds = new Rectangle(90 + i * 104, 0, 92, 44), Tag = gs };
            btn.Click += (s, e) =>
            {
                if (s is ToggleButton tb && tb.Tag is int ns)
                {
                    if (ns == engine.GridSize) return;
                    engine.GridSize = ns;
                    ResetScores();
                    UpdateActiveButtons(sizePanel, ns);
                    ApplySettings();
                }
            };
            sizePanel.Controls.Add(btn);
        }

        // ─── Panel herního módu
        modePanel = new Panel { Bounds = new Rectangle(BOARD_X, 196, FORM_W - BOARD_X * 2, 44), BackColor = Color.Transparent };
        Controls.Add(modePanel);
        modePanel.Controls.Add(new Label { Text = "Režim:", Font = new Font("Segoe UI", 10f), ForeColor = TEXT_MUTED, AutoSize = true, Location = new Point(0, 12) });
        
        string[] modeLabels = { "1 Hráč (vs PC)", "2 Hráči" };
        bool[]   modeValues = { true, false };
        for (int i = 0; i < 2; i++)
        {
            bool isVsPc = modeValues[i];
            var btn = new ToggleButton(modeLabels[i], isVsPc == engine.VsComputer) { Bounds = new Rectangle(90 + i * 144, 0, 134, 44), Tag = isVsPc };
            btn.Click += (s, e) =>
            {
                if (s is ToggleButton tb && tb.Tag is bool vsPc)
                {
                    if (vsPc == engine.VsComputer) return;
                    engine.VsComputer = vsPc;
                    diffPanel.Visible = vsPc; 
                    ResetScores();
                    UpdateActiveButtons(modePanel, vsPc);
                    ApplySettings();
                }
            };
            modePanel.Controls.Add(btn);
        }

        // ─── Panel obtížnosti
        diffPanel = new Panel { Bounds = new Rectangle(BOARD_X, 248, FORM_W - BOARD_X * 2, 44), BackColor = Color.Transparent, Visible = engine.VsComputer };
        Controls.Add(diffPanel);
        diffPanel.Controls.Add(new Label { Text = "Obtížnost:", Font = new Font("Segoe UI", 10f), ForeColor = TEXT_MUTED, AutoSize = true, Location = new Point(0, 12) });
        
        string[] diffLabels = { "Lehká", "Střední", "Těžká" };
        GameEngine.Difficulty[] diffVals = { GameEngine.Difficulty.Lehka, GameEngine.Difficulty.Stredni, GameEngine.Difficulty.Tezka };
        
        for (int i = 0; i < 3; i++)
        {
            var dVal = diffVals[i];
            var btn = new ToggleButton(diffLabels[i], dVal == engine.AiDifficulty) { Bounds = new Rectangle(90 + i * 104, 0, 92, 44), Tag = dVal };
            btn.Click += (s, e) =>
            {
                if (s is ToggleButton tb && tb.Tag is GameEngine.Difficulty nd)
                {
                    if (nd == engine.AiDifficulty) return;
                    engine.AiDifficulty = nd;
                    ResetScores();
                    UpdateActiveButtons(diffPanel, nd);
                    ApplySettings();
                }
            };
            diffPanel.Controls.Add(btn);
        }

        // ─── Panel: Kdo začíná
        startPanel = new Panel { Bounds = new Rectangle(BOARD_X, 300, FORM_W - BOARD_X * 2, 44), BackColor = Color.Transparent };
        Controls.Add(startPanel);
        startPanel.Controls.Add(new Label { Text = "Začíná:", Font = new Font("Segoe UI", 10f), ForeColor = TEXT_MUTED, AutoSize = true, Location = new Point(0, 12) });
        
        string[] startLabels = { "Hráč X", "Hráč O" };
        char[] startVals = { 'X', 'O' };
        for (int i = 0; i < 2; i++)
        {
            char sVal = startVals[i];
            var btn = new ToggleButton(startLabels[i], sVal == engine.StartingPlayer) { Bounds = new Rectangle(90 + i * 104, 0, 92, 44), Tag = sVal };
            btn.Click += (s, e) =>
            {
                if (s is ToggleButton tb && tb.Tag is char ns)
                {
                    if (ns == engine.StartingPlayer) return;
                    engine.StartingPlayer = ns;
                    ResetScores();
                    UpdateActiveButtons(startPanel, ns);
                    ApplySettings();
                }
            };
            startPanel.Controls.Add(btn);
        }

        // ─── Nové: Panel nastavení časovače
        timerPanel = new Panel { Bounds = new Rectangle(BOARD_X, 352, FORM_W - BOARD_X * 2, 44), BackColor = Color.Transparent };
        Controls.Add(timerPanel);
        timerPanel.Controls.Add(new Label { Text = "Časovač:", Font = new Font("Segoe UI", 10f), ForeColor = TEXT_MUTED, AutoSize = true, Location = new Point(0, 12) });

        string[] timerLabels = { "Vypnuto", "3 s", "5 s", "10 s" };
        int[] timerVals = { 0, 3, 5, 10 };
        for (int i = 0; i < 4; i++)
        {
            int tVal = timerVals[i];
            var btn = new ToggleButton(timerLabels[i], tVal == engine.TurnTimeoutSeconds) { Bounds = new Rectangle(90 + i * 84, 0, 78, 44), Tag = tVal };
            btn.Click += (s, e) =>
            {
                if (s is ToggleButton tb && tb.Tag is int nt)
                {
                    if (nt == engine.TurnTimeoutSeconds) return;
                    engine.TurnTimeoutSeconds = nt;
                    ResetScores();
                    UpdateActiveButtons(timerPanel, nt);
                    ApplySettings();
                }
            };
            timerPanel.Controls.Add(btn);
        }

        // ─── Ukazatel tahu
        lblTurn = new Label
        {
            Text = "Hráč X je na tahu", Font = new Font("Segoe UI", 12f),
            ForeColor = X_COLOR, AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Bounds = new Rectangle(0, 404, FORM_W, 30), BackColor = Color.Transparent
        };
        Controls.Add(lblTurn);

        boardPanel = new Panel { BackColor = Color.Transparent };
        Controls.Add(boardPanel);

        // ─── Tlačítka resetu
        btnReset = MakeButton("Nová hra (stejné skóre)", Rectangle.Empty);
        btnReset.Click += (s, e) => { StopLine(); engine.ResetGame(); };
        Controls.Add(btnReset);

        btnResetAll = MakeButton("Resetovat vše", Rectangle.Empty);
        btnResetAll.ForeColor = TEXT_MUTED;
        btnResetAll.Click += (s, e) => { ResetScores(); StopLine(); engine.ResetGame(); };
        Controls.Add(btnResetAll);
    }

    void UpdateActiveButtons(Panel p, object activeTag)
    {
        foreach (Control c in p.Controls)
            if (c is ToggleButton tb) tb.SetActive(tb.Tag?.Equals(activeTag) == true);
    }

    void ApplySettings()
    {
        int n  = engine.GridSize;
        int bW = FORM_W - BOARD_X * 2;
        int bH = n * CELL_SIZE + (n - 1) * GAP;
        int btnY = BOARD_TOP + bH + 12;

        MinimumSize = MaximumSize = Size.Empty;
        ClientSize = new Size(FORM_W, btnY + BTN_H + BOTTOM);
        MinimumSize = MaximumSize = Size;

        boardPanel.Bounds = new Rectangle(BOARD_X, BOARD_TOP, bW, bH);
        int halfW = (bW - 8) / 2;
        btnReset.Bounds    = new Rectangle(BOARD_X,             btnY, halfW, BTN_H);
        btnResetAll.Bounds = new Rectangle(BOARD_X + halfW + 8, btnY, halfW, BTN_H);

        StopLine();
        BuildCells(n, bW, bH);
        engine.ResetGame();
    }

    void BuildCells(int n, int bW, int bH)
    {
        boardPanel.Controls.Clear();
        cells = new CellButton[n * n];
        int cellW = (bW - GAP * (n - 1)) / n;
        int cellH = (bH - GAP * (n - 1)) / n;
        for (int i = 0; i < n * n; i++)
        {
            int col = i % n, row = i / n;
            var cell = new CellButton(i)
            {
                Bounds = new Rectangle(col * (cellW + GAP), row * (cellH + GAP), cellW, cellH),
                DrawOverlay = DrawWinningLine
            };
            cell.CellClicked += (idx) => engine.Play(idx);
            cells[i] = cell;
            boardPanel.Controls.Add(cell);
        }
    }

    void Engine_OnMoveMade(int index, char player)
    {
        AudioHelper.PlayClick();
        Color pc = player == 'X' ? X_COLOR : O_COLOR;
        cells[index].SetSymbol(player, pc);
    }

    // Nové: Logika tikání odpočtu kaźdou sekundu
    void TurnTimer_Tick(object? sender, EventArgs e)
    {
        if (engine.TurnTimeoutSeconds <= 0 || engine.IsGameOver) return;

        remainingSeconds--;
        UpdateTurnLabel();

        if (remainingSeconds <= 0)
        {
            turnTimer?.Stop();
            engine.ForceRandomMove(); // Čas vypršel -> engine vybere náhodný tah
        }
    }

    // Nové: Sjednocené překreslování stavového textu
    void UpdateTurnLabel()
    {
        char player = engine.CurrentPlayer;
        int wLen = engine.WinLength;
        string goal = engine.GridSize > 3 ? $" (Cíl: Spoj {wLen})" : "";
        
        bool isAiThinking = engine.VsComputer && player == 'O';
        string timerStr = (engine.TurnTimeoutSeconds > 0 && !isAiThinking) ? $" [{remainingSeconds}s]" : "";

        if (isAiThinking)
        {
            lblTurn.Text = "🤖 Počítač přemýšlí...";
        }
        else
        {
            lblTurn.Text = $"Hráč {player} je na tahu" + goal + timerStr;
        }
        lblTurn.ForeColor = player == 'X' ? X_COLOR : O_COLOR;
    }

    void Engine_OnTurnChanged(char player)
    {
        turnTimer?.Stop();
        
        // Spustíme odpočet pouze pokud je zapnutý, hra neskončila a zrovna nehraje AI
        bool isAiTurn = engine.VsComputer && player == 'O';
        if (engine.TurnTimeoutSeconds > 0 && !engine.IsGameOver && !isAiTurn)
        {
            remainingSeconds = engine.TurnTimeoutSeconds;
            turnTimer?.Start();
        }

        UpdateTurnLabel();
    }

    void Engine_OnGameWon(int[] winLine, char winner)
    {
        turnTimer?.Stop();
        if (winner == 'X') scoreX++; else scoreO++;
        UpdateScoreLabels();
        AudioHelper.PlayWin();

        lblTurn.Text = $"🎉  Hráč {winner} vyhrál!";
        Color wColor = winner == 'X' ? X_COLOR : O_COLOR;
        lblTurn.ForeColor = wColor;

        for (int i = 0; i < cells.Length; i++)
            if (!winLine.Contains(i)) cells[i].Dim();

        StartLine(winLine, wColor);
    }

    void Engine_OnGameDrawn()
    {
        turnTimer?.Stop();
        draws++;
        UpdateScoreLabels();
        AudioHelper.PlayDraw();
        lblTurn.Text = "🤝  Remíza!";
        lblTurn.ForeColor = TEXT_MUTED;
    }

    void ResetScores() { scoreX = scoreO = draws = 0; UpdateScoreLabels(); }

    void UpdateScoreLabels()
    {
        lblScoreX.Text = $"X\n{scoreX}"; lblScoreO.Text = $"O\n{scoreO}"; lblDraws.Text  = $"REMÍZY\n{draws}";
    }

    public void DrawWinningLine(Graphics g, int offsetX, int offsetY)
    {
        if (!lineActive || lineProgress <= 0f) return;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TranslateTransform(offsetX, offsetY);

        float ex = lineFrom.X + (lineTo.X - lineFrom.X) * lineProgress;
        float ey = lineFrom.Y + (lineTo.Y - lineFrom.Y) * lineProgress;
        var end = new PointF(ex, ey);
        var c = lineColor;

        using (var g1 = new Pen(Color.FromArgb(15, c.R, c.G, c.B), 40f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLine(g1, lineFrom, end);
        using (var g2 = new Pen(Color.FromArgb(35, c.R, c.G, c.B), 26f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLine(g2, lineFrom, end);
        using (var g3 = new Pen(Color.FromArgb(120, c.R, c.G, c.B), 14f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLine(g3, lineFrom, end);
        using (var g4 = new Pen(Color.FromArgb(255, 255, 255, 255), 3f)  { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLine(g4, lineFrom, end);

        g.TranslateTransform(-offsetX, -offsetY);
    }

    void StartLine(int[] line, Color color)
    {
        int n = engine.GridSize;
        int bW = boardPanel.Width, bH = boardPanel.Height;
        int cellW = (bW - GAP * (n - 1)) / n;
        int cellH = (bH - GAP * (n - 1)) / n;
        int fi = line[0], li = line[line[line.Length - 1] == 0 ? 0 : line.Length - 1]; 

        float bx0 = (fi % n) * (cellW + GAP) + cellW / 2f;
        float by0 = (fi / n) * (cellH + GAP) + cellH / 2f;
        float bx1 = (li % n) * (cellW + GAP) + cellW / 2f;
        float by1 = (li / n) * (cellH + GAP) + cellH / 2f;

        lineFrom  = new PointF(boardPanel.Left + bx0, boardPanel.Top + by0);
        lineTo    = new PointF(boardPanel.Left + bx1, boardPanel.Top + by1);
        lineColor = color;
        lineProgress = 0f;
        lineActive = true;

        lineTimer?.Stop();
        lineTimer = new Timer { Interval = 16 };
        lineTimer.Tick += (s, e) =>
        {
            lineProgress = Math.Min(1f, lineProgress + 0.05f);
            Invalidate(true);
            if (lineProgress >= 1f) lineTimer.Stop();
        };
        lineTimer.Start();
    }

    void StopLine()
    {
        turnTimer?.Stop();
        lineTimer?.Stop();
        lineActive = false;
        lineProgress = 0f;
        if (cells != null) foreach (var c in cells) c.Reset();
        Invalidate(true);
    }

    protected override void OnPaint(PaintEventArgs e) { base.OnPaint(e); DrawWinningLine(e.Graphics, 0, 0); }

    Label MakeScoreLabel(string text, Color color, Rectangle bounds) => new Label { Text = text, Font = new Font("Segoe UI", 13f, FontStyle.Bold), ForeColor = color, BackColor = Color.Transparent, TextAlign = ContentAlignment.MiddleCenter, AutoSize = false, Bounds = bounds };

    Button MakeButton(string text, Rectangle bounds)
    {
        var btn = new FlatButton { Text = text, Bounds = bounds, ForeColor = TEXT_LIGHT, BackColor = PANEL_BG, Font = new Font("Segoe UI", 10f), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
        btn.FlatAppearance.BorderColor = BORDER; btn.FlatAppearance.BorderSize = 1; btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(35, 40, 50);
        return btn;
    }

    static void DrawRoundedBorder(Graphics g, Rectangle r, Color color, int radius)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(color, 1f); using var path = RoundRect(r, radius);
        g.DrawPath(pen, path);
    }

    public static GraphicsPath RoundRect(Rectangle r, int radius)
    {
        var p = new GraphicsPath();
        p.AddArc(r.X, r.Y, radius * 2, radius * 2, 180, 90);
        p.AddArc(r.Right - radius * 2, r.Y, radius * 2, radius * 2, 270, 90);
        p.AddArc(r.Right - radius * 2, r.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
        p.AddArc(r.X, r.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
        p.CloseFigure(); return p;
    }
}

// ─── ZVUKOVÝ MANAŽER ─────────────────────────────────────────────────────────
static class AudioHelper
{
    public static void PlayClick() => Task.Run(() => Console.Beep(800, 40));
    public static void PlayWin() => Task.Run(() => { Console.Beep(523, 100); Console.Beep(659, 100); Console.Beep(784, 100); Console.Beep(1046, 250); });
    public static void PlayDraw() => Task.Run(() => { Console.Beep(300, 200); Console.Beep(250, 300); });
}

// ─── HERNÍ LOGIKA (Plně Thread-Safe) ─────────────────────────────────────────
class GameEngine
{
    public enum Difficulty { Lehka, Stredni, Tezka }
    
    public int GridSize { get; set; } = 3;
    public bool VsComputer { get; set; } = true;
    public Difficulty AiDifficulty { get; set; } = Difficulty.Stredni;
    public char StartingPlayer { get; set; } = 'X';
    
    // Nové: Vlastnosti pro veřejný přístup z MainForm
    public int TurnTimeoutSeconds { get; set; } = 0; // 0 znamená vypnuto
    public char CurrentPlayer => currentPlayer;
    public bool IsGameOver => gameOver;
    public bool IsAiThinking => isAiThinking;
    public int WinLength => GridSize == 3 ? 3 : 4; 

    char[] board = Array.Empty<char>();
    char currentPlayer = 'X';
    bool gameOver = false;
    bool isAiThinking = false;
    int gameVersion = 0; 

    public event Action<int, char>? MoveMade;
    public event Action<int[], char>? GameWon;
    public event Action? GameDrawn;
    public event Action<char>? TurnChanged;

    public async void ResetGame()
    {
        gameVersion++; 
        board = new char[GridSize * GridSize];
        currentPlayer = StartingPlayer;
        gameOver = false;
        isAiThinking = false;
        TurnChanged?.Invoke(currentPlayer);

        if (VsComputer && currentPlayer == 'O')
        {
            await TriggerComputerMoveAsync();
        }
    }

    public async void Play(int index)
    {
        if (gameOver || isAiThinking || board[index] != '\0') return;

        MakeMove(index);

        if (!gameOver && VsComputer && currentPlayer == 'O')
        {
            await TriggerComputerMoveAsync();
        }
    }

    // Nové: Metoda pro vynucení náhodného tahu při vypršení časovače
    public void ForceRandomMove()
    {
        if (gameOver || isAiThinking) return;

        Random localRnd = new Random();
        int move = GetRandomMove(board, localRnd);
        if (move != -1 && board[move] == '\0')
        {
            MakeMove(move);
        }
    }

    async Task TriggerComputerMoveAsync()
    {
        isAiThinking = true;
        await Task.Delay(150); 
        
        int currentVersion = gameVersion;
        char[] boardCopy = (char[])board.Clone(); 
        
        int move = await Task.Run(() => ComputeBestMove(boardCopy)); 
        
        if (currentVersion == gameVersion && !gameOver)
        {
            if(move != -1 && board[move] == '\0') MakeMove(move);
        }
        isAiThinking = false;
    }

    void MakeMove(int index)
    {
        board[index] = currentPlayer;
        MoveMade?.Invoke(index, currentPlayer);

        int[]? winLine = CheckWin(board);
        if (winLine != null)
        {
            gameOver = true;
            GameWon?.Invoke(winLine, currentPlayer);
        }
        else if (board.All(c => c != '\0'))
        {
            gameOver = true;
            GameDrawn?.Invoke();
        }
        else
        {
            currentPlayer = currentPlayer == 'X' ? 'O' : 'X';
            TurnChanged?.Invoke(currentPlayer);
        }
    }

    int ComputeBestMove(char[] currentBoard)
    {
        Random localRnd = new Random(); 

        if (AiDifficulty == Difficulty.Lehka) return GetRandomMove(currentBoard, localRnd);

        int move = FindImmediateMove(currentBoard, 'O'); 
        if (move != -1) return move;
        move = FindImmediateMove(currentBoard, 'X'); 
        if (move != -1) return move;

        if (AiDifficulty == Difficulty.Stredni) return GetRandomMove(currentBoard, localRnd);

        int size = (int)Math.Sqrt(currentBoard.Length);
        int maxDepth = size == 3 ? 9 : (size == 4 ? 4 : 3);
        return GetMinimaxMove(currentBoard, maxDepth, localRnd);
    }

    int GetRandomMove(char[] currentBoard, Random rnd)
    {
        var empty = currentBoard.Select((c, i) => new { c, i }).Where(x => x.c == '\0').Select(x => x.i).ToList();
        return empty.Count > 0 ? empty[rnd.Next(empty.Count)] : -1;
    }

    int FindImmediateMove(char[] currentBoard, char playerSymbol)
    {
        for (int i = 0; i < currentBoard.Length; i++)
        {
            if (currentBoard[i] == '\0')
            {
                currentBoard[i] = playerSymbol; 
                bool isWinning = CheckWin(currentBoard) != null;
                currentBoard[i] = '\0'; 
                if (isWinning) return i;
            }
        }
        return -1;
    }

    int GetMinimaxMove(char[] currentBoard, int maxDepth, Random rnd)
    {
        int bestScore = int.MinValue;
        List<int> bestMoves = new List<int>();
        int alpha = int.MinValue;
        int beta = int.MaxValue;
        
        for (int i = 0; i < currentBoard.Length; i++)
        {
            if (currentBoard[i] == '\0')
            {
                currentBoard[i] = 'O';
                int score = MinimaxLoop(currentBoard, 0, false, alpha, beta, maxDepth);
                currentBoard[i] = '\0';
                
                if (score > bestScore)
                {
                    bestScore = score;
                    bestMoves.Clear();
                    bestMoves.Add(i);
                }
                else if (score == bestScore)
                {
                    bestMoves.Add(i);
                }
            }
        }
        return bestMoves.Count > 0 ? bestMoves[rnd.Next(bestMoves.Count)] : GetRandomMove(currentBoard, rnd);
    }

    int MinimaxLoop(char[] tempBoard, int depth, bool isMaximizing, int alpha, int beta, int maxDepth)
    {
        int[]? win = CheckWin(tempBoard);
        if (win != null)
        {
            char winner = tempBoard[win[0]];
            if (winner == 'O') return 10000 - depth;
            if (winner == 'X') return -10000 + depth;
        }
        if (tempBoard.All(c => c != '\0')) return 0; 
        
        if (depth >= maxDepth) return EvaluateBoard(tempBoard);

        if (isMaximizing)
        {
            int maxEval = int.MinValue;
            for (int i = 0; i < tempBoard.Length; i++)
            {
                if (tempBoard[i] == '\0')
                {
                    tempBoard[i] = 'O';
                    int eval = MinimaxLoop(tempBoard, depth + 1, false, alpha, beta, maxDepth);
                    tempBoard[i] = '\0';
                    maxEval = Math.Max(maxEval, eval);
                    alpha = Math.Max(alpha, eval);
                    if (beta <= alpha) break; 
                }
            }
            return maxEval;
        }
        else
        {
            int minEval = int.MaxValue;
            for (int i = 0; i < tempBoard.Length; i++)
            {
                if (tempBoard[i] == '\0')
                {
                    tempBoard[i] = 'X';
                    int eval = MinimaxLoop(tempBoard, depth + 1, true, alpha, beta, maxDepth);
                    tempBoard[i] = '\0';
                    minEval = Math.Min(minEval, eval);
                    beta = Math.Min(beta, eval);
                    if (beta <= alpha) break; 
                }
            }
            return minEval;
        }
    }

    int EvaluateBoard(char[] state)
    {
        int score = 0;
        int size = (int)Math.Sqrt(state.Length);
        int winLen = size == 3 ? 3 : 4;
        int[] dx = { 1, 0, 1, -1 };
        int[] dy = { 0, 1, 1, 1 };

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                for (int d = 0; d < 4; d++)
                {
                    int oCount = 0;
                    int xCount = 0;
                    bool outOfBounds = false;

                    for (int k = 0; k < winLen; k++)
                    {
                        int nx = x + dx[d] * k;
                        int ny = y + dy[d] * k;
                        if (nx < 0 || ny < 0 || nx >= size || ny >= size) { outOfBounds = true; break; }
                        
                        char c = state[ny * size + nx];
                        if (c == 'O') oCount++;
                        else if (c == 'X') xCount++;
                    }

                    if (outOfBounds) continue;

                    if (oCount > 0 && xCount == 0)
                    {
                        if (oCount == winLen - 1) score += 50;
                        else if (oCount == winLen - 2) score += 10;
                        else score += 1;
                    }
                    else if (xCount > 0 && oCount == 0)
                    {
                        if (xCount == winLen - 1) score -= 60; 
                        else if (xCount == winLen - 2) score -= 12;
                        else score -= 1;
                    }
                }
            }
        }
        return score;
    }

    int[]? CheckWin(char[] state)
    {
        int size = (int)Math.Sqrt(state.Length);
        int winLen = size == 3 ? 3 : 4;
        int[] dx = { 1, 0, 1, -1 };
        int[] dy = { 0, 1, 1, 1 };

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int idx = y * size + x;
                if (state[idx] == '\0') continue;
                char c = state[idx];

                for (int d = 0; d < 4; d++)
                {
                    int[] line = new int[winLen];
                    bool match = true;
                    for (int k = 0; k < winLen; k++)
                    {
                        int nx = x + dx[d] * k;
                        int ny = y + dy[d] * k;
                        if (nx < 0 || ny < 0 || nx >= size || ny >= size) { match = false; break; }
                        int nidx = ny * size + nx;
                        if (state[nidx] != c) { match = false; break; }
                        line[k] = nidx;
                    }
                    if (match) return line;
                }
            }
        }
        return null;
    }
}

// ─── CUSTOM CONTROLS ─────────────────────────────────────────────────────────
class CellButton : Control
{
    static readonly Color BG_NORMAL  = Color.FromArgb(22, 27, 34);
    static readonly Color BG_HOVER   = Color.FromArgb(30, 38, 50);
    static readonly Color BORDER_CLR = Color.FromArgb(48, 54, 61);

    public event Action<int>? CellClicked;
    public Action<Graphics, int, int>? DrawOverlay;

    readonly int index;
    char symbol = '\0';
    Color symbolColor = Color.White;
    bool isHovered = false;
    bool isDimmed = false;
    float drawProgress = 0f;
    Timer? drawTimer;

    public CellButton(int index)
    {
        this.index = index;
        DoubleBuffered = true; Cursor = Cursors.Hand;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }

    public void SetSymbol(char sym, Color color)
    {
        symbol = sym; symbolColor = color; drawProgress = 0f;
        drawTimer?.Stop();
        drawTimer = new Timer { Interval = 16 };
        drawTimer.Tick += (s, e) => { drawProgress = Math.Min(1f, drawProgress + 0.08f); Invalidate(); if (drawProgress >= 1f) drawTimer?.Stop(); };
        drawTimer.Start();
    }

    public void Dim() { isDimmed = true; isHovered = false; Invalidate(); }
    public void Reset() { symbol = '\0'; drawProgress = 0f; isDimmed = false; isHovered = false; drawTimer?.Stop(); Invalidate(); }

    protected override void OnMouseEnter(EventArgs e) { if(!isDimmed) isHovered = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { isHovered = false; Invalidate(); }
    protected override void OnClick(EventArgs e) { if (!isDimmed) CellClicked?.Invoke(index); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        var r = ClientRectangle;

        Color bg = isHovered && symbol == '\0' ? BG_HOVER : BG_NORMAL;
        using (var b = new SolidBrush(bg)) using (var path = MainForm.RoundRect(r, 10)) g.FillPath(b, path);
        using (var pen = new Pen(BORDER_CLR, 1f)) using (var path = MainForm.RoundRect(r, 10)) g.DrawPath(pen, path);

        if (symbol != '\0')
        {
            float cx = r.Width / 2f, cy = r.Height / 2f;
            float size = Math.Min(r.Width, r.Height) * 0.32f;
            float sw = Math.Max(2.5f, size * 0.15f);

            int alpha = isDimmed ? 30 : (int)(255 * drawProgress);
            using var pen2 = new Pen(Color.FromArgb(alpha, symbolColor), sw) { StartCap = LineCap.Round, EndCap = LineCap.Round };

            if (symbol == 'X')
            {
                if (drawProgress <= 0.5f && !isDimmed)
                {
                    float p = drawProgress * 2f;
                    g.DrawLine(pen2, cx - size, cy - size, cx - size + 2 * size * p, cy - size + 2 * size * p);
                }
                else
                {
                    float p2 = isDimmed ? 1f : (drawProgress - 0.5f) * 2f;
                    g.DrawLine(pen2, cx - size, cy - size, cx + size, cy + size);
                    g.DrawLine(pen2, cx + size, cy - size, cx + size - 2 * size * p2, cy - size + 2 * size * p2);
                }
            }
            else
            {
                g.DrawArc(pen2, cx - size, cy - size, size * 2, size * 2, -90, isDimmed ? 360f : 360f * drawProgress);
            }
        }

        if (Parent != null && !isDimmed) DrawOverlay?.Invoke(g, -(Parent.Left + Left), -(Parent.Top + Top));
    }
}

class ToggleButton : Control
{
    static readonly Color BG_ACT = Color.FromArgb(28, 78, 148);
    static readonly Color BG_IN  = Color.FromArgb(22, 27, 34);
    static readonly Color BD_ACT = Color.FromArgb(88, 166, 255);
    static readonly Color BD_IN  = Color.FromArgb(48, 54, 61);
    static readonly Color TX_ACT = Color.FromArgb(230, 237, 243);
    static readonly Color TX_IN  = Color.FromArgb(139, 148, 158);

    bool active;
    readonly string label;

    public ToggleButton(string label, bool active)
    {
        this.label = label; this.active = active;
        DoubleBuffered = true; Cursor = Cursors.Hand;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }

    public void SetActive(bool v) { active = v; Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        var r = ClientRectangle;
        using (var b = new SolidBrush(active ? BG_ACT : BG_IN)) using (var path = MainForm.RoundRect(r, 8)) g.FillPath(b, path);
        using (var pen = new Pen(active ? BD_ACT : BD_IN, active ? 1.5f : 1f)) using (var path = MainForm.RoundRect(r, 8)) g.DrawPath(pen, path);
        using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        using var font = new Font("Segoe UI", 10f, active ? FontStyle.Bold : FontStyle.Regular);
        g.DrawString(label, font, new SolidBrush(active ? TX_ACT : TX_IN), r, sf);
    }
}

class FlatButton : Button
{
    public FlatButton() { DoubleBuffered = true; }
    protected override bool ShowFocusCues => false;
}
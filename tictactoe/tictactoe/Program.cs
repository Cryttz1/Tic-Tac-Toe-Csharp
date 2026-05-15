using System;
using System.Drawing;
using System.Drawing.Drawing2D;
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

class MainForm : Form
{
    // ── Barvy ─────────────────────────────────────────────────────────────────
    static readonly Color BG         = Color.FromArgb(13,  17,  23);
    static readonly Color PANEL_BG   = Color.FromArgb(22,  27,  34);
    static readonly Color BORDER     = Color.FromArgb(48,  54,  61);
    static readonly Color X_COLOR    = Color.FromArgb(88, 166, 255);
    static readonly Color O_COLOR    = Color.FromArgb(255, 123, 114);
    static readonly Color TEXT_MUTED = Color.FromArgb(139, 148, 158);
    static readonly Color TEXT_LIGHT = Color.FromArgb(230, 237, 243);

    // ── Layout (fixní části) ───────────────────────────────────────────────────
    const int FORM_W    = 540;   // šířka okna
    const int BOARD_X   = 20;   // levý/pravý okraj desky
    const int BOARD_TOP = 234;  // Y souřadnice horního okraje desky
    const int CELL_SIZE = 110;  // výška i šířka jedné buňky (čtvercová)
    const int GAP       = 8;    // mezera mezi buňkami
    const int BTN_H     = 40;   // výška dolních tlačítek
    const int BOTTOM    = 12;   // mezera pod tlačítky

    // ── Stav hry ──────────────────────────────────────────────────────────────
    int gridSize = 3;
    char[] board;
    char currentPlayer = 'X';
    bool gameOver = false;
    int scoreX = 0, scoreO = 0, draws = 0;

    // ── UI prvky ──────────────────────────────────────────────────────────────
    CellButton[] cells;
    Label lblTurn, lblScoreX, lblScoreO, lblDraws;
    Button btnReset, btnResetAll;
    WinLinePanel boardPanel;
    Panel sizePanel;

    public MainForm()
    {
        InitializeStaticUI();
        ApplyGridSize();
    }

    // Vypočítá výšku okna pro daný gridSize
    int FormHeight(int n) =>
        BOARD_TOP + n * CELL_SIZE + (n - 1) * GAP + BTN_H + 16 + BOTTOM;

    // ── Statické UI (kreslí se jednou) ────────────────────────────────────────
    void InitializeStaticUI()
    {
        Text = "Tic Tac Toe";
        BackColor = BG;
        ForeColor = TEXT_LIGHT;
        Font = new Font("Segoe UI", 10f);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        DoubleBuffered = true;

        // Titulek
        Controls.Add(new Label
        {
            Text = "TIC TAC TOE",
            Font = new Font("Segoe UI", 22f, FontStyle.Bold),
            ForeColor = TEXT_LIGHT,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Bounds = new Rectangle(0, 14, FORM_W, 46),
            BackColor = Color.Transparent
        });

        // Score panel
        var scoreBox = new Panel { Bounds = new Rectangle(BOARD_X, 68, FORM_W - BOARD_X * 2, 66), BackColor = PANEL_BG };
        scoreBox.Paint += (s, e) => DrawRoundedBorder(e.Graphics, scoreBox.ClientRectangle, BORDER, 10);
        Controls.Add(scoreBox);

        int sw = (scoreBox.Width - 2) / 3;
        lblScoreX = MakeScoreLabel("X\n0",      X_COLOR,    new Rectangle(0,       4, sw, 58));
        lblDraws  = MakeScoreLabel("REMÍZY\n0", TEXT_MUTED, new Rectangle(sw + 1,  4, sw, 58));
        lblScoreO = MakeScoreLabel("O\n0",      O_COLOR,    new Rectangle(sw*2+2,  4, sw, 58));
        scoreBox.Controls.Add(lblScoreX);
        scoreBox.Controls.Add(lblDraws);
        scoreBox.Controls.Add(lblScoreO);
        scoreBox.Controls.Add(new Panel { Bounds = new Rectangle(sw,     8, 1, 50), BackColor = BORDER });
        scoreBox.Controls.Add(new Panel { Bounds = new Rectangle(sw*2+1, 8, 1, 50), BackColor = BORDER });

        // Výběr velikosti desky
        sizePanel = new Panel { Bounds = new Rectangle(BOARD_X, 144, FORM_W - BOARD_X * 2, 44), BackColor = Color.Transparent };
        Controls.Add(sizePanel);
        sizePanel.Controls.Add(new Label
        {
            Text = "Velikost:",
            Font = new Font("Segoe UI", 10f),
            ForeColor = TEXT_MUTED,
            AutoSize = true,
            Location = new Point(0, 12),
            BackColor = Color.Transparent
        });
        string[] sizeLabels = { "3 × 3", "4 × 4", "5 × 5" };
        int[] gridSizes = { 3, 4, 5 };
        for (int i = 0; i < 3; i++)
        {
            int gs = gridSizes[i];
            var btn = new SizeButton(sizeLabels[i], gs == gridSize)
            {
                Bounds = new Rectangle(90 + i * 104, 0, 92, 44),
                Tag = gs
            };
            btn.Click += (s, e) =>
            {
                int ns = (int)((SizeButton)s).Tag;
                if (ns == gridSize) return;
                gridSize = ns;
                scoreX = scoreO = draws = 0;
                UpdateScoreLabels();
                foreach (Control c in sizePanel.Controls)
                    if (c is SizeButton sb) sb.SetActive((int)sb.Tag == gridSize);
                ApplyGridSize();
            };
            sizePanel.Controls.Add(btn);
        }

        // Status label
        lblTurn = new Label
        {
            Text = "Hráč X je na tahu",
            Font = new Font("Segoe UI", 12f),
            ForeColor = X_COLOR,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Bounds = new Rectangle(0, 196, FORM_W, 30),
            BackColor = Color.Transparent
        };
        Controls.Add(lblTurn);

        // Herní panel — velikost se nastaví v ApplyGridSize()
        boardPanel = new WinLinePanel { BackColor = Color.Transparent };
        Controls.Add(boardPanel);

        // Tlačítka — Y pozice se nastaví v ApplyGridSize()
        btnReset = MakeButton("Nová hra (stejné skóre)", Rectangle.Empty);
        btnReset.Click += (s, e) => ResetBoard();
        Controls.Add(btnReset);

        btnResetAll = MakeButton("Resetovat vše", Rectangle.Empty);
        btnResetAll.ForeColor = TEXT_MUTED;
        btnResetAll.Click += (s, e) => { scoreX = scoreO = draws = 0; UpdateScoreLabels(); ResetBoard(); };
        Controls.Add(btnResetAll);
    }

    // ── Překreslí layout + desku pro aktuální gridSize ────────────────────────
    void ApplyGridSize()
    {
        int n    = gridSize;
        int bW   = FORM_W - BOARD_X * 2;           // šířka boardPanelu
        int bH   = n * CELL_SIZE + (n - 1) * GAP;  // výška boardPanelu
        int btnY = BOARD_TOP + bH + 12;
        int formH = btnY + BTN_H + BOTTOM + (Height - ClientSize.Height); // kompenzace dekorace

        // Změna velikosti okna
        int newH = BOARD_TOP + bH + 12 + BTN_H + BOTTOM;
        MinimumSize = Size.Empty;
        MaximumSize = Size.Empty;
        ClientSize = new Size(FORM_W, newH);
        MinimumSize = MaximumSize = Size;

        // Přemístění boardPanelu
        boardPanel.Bounds = new Rectangle(BOARD_X, BOARD_TOP, bW, bH);
        boardPanel.ClearLine();

        // Přemístění tlačítek
        int halfW = (FORM_W - BOARD_X * 2 - 8) / 2;
        btnReset.Bounds    = new Rectangle(BOARD_X,              btnY, halfW, BTN_H);
        btnResetAll.Bounds = new Rectangle(BOARD_X + halfW + 8,  btnY, halfW, BTN_H);

        // Sestav buňky
        BuildCells(n, bW, bH);

        // Reset stavu
        currentPlayer = 'X';
        gameOver = false;
        board = new char[n * n];
        lblTurn.Text = "Hráč X je na tahu";
        lblTurn.ForeColor = X_COLOR;
    }

    void BuildCells(int n, int bW, int bH)
    {
        boardPanel.Controls.Clear();
        cells = new CellButton[n * n];

        // Přepočítej skutečnou velikost buněk tak, aby přesně zaplnily panel
        int cellW = (bW - GAP * (n - 1)) / n;
        int cellH = (bH - GAP * (n - 1)) / n;

        for (int i = 0; i < n * n; i++)
        {
            int col = i % n, row = i / n;
            var cell = new CellButton(i)
            {
                Bounds = new Rectangle(col * (cellW + GAP), row * (cellH + GAP), cellW, cellH)
            };
            cell.CellClicked += OnCellClicked;
            cells[i] = cell;
            boardPanel.Controls.Add(cell);
        }
    }

    // ── Herní logika ──────────────────────────────────────────────────────────
    void OnCellClicked(int index)
    {
        if (gameOver || board[index] != '\0') return;

        board[index] = currentPlayer;
        Color pc = currentPlayer == 'X' ? X_COLOR : O_COLOR;
        cells[index].SetSymbol(currentPlayer, pc);

        int[] winLine = CheckWin();
        if (winLine != null)
        {
            gameOver = true;
            if (currentPlayer == 'X') scoreX++; else scoreO++;
            UpdateScoreLabels();
            foreach (int i in winLine) cells[i].SetWinner(pc);
            AnimateWinLine(winLine, pc);
            lblTurn.Text = $"🎉  Hráč {currentPlayer} vyhrál!";
            lblTurn.ForeColor = pc;
        }
        else if (IsDraw())
        {
            gameOver = true; draws++;
            UpdateScoreLabels();
            lblTurn.Text = "🤝  Remíza!";
            lblTurn.ForeColor = TEXT_MUTED;
        }
        else
        {
            currentPlayer = currentPlayer == 'X' ? 'O' : 'X';
            lblTurn.Text = $"Hráč {currentPlayer} je na tahu";
            lblTurn.ForeColor = currentPlayer == 'X' ? X_COLOR : O_COLOR;
        }
    }

    void ResetBoard()
    {
        int n = gridSize;
        board = new char[n * n];
        currentPlayer = 'X';
        gameOver = false;
        boardPanel.ClearLine();
        foreach (var c in cells) c.Reset();
        lblTurn.Text = "Hráč X je na tahu";
        lblTurn.ForeColor = X_COLOR;
    }

    int[] CheckWin()
    {
        int n = gridSize;
        for (int r = 0; r < n; r++)
        {
            var line = new int[n];
            for (int c = 0; c < n; c++) line[c] = r * n + c;
            if (AllSame(line)) return line;
        }
        for (int c = 0; c < n; c++)
        {
            var line = new int[n];
            for (int r = 0; r < n; r++) line[r] = r * n + c;
            if (AllSame(line)) return line;
        }
        var d1 = new int[n]; for (int i = 0; i < n; i++) d1[i] = i * n + i;
        if (AllSame(d1)) return d1;
        var d2 = new int[n]; for (int i = 0; i < n; i++) d2[i] = i * n + (n - 1 - i);
        if (AllSame(d2)) return d2;
        return null;
    }

    bool AllSame(int[] idx)
    {
        char f = board[idx[0]];
        if (f == '\0') return false;
        foreach (int i in idx) if (board[i] != f) return false;
        return true;
    }

    bool IsDraw() { foreach (var c in board) if (c == '\0') return false; return true; }

    void AnimateWinLine(int[] line, Color color)
    {
        int n = gridSize;
        int bW = boardPanel.Width, bH = boardPanel.Height;
        int cellW = (bW - GAP * (n - 1)) / n;
        int cellH = (bH - GAP * (n - 1)) / n;

        int fi = line[0], li = line[line.Length - 1];
        float x0 = (fi % n) * (cellW + GAP) + cellW / 2f;
        float y0 = (fi / n) * (cellH + GAP) + cellH / 2f;
        float x1 = (li % n) * (cellW + GAP) + cellW / 2f;
        float y1 = (li / n) * (cellH + GAP) + cellH / 2f;

        boardPanel.StartLine(new PointF(x0, y0), new PointF(x1, y1), color);
    }

    void UpdateScoreLabels()
    {
        lblScoreX.Text = $"X\n{scoreX}";
        lblScoreO.Text = $"O\n{scoreO}";
        lblDraws.Text  = $"REMÍZY\n{draws}";
    }

    // ── Pomocné továrny ───────────────────────────────────────────────────────
    Label MakeScoreLabel(string text, Color color, Rectangle bounds) => new Label
    {
        Text = text, Font = new Font("Segoe UI", 13f, FontStyle.Bold),
        ForeColor = color, BackColor = Color.Transparent,
        TextAlign = ContentAlignment.MiddleCenter, AutoSize = false, Bounds = bounds
    };

    Button MakeButton(string text, Rectangle bounds)
    {
        var btn = new FlatButton
        {
            Text = text, Bounds = bounds, ForeColor = TEXT_LIGHT,
            BackColor = PANEL_BG, Font = new Font("Segoe UI", 10f),
            FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand
        };
        btn.FlatAppearance.BorderColor = BORDER;
        btn.FlatAppearance.BorderSize = 1;
        btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(35, 40, 50);
        return btn;
    }

    static void DrawRoundedBorder(Graphics g, Rectangle r, Color color, int radius)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(color, 1f);
        using var path = RoundRect(r, radius);
        g.DrawPath(pen, path);
    }

    public static GraphicsPath RoundRect(Rectangle r, int radius)
    {
        var p = new GraphicsPath();
        p.AddArc(r.X, r.Y, radius * 2, radius * 2, 180, 90);
        p.AddArc(r.Right - radius * 2, r.Y, radius * 2, radius * 2, 270, 90);
        p.AddArc(r.Right - radius * 2, r.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
        p.AddArc(r.X, r.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
        p.CloseFigure();
        return p;
    }
}

// ─── Panel s animovanou čárou vítěze ─────────────────────────────────────────
class WinLinePanel : Panel
{
    PointF _from, _to;
    Color _color;
    float _progress = 0f;
    bool _active = false;
    Timer _timer;

    public WinLinePanel()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
    }

    public void StartLine(PointF from, PointF to, Color color)
    {
        _timer?.Stop();
        _from = from; _to = to; _color = color;
        _progress = 0f; _active = true;
        _timer = new Timer { Interval = 12 };
        _timer.Tick += (s, e) =>
        {
            _progress = Math.Min(1f, _progress + 0.06f);
            Invalidate();
            if (_progress >= 1f) _timer.Stop();
        };
        _timer.Start();
    }

    public void ClearLine() { _timer?.Stop(); _active = false; Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (!_active || _progress <= 0f) return;
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float ex = _from.X + (_to.X - _from.X) * _progress;
        float ey = _from.Y + (_to.Y - _from.Y) * _progress;
        var end = new PointF(ex, ey);
        // Glow
        using (var gp = new Pen(Color.FromArgb(55, _color.R, _color.G, _color.B), 18f)
               { StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawLine(gp, _from, end);
        // Hlavní čára
        using (var pen = new Pen(Color.FromArgb(230, _color.R, _color.G, _color.B), 5f)
               { StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawLine(pen, _from, end);
    }
}

// ─── Buňka herní desky ────────────────────────────────────────────────────────
class CellButton : Control
{
    static readonly Color BG_NORMAL  = Color.FromArgb(22, 27, 34);
    static readonly Color BG_HOVER   = Color.FromArgb(30, 38, 50);
    static readonly Color BORDER_CLR = Color.FromArgb(48, 54, 61);

    public event Action<int> CellClicked;

    readonly int index;
    char symbol = '\0';
    Color symbolColor = Color.White;
    bool isWinner = false, isHovered = false;
    float drawProgress = 0f;
    Timer drawTimer;

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
        drawTimer.Tick += (s, e) =>
        {
            drawProgress = Math.Min(1f, drawProgress + 0.08f);
            Invalidate();
            if (drawProgress >= 1f) drawTimer.Stop();
        };
        drawTimer.Start(); Invalidate();
    }

    public void SetWinner(Color color) { isWinner = true; symbolColor = color; Invalidate(); }

    public void Reset()
    {
        symbol = '\0'; isWinner = false; drawProgress = 0f;
        drawTimer?.Stop(); Invalidate();
    }

    protected override void OnMouseEnter(EventArgs e) { isHovered = true;  Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { isHovered = false; Invalidate(); }
    protected override void OnClick(EventArgs e) => CellClicked?.Invoke(index);

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
        var r = ClientRectangle;

        Color bg = isWinner
            ? Color.FromArgb(35, symbolColor.R, symbolColor.G, symbolColor.B)
            : (isHovered && symbol == '\0' ? BG_HOVER : BG_NORMAL);

        using (var b = new SolidBrush(bg))
        using (var path = RR(r, 10)) g.FillPath(b, path);

        Color bc = isWinner
            ? Color.FromArgb(190, symbolColor.R, symbolColor.G, symbolColor.B)
            : (isHovered ? Color.FromArgb(80, 100, 120) : BORDER_CLR);

        using (var pen = new Pen(bc, isWinner ? 2f : 1f))
        using (var path = RR(r, 10)) g.DrawPath(pen, path);

        if (symbol == '\0') return;

        float cx = r.Width / 2f, cy = r.Height / 2f;
        float size = Math.Min(r.Width, r.Height) * 0.32f;
        float sw = Math.Max(2.5f, size * 0.15f);

        using var pen2 = new Pen(Color.FromArgb((int)(255 * drawProgress), symbolColor), sw)
            { StartCap = LineCap.Round, EndCap = LineCap.Round };

        if (symbol == 'X')
        {
            if (drawProgress <= 0.5f)
            {
                float p = drawProgress * 2f;
                g.DrawLine(pen2, cx - size, cy - size, cx - size + 2 * size * p, cy - size + 2 * size * p);
            }
            else
            {
                float p2 = (drawProgress - 0.5f) * 2f;
                g.DrawLine(pen2, cx - size, cy - size, cx + size, cy + size);
                g.DrawLine(pen2, cx + size, cy - size, cx + size - 2 * size * p2, cy - size + 2 * size * p2);
            }
        }
        else
        {
            g.DrawArc(pen2, cx - size, cy - size, size * 2, size * 2, -90, 360f * drawProgress);
        }
    }

    static GraphicsPath RR(Rectangle r, int rad)
    {
        var p = new GraphicsPath();
        p.AddArc(r.X, r.Y, rad * 2, rad * 2, 180, 90);
        p.AddArc(r.Right - rad * 2, r.Y, rad * 2, rad * 2, 270, 90);
        p.AddArc(r.Right - rad * 2, r.Bottom - rad * 2, rad * 2, rad * 2, 0, 90);
        p.AddArc(r.X, r.Bottom - rad * 2, rad * 2, rad * 2, 90, 90);
        p.CloseFigure(); return p;
    }
}

// ─── Tlačítko výběru velikosti ────────────────────────────────────────────────
class SizeButton : Control
{
    static readonly Color BG_ACT  = Color.FromArgb(28, 78, 148);
    static readonly Color BG_IN   = Color.FromArgb(22, 27, 34);
    static readonly Color BD_ACT  = Color.FromArgb(88, 166, 255);
    static readonly Color BD_IN   = Color.FromArgb(48, 54, 61);
    static readonly Color TX_ACT  = Color.FromArgb(230, 237, 243);
    static readonly Color TX_IN   = Color.FromArgb(139, 148, 158);

    bool active;
    readonly string label;

    public SizeButton(string label, bool active)
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
        using (var b = new SolidBrush(active ? BG_ACT : BG_IN))
        using (var path = RR(r, 8)) g.FillPath(b, path);
        using (var pen = new Pen(active ? BD_ACT : BD_IN, active ? 1.5f : 1f))
        using (var path = RR(r, 8)) g.DrawPath(pen, path);
        using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        using var font = new Font("Segoe UI", 10f, active ? FontStyle.Bold : FontStyle.Regular);
        g.DrawString(label, font, new SolidBrush(active ? TX_ACT : TX_IN), r, sf);
    }

    static GraphicsPath RR(Rectangle r, int rad)
    {
        var p = new GraphicsPath();
        p.AddArc(r.X, r.Y, rad * 2, rad * 2, 180, 90);
        p.AddArc(r.Right - rad * 2, r.Y, rad * 2, rad * 2, 270, 90);
        p.AddArc(r.Right - rad * 2, r.Bottom - rad * 2, rad * 2, rad * 2, 0, 90);
        p.AddArc(r.X, r.Bottom - rad * 2, rad * 2, rad * 2, 90, 90);
        p.CloseFigure(); return p;
    }
}

class FlatButton : Button
{
    public FlatButton() { DoubleBuffered = true; }
    protected override bool ShowFocusCues => false;
}
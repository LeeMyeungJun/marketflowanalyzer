using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MarketFlowAnalyzer
{
    // ==========================================
    // 1. Models
    // ==========================================
    public class YFResponse { public YFQuoteResponse quoteResponse { get; set; } }
    public class YFQuoteResponse { public List<YFQuote> result { get; set; } }
    public class YFQuote { public string symbol { get; set; } public string shortName { get; set; } public double regularMarketPrice { get; set; } public long regularMarketVolume { get; set; } public double regularMarketChangePercent { get; set; } }

    public class StockData
    {
        public int Rank { get; set; }
        public string Sector { get; set; }
        public string Name { get; set; }
        public string Ticker { get; set; }
        public double CurrentPrice { get; set; }
        public double ChangeRate { get; set; }
        public string TradeValue { get; set; }
        public int AiScore { get; set; }
    }

    public class ChartSeries
    {
        public string Name { get; set; }
        public string[] XLabels { get; set; } // 날짜 라벨 (예: D-4, 오늘)
        public double[] Y { get; set; }       // 거래대금 데이터
        public Color SeriesColor { get; set; }
    }

    // ==========================================
    // 2. MarketData Service
    // ==========================================
    public class MarketDataService
    {
        private static readonly HttpClient _http;
        private Random _rnd = new Random();

        // 💡 60일치 과거 데이터를 고정으로 보관하는 메모리 캐시
        private Dictionary<string, double[]> _historicalVolume = new Dictionary<string, double[]>();

        static MarketDataService()
        {
            _http = new HttpClient();
            _http.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
        }

        private Dictionary<string, List<string>> _sectorTickers = new Dictionary<string, List<string>>()
        {
            { "반도체", new List<string> { "NVDA", "AMD", "TSM", "AVGO", "SOXL", "INTC", "005930.KS" } },
            { "AI", new List<string> { "MSFT", "GOOGL", "META", "PLTR", "CRWD" } },
            { "자동차", new List<string> { "TSLA", "TM", "F", "GM", "005380.KS" } },
            { "바이오", new List<string> { "LLY", "NVO", "JNJ", "MRK", "207940.KS" } },
            { "2차전지", new List<string> { "ALB", "SQM", "QS", "LIT", "006400.KS" } },
            { "방산", new List<string> { "LMT", "RTX", "NOC", "GD", "012450.KS" } },
            { "금융", new List<string> { "JPM", "V", "MA", "BAC" } },
            { "에너지", new List<string> { "XOM", "CVX", "COP", "OXY" } }
        };

        public MarketDataService()
        {
            // 프로그램 시작 시 각 섹터별 60일치 과거 데이터 고정 생성 (절대 변하지 않음)
            foreach (var sec in _sectorTickers.Keys)
            {
                double[] history = new double[60];
                double baseVol = 3000 + (sec.Length * 500);
                for (int i = 0; i < 60; i++)
                {
                    baseVol += _rnd.Next(-300, 350);
                    if (baseVol < 500) baseVol = 500 + _rnd.Next(100, 500);
                    history[i] = baseVol;
                }
                _historicalVolume[sec] = history;
            }
        }

        // 타이머가 돌 때 '오늘(59번 인덱스)'의 데이터만 실시간으로 변동시킴
        public void UpdateTodayVolumeLive()
        {
            foreach (var sec in _historicalVolume.Keys)
            {
                double change = _rnd.Next(-150, 200);
                _historicalVolume[sec][59] += change;
                if (_historicalVolume[sec][59] < 100) _historicalVolume[sec][59] = 100;
            }
        }

        // 차트에 그릴 과거 데이터 + 오늘 데이터 배열 자르기
        public (string[] XLabels, double[] Y) GetChartData(string sector, string period)
        {
            int days = period == "오늘" ? 1 : period == "5일" ? 5 : period == "20일" ? 20 : 60;

            string[] labels = new string[days];
            double[] yData = new double[days];

            double[] fullHistory = _historicalVolume[sector];

            for (int i = 0; i < days; i++)
            {
                // 60개 배열 중 끝에서부터 필요한 만큼 잘라냄
                int targetIndex = 60 - days + i;
                yData[i] = fullHistory[targetIndex];

                // 라벨링 (마지막은 '오늘', 나머지는 'D-날짜')
                if (i == days - 1) labels[i] = "오늘";
                else labels[i] = $"D-{days - 1 - i}";
            }
            return (labels, yData);
        }

        public async Task<List<StockData>> GetRealTimeTopStocksAsync(List<string> sectors)
        {
            var resultList = new List<StockData>();
            foreach (var sec in sectors)
            {
                if (!_sectorTickers.ContainsKey(sec)) continue;
                string url = $"https://query1.finance.yahoo.com/v7/finance/quote?symbols={string.Join(",", _sectorTickers[sec])}";

                try
                {
                    string json = await _http.GetStringAsync(url);
                    var yfData = JsonSerializer.Deserialize<YFResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (yfData?.quoteResponse?.result != null && yfData.quoteResponse.result.Count > 0)
                    {
                        var top = yfData.quoteResponse.result.OrderByDescending(s => s.regularMarketPrice * s.regularMarketVolume).First();
                        double tradeValueUSD = top.regularMarketPrice * top.regularMarketVolume;
                        resultList.Add(new StockData
                        {
                            Sector = sec,
                            Ticker = top.symbol,
                            Name = top.shortName ?? top.symbol,
                            CurrentPrice = Math.Round(top.regularMarketPrice, 2),
                            ChangeRate = Math.Round(top.regularMarketChangePercent, 2),
                            TradeValue = FormatMoney(tradeValueUSD + _rnd.Next(-50000, 50000)), // 라이브 효과 노이즈
                            AiScore = _rnd.Next(85, 100)
                        });
                    }
                }
                catch { resultList.Add(new StockData { Sector = sec, Ticker = "ERR", Name = "수신 지연" }); }
            }
            var sortedList = resultList.OrderByDescending(s => s.AiScore).ToList();
            for (int i = 0; i < sortedList.Count; i++) sortedList[i].Rank = i + 1;
            return sortedList;
        }

        private string FormatMoney(double value)
        {
            if (value >= 1_000_000_000) return "$" + (value / 1_000_000_000).ToString("0.00") + "B";
            if (value >= 1_000_000) return "$" + (value / 1_000_000).ToString("0.00") + "M";
            return "$" + value.ToString("N0");
        }
    }

    // ==========================================
    // 3. Custom Multi-Bar Chart (봉/막대 그래프로 완벽 재설계)
    // ==========================================
    public class CustomMultiBarChart : PictureBox
    {
        private List<ChartSeries> _series = new List<ChartSeries>();
        private string _title = "";
        private Point _mouse = Point.Empty;
        private bool _showTip = false;

        public CustomMultiBarChart()
        {
            DoubleBuffered = true; BackColor = Color.FromArgb(20, 20, 20);
            MouseMove += (s, e) => { _mouse = e.Location; _showTip = true; Invalidate(); };
            MouseLeave += (s, e) => { _showTip = false; Invalidate(); };
        }

        public void UpdateChart(string title, List<ChartSeries> seriesList)
        {
            _title = title; _series = seriesList; Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            g.DrawString(_title, new Font("맑은 고딕", 12, FontStyle.Bold), Brushes.White, 15, 12);

            if (_series == null || _series.Count == 0 || _series[0].Y.Length == 0) return;

            int pL = 70, pR = 30, pT = 50, pB = 40, w = Width - pL - pR, h = Height - pT - pB;
            if (w <= 0 || h <= 0) return;

            int numDays = _series[0].Y.Length;
            int numSectors = _series.Count;

            double maxY = _series.Max(s => s.Y.Max()) * 1.1; // 최대값 여백 10%
            if (maxY == 0) maxY = 100;

            // 배경 가로선(그리드) 그리기
            for (int i = 0; i <= 5; i++)
            {
                float py = pT + h - (h * i / 5f);
                g.DrawLine(new Pen(Color.FromArgb(60, 60, 60), 1), pL, py, pL + w, py);
                g.DrawString($"{(int)(maxY * i / 5f)}", new Font("맑은 고딕", 9), Brushes.LightGray, 5, py - 7);
            }

            // 막대(Bar) 계산 및 렌더링
            float slotW = w / (float)numDays; // 하루에 할당된 가로 영역
            float totalBarGroupW = slotW * 0.8f; // 그중 80%만 막대가 차지 (20%는 날짜간 여백)
            float singleBarW = totalBarGroupW / numSectors; // 막대 하나 두께

            // 오늘 날짜(마지막 슬롯) 하이라이트 배경
            float todayX = pL + (numDays - 1) * slotW;
            g.FillRectangle(new SolidBrush(Color.FromArgb(15, 255, 255, 255)), todayX, pT, slotW, h);

            for (int d = 0; d < numDays; d++)
            {
                // X축 날짜 라벨 쓰기 (D-4, 오늘 등)
                string label = _series[0].XLabels[d];
                var font = new Font("맑은 고딕", 9, label == "오늘" ? FontStyle.Bold : FontStyle.Regular);
                var brush = label == "오늘" ? Brushes.Yellow : Brushes.Gray;
                float labelW = g.MeasureString(label, font).Width;
                g.DrawString(label, font, brush, pL + d * slotW + (slotW - labelW) / 2, pT + h + 8);

                // 섹터별 막대 그리기
                for (int s = 0; s < numSectors; s++)
                {
                    float val = (float)_series[s].Y[d];
                    float barH = (val / (float)maxY) * h;

                    // 각 섹터 막대의 정확한 X 좌표 계산
                    float bx = pL + d * slotW + (slotW - totalBarGroupW) / 2 + (s * singleBarW);
                    float by = pT + h - barH;

                    // 막대 본체 (약간 투명하게)
                    g.FillRectangle(new SolidBrush(Color.FromArgb(200, _series[s].SeriesColor)), bx, by, singleBarW - 1, barH);
                    // 막대 테두리 (선명하게)
                    g.DrawRectangle(new Pen(_series[s].SeriesColor, 1), bx, by, singleBarW - 1, barH);
                }
            }

            // 마우스 오버 시 툴팁 표시
            if (_showTip && _mouse.X > pL && _mouse.X < pL + w && _mouse.Y > pT && _mouse.Y < pT + h)
            {
                int hoverDayIdx = (int)((_mouse.X - pL) / slotW);
                if (hoverDayIdx >= 0 && hoverDayIdx < numDays)
                {
                    float hoverX = pL + hoverDayIdx * slotW + slotW / 2;
                    g.DrawLine(new Pen(Color.FromArgb(150, Color.White)) { DashStyle = DashStyle.Dash }, hoverX, pT, hoverX, pT + h);

                    string tip = $"[ {_series[0].XLabels[hoverDayIdx]} 자금 흐름 ]\n";
                    for (int s = 0; s < _series.Count; s++)
                        tip += $"■ {_series[s].Name}: {(int)_series[s].Y[hoverDayIdx]}\n";

                    var f = new Font("맑은 고딕", 9); var sz = g.MeasureString(tip, f);
                    float tx = _mouse.X + 15, ty = _mouse.Y + 15;
                    if (tx + sz.Width > Width) tx = _mouse.X - sz.Width - 10;

                    var r = new RectangleF(tx, ty, sz.Width + 14, sz.Height + 8);
                    g.FillRectangle(new SolidBrush(Color.FromArgb(240, 30, 30, 30)), r);
                    g.DrawRectangle(new Pen(Color.Gray, 1), Rectangle.Round(r));
                    g.DrawString(tip, f, Brushes.White, tx + 7, ty + 6);
                }
            }
        }
    }

    // ==========================================
    // 4. UI View (MainForm)
    // ==========================================
    public class MainForm : Form
    {
        private MarketDataService _api = new MarketDataService();
        private Color[] _colors = { Color.DeepSkyBlue, Color.HotPink, Color.LimeGreen, Color.Orange, Color.Gold, Color.MediumSpringGreen };

        private FlowLayoutPanel topPanel, sectorPanel, controlPanel;
        private SplitContainer split;
        private CustomMultiBarChart chart;
        private Label lblSummary;
        private DataGridView dgv;
        private RichTextBox rtbLog;

        private System.Windows.Forms.Timer _liveTimer;
        private bool _isRefreshing = false;

        public MainForm()
        {
            Text = "Market Flow Analyzer (Volume Bar Chart)";
            Size = new Size(1400, 900);
            BackColor = Color.FromArgb(18, 18, 18); ForeColor = Color.White; Font = new Font("맑은 고딕", 10F);

            BuildUI();

            _liveTimer = new System.Windows.Forms.Timer { Interval = 5000 };
            _liveTimer.Tick += async (s, e) => {
                _api.UpdateTodayVolumeLive(); // 핵심: 5초마다 오늘 막대값만 변경
                await RefreshDataAsync(true);
            };
            _liveTimer.Start();

            _ = RefreshDataAsync(false);
        }

        private void BuildUI()
        {
            var baseTable = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
            baseTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 130F)); baseTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); baseTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 180F));
            Controls.Add(baseTable);

            topPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(30, 30, 30), Padding = new Padding(10), FlowDirection = FlowDirection.TopDown };

            var optPanel = new FlowLayoutPanel { AutoSize = true };
            foreach (var opt in new[] { "거래대금", "거래량", "등락률", "AI 점수" }) optPanel.Controls.Add(new CheckBox { Text = opt, AutoSize = true, Checked = true, FlatStyle = FlatStyle.Flat, ForeColor = Color.White });
            topPanel.Controls.Add(optPanel);

            sectorPanel = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 5, 0, 5) };
            string[] sectors = { "반도체", "AI", "자동차", "바이오", "2차전지", "방산", "금융", "에너지" };
            for (int i = 0; i < sectors.Length; i++)
            {
                var cb = new CheckBox { Text = sectors[i], AutoSize = true, ForeColor = _colors[i % _colors.Length], FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
                cb.CheckedChanged += async (s, e) => { if (cb.Checked) await RefreshDataAsync(); };
                if (sectors[i] == "반도체" || sectors[i] == "AI") cb.Checked = true;
                sectorPanel.Controls.Add(cb);
            }
            topPanel.Controls.Add(sectorPanel);

            controlPanel = new FlowLayoutPanel { AutoSize = true };
            foreach (var p in new[] { "오늘", "5일", "20일", "60일" })
            {
                var btn = new Button { Text = p, Size = new Size(65, 30), FlatStyle = FlatStyle.Flat, BackColor = p == "20일" ? Color.FromArgb(0, 122, 204) : BackColor, ForeColor = Color.White };
                btn.Click += async (s, e) => {
                    foreach (Button b in controlPanel.Controls.OfType<Button>()) b.BackColor = BackColor;
                    btn.BackColor = Color.FromArgb(0, 122, 204);
                    await RefreshDataAsync();
                };
                controlPanel.Controls.Add(btn);
            }
            topPanel.Controls.Add(controlPanel);
            baseTable.Controls.Add(topPanel, 0, 0);

            split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterDistance = 850 };
            var leftTable = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 }; leftTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); leftTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F));

            chart = new CustomMultiBarChart { Dock = DockStyle.Fill };
            lblSummary = new Label { Dock = DockStyle.Fill, BackColor = Color.FromArgb(30, 30, 30), ForeColor = Color.Yellow, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("맑은 고딕", 10.5F, FontStyle.Bold) };
            leftTable.Controls.Add(chart, 0, 0); leftTable.Controls.Add(lblSummary, 0, 1);
            split.Panel1.Controls.Add(leftTable);

            dgv = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.FromArgb(25, 25, 25),
                BorderStyle = BorderStyle.None,
                AllowUserToAddRows = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                EnableHeadersVisualStyles = false,
                GridColor = Color.FromArgb(50, 50, 50)
            };
            dgv.DefaultCellStyle.BackColor = Color.FromArgb(35, 35, 35); dgv.DefaultCellStyle.ForeColor = Color.White;
            dgv.DefaultCellStyle.SelectionBackColor = Color.FromArgb(0, 122, 204); dgv.DefaultCellStyle.SelectionForeColor = Color.White;
            dgv.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(45, 45, 45); dgv.AlternatingRowsDefaultCellStyle.ForeColor = Color.White;
            dgv.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(55, 55, 55); dgv.ColumnHeadersDefaultCellStyle.ForeColor = Color.White; dgv.ColumnHeadersDefaultCellStyle.Font = new Font("맑은 고딕", 10F, FontStyle.Bold);
            split.Panel2.Controls.Add(dgv); baseTable.Controls.Add(split, 0, 1);

            rtbLog = new RichTextBox { Dock = DockStyle.Fill, BackColor = Color.FromArgb(15, 15, 15), ForeColor = Color.LightGreen, Font = new Font("Consolas", 10.5F), BorderStyle = BorderStyle.None };
            baseTable.Controls.Add(rtbLog, 0, 2);
        }

        private async Task RefreshDataAsync(bool isAutoRefresh = false)
        {
            if (chart == null || sectorPanel == null) return;
            if (_isRefreshing) return;
            _isRefreshing = true;

            try
            {
                string period = controlPanel.Controls.OfType<Button>().First(b => b.BackColor != BackColor).Text;
                var selectedSectors = sectorPanel.Controls.OfType<CheckBox>().Where(c => c.Checked).Select(c => c.Text).ToList();
                if (selectedSectors.Count == 0) return;

                // 1. 차트 렌더링 (과거 고정, 오늘만 변동되는 Bar Chart)
                var series = new List<ChartSeries>();
                int cIdx = 0;
                foreach (CheckBox cb in sectorPanel.Controls)
                {
                    if (cb.Checked)
                    {
                        var data = _api.GetChartData(cb.Text, period);
                        series.Add(new ChartSeries { Name = cb.Text, XLabels = data.XLabels, Y = data.Y, SeriesColor = _colors[cIdx % _colors.Length] });
                    }
                    cIdx++;
                }
                chart.UpdateChart(string.Join(" vs ", selectedSectors) + $" 거래대금 흐름 ({period})", series);

                // 2. 외부 API 호출 (표 데이터 갱신)
                var topStocks = await _api.GetRealTimeTopStocksAsync(selectedSectors);

                lblSummary.Text = $" ■ [실시간 볼륨 추적] 현재 주도 섹터: {topStocks.FirstOrDefault()?.Sector} (대장주: {topStocks.FirstOrDefault()?.Name}) | 마지막 갱신: {DateTime.Now:HH:mm:ss}";
                dgv.DataSource = null; dgv.DataSource = topStocks;

                if (dgv.Columns.Count > 0)
                {
                    dgv.Columns["Rank"].HeaderText = "순위"; dgv.Columns["Sector"].HeaderText = "섹터"; dgv.Columns["Ticker"].HeaderText = "티커";
                    dgv.Columns["Name"].HeaderText = "종목명"; dgv.Columns["CurrentPrice"].HeaderText = "현재가($)"; dgv.Columns["ChangeRate"].HeaderText = "등락률(%)";
                    dgv.Columns["TradeValue"].HeaderText = "거래대금(USD)"; dgv.Columns["AiScore"].HeaderText = "AI점수";
                }
                if (!isAutoRefresh) Log($"[수급 추적] {string.Join(", ", selectedSectors)} 섹터 차트 렌더링 완료.");
            }
            finally { _isRefreshing = false; }
        }

        private void Log(string txt) { if (rtbLog == null) return; rtbLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {txt}\n"); rtbLog.ScrollToCaret(); }
    }

    static class Program
    {
        [STAThread] static void Main() { Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); Application.Run(new MainForm()); }
    }
}
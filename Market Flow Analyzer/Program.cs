using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace MarketFlowAnalyzer
{
    // ==========================================
    // 1. Models & Services (데이터 구조 및 가상 데이터 서비스)
    // ==========================================
    public class StockData
    {
        public int Rank { get; set; }
        public string Name { get; set; }
        public int CurrentPrice { get; set; }
        public double ChangeRate { get; set; }
        public string TradeValue { get; set; }
        public string ForeignBuy { get; set; }
        public string InstitutionalBuy { get; set; }
        public int AiScore { get; set; }
    }

    public class MarketDataService
    {
        private Random _rand = new Random();

        // 선택된 섹터 및 기간별 차트 데이터 생성 (X축 순서, Y축 거래대금)
        public (double[] X, double[] Y) GetSectorChartData(string sector, string period)
        {
            int points = period == "오늘" ? 20 : period == "5일" ? 40 : period == "20일" ? 60 : 100;
            double[] x = new double[points];
            double[] y = new double[points];

            double currentVal = 5000 + _rand.Next(-1000, 2000);
            for (int i = 0; i < points; i++)
            {
                x[i] = i;
                currentVal += _rand.Next(-500, 600);
                if (currentVal < 1000) currentVal = 1000;
                y[i] = currentVal;
            }
            return (x, y);
        }

        // 선택된 섹터의 실시간 TOP 종목 목록
        public List<StockData> GetTopStocks(string sector)
        {
            return new List<StockData>
            {
                new StockData { Rank = 1, Name = sector + " 대장주", CurrentPrice = 78200, ChangeRate = 4.8, TradeValue = "5,420억", ForeignBuy = "+210억", InstitutionalBuy = "+120억", AiScore = 96 },
                new StockData { Rank = 2, Name = sector + " 핵심주 A", CurrentPrice = 145000, ChangeRate = 2.1, TradeValue = "3,110억", ForeignBuy = "-30억", InstitutionalBuy = "+85억", AiScore = 89 },
                new StockData { Rank = 3, Name = sector + " 수혜주 B", CurrentPrice = 32400, ChangeRate = -1.5, TradeValue = "1,950억", ForeignBuy = "+140억", InstitutionalBuy = "-40억", AiScore = 75 },
                new StockData { Rank = 4, Name = sector + " 부품주 C", CurrentPrice = 12500, ChangeRate = 8.7, TradeValue = "1,420억", ForeignBuy = "+50억", InstitutionalBuy = "+20억", AiScore = 91 }
            };
        }
    }

    // ==========================================
    // 2. Custom Control (외부 라이브러리 없는 순수 그래픽 차트)
    // ==========================================
    public class CustomAreaChart : PictureBox
    {
        private double[] _xData = new double[0];
        private double[] _yData = new double[0];
        private string _title = "";
        private Point _mousePos = Point.Empty;
        private bool _showTooltip = false;
        private Color _accentColor = Color.FromArgb(0, 122, 204);

        public CustomAreaChart()
        {
            this.DoubleBuffered = true; // 화면 깜빡임 방지
            this.BackColor = Color.FromArgb(35, 35, 35);
            this.MouseMove += (s, e) => { _mousePos = e.Location; _showTooltip = true; this.Invalidate(); };
            this.MouseLeave += (s, e) => { _showTooltip = false; this.Invalidate(); };
        }

        public void UpdateChart(string title, double[] x, double[] y)
        {
            _title = title;
            _xData = x;
            _yData = y;
            this.Invalidate(); // 다시 그리기
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // 1. 제목 그리기
            using (Font titleFont = new Font("맑은 고딕", 12, FontStyle.Bold))
            {
                g.DrawString(_title, titleFont, Brushes.WhiteSmoke, 15, 12);
            }

            if (_xData.Length == 0 || _yData.Length == 0) return;

            // 레이아웃 여백 설정
            int padLeft = 70, padRight = 30, padTop = 45, padBottom = 40;
            int w = this.Width - padLeft - padRight;
            int h = this.Height - padTop - padBottom;
            if (w <= 0 || h <= 0) return;

            double minY = _yData.Min(), maxY = _yData.Max();
            if (minY == maxY) { minY -= 10; maxY += 10; }
            double rangeY = maxY - minY;

            double minX = _xData.Min(), maxX = _xData.Max();
            if (minX == maxX) { minX -= 1; maxX += 1; }
            double rangeX = maxX - minX;

            // 2. 배경 그리드선 및 Y축 단위선 그리기
            using (Pen gridPen = new Pen(Color.FromArgb(55, 55, 55), 1))
            using (Font axisFont = new Font("맑은 고딕", 9))
            {
                int ticks = 4;
                for (int i = 0; i <= ticks; i++)
                {
                    float yVal = (float)(minY + (rangeY * i / ticks));
                    float py = padTop + h - (h * i / ticks);
                    g.DrawLine(gridPen, padLeft, py, padLeft + w, py);
                    g.DrawString($"{(int)yVal}억", axisFont, Brushes.Gray, 10, py - 7);
                }
            }

            // 3. 데이터 좌표를 화면 픽셀 좌표로 변환
            PointF[] points = new PointF[_xData.Length];
            for (int i = 0; i < _xData.Length; i++)
            {
                float px = padLeft + (float)((_xData[i] - minX) / rangeX * w);
                float py = padTop + h - (float)((_yData[i] - minY) / rangeY * h);
                points[i] = new PointF(px, py);
            }

            // 4. 자금 흐름을 시각화하는 반투명 면적(Area) 채우기
            if (points.Length > 1)
            {
                List<PointF> fillPoints = new List<PointF>(points);
                fillPoints.Add(new PointF(points[points.Length - 1].X, padTop + h));
                fillPoints.Add(new PointF(points[0].X, padTop + h));

                using (LinearGradientBrush fillBrush = new LinearGradientBrush(
                    new Point(0, padTop), new Point(0, padTop + h),
                    Color.FromArgb(80, _accentColor), Color.FromArgb(10, _accentColor)))
                {
                    g.FillPolygon(fillBrush, fillPoints.ToArray());
                }

                // 선(Line) 그리기
                using (Pen linePen = new Pen(_accentColor, 2.5f))
                {
                    g.DrawLines(linePen, points);
                }
            }

            // 5. 실시간 마우스 트래킹 및 가이드라인/툴팁 구현
            if (_showTooltip && points.Length > 0)
            {
                // 마우스와 가장 가까운 X 데이터 포인트 찾기
                int idx = 0;
                float minDist = float.MaxValue;
                for (int i = 0; i < points.Length; i++)
                {
                    float dist = Math.Abs(points[i].X - _mousePos.X);
                    if (dist < minDist) { minDist = dist; idx = i; }
                }

                PointF targetPt = points[idx];

                // 세로 점선 그리기
                using (Pen guidePen = new Pen(Color.FromArgb(120, Color.White), 1) { DashStyle = DashStyle.Dash })
                {
                    g.DrawLine(guidePen, targetPt.X, padTop, targetPt.X, padTop + h);
                }

                // 포인트 강조 원 그리기
                g.FillEllipse(Brushes.White, targetPt.X - 5, targetPt.Y - 5, 10, 10);
                using (Pen circlePen = new Pen(_accentColor, 2))
                {
                    g.DrawEllipse(circlePen, targetPt.X - 5, targetPt.Y - 5, 10, 10);
                }

                // 툴팁 상자 렌더링
                string tipText = $"구분: {idx + 1}구간\n자금 흐름: {(int)_yData[idx]} 억\n변동성: +{((_yData[idx] - minY) / (minY == 0 ? 1 : minY) * 100):0.0}%";
                using (Font tipFont = new Font("맑은 고딕", 9))
                {
                    SizeF boxSize = g.MeasureString(tipText, tipFont);
                    float tx = _mousePos.X + 15;
                    float ty = _mousePos.Y + 15;

                    if (tx + boxSize.Width > this.Width) tx = _mousePos.X - boxSize.Width - 20;
                    if (ty + boxSize.Height > this.Height) ty = _mousePos.Y - boxSize.Height - 20;

                    RectangleF rect = new RectangleF(tx, ty, boxSize.Width + 12, boxSize.Height + 10);
                    g.FillRectangle(new SolidBrush(Color.FromArgb(230, 20, 20, 20)), rect);
                    g.DrawRectangle(new Pen(_accentColor, 1), Rectangle.Round(rect));
                    g.DrawString(tipText, tipFont, Brushes.WhiteSmoke, tx + 6, ty + 5);
                }
            }
        }
    }

    // ==========================================
    // 3. UI View (디자이너 없이 순수 코드로 화면 구성)
    // ==========================================
    public class MainForm : Form
    {
        private MarketDataService _dataService = new MarketDataService();

        // 다크테마 컬러 팔레트
        private Color BgColor = Color.FromArgb(20, 20, 20);
        private Color PanelBgColor = Color.FromArgb(30, 30, 30);
        private Color TextColor = Color.WhiteSmoke;
        private Color AccentColor = Color.FromArgb(0, 122, 204);

        // UI 컨트롤 선언
        private FlowLayoutPanel topLayout;
        private FlowLayoutPanel sectorLayout;
        private SplitContainer mainSplitter;
        private CustomAreaChart mainChart;
        private Label lblSectorSummary;
        private DataGridView dgvSectorStocks;
        private RichTextBox rtbAiLog;

        public MainForm()
        {
            this.Text = "Market Flow Analyzer (시장 자금 흐름 분석기)";
            this.Size = new Size(1350, 850);
            this.BackColor = BgColor;
            this.ForeColor = TextColor;
            this.Font = new Font("맑은 고딕", 10F);

            BuildProgrammaticUI();
            InitializeDefaultData();
        }

        private void BuildProgrammaticUI()
        {
            // 전체 화면 분할용 최상위 레이아웃
            TableLayoutPanel baseTable = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
            baseTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 115F)); // ① 상단 옵션 패널
            baseTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));  // ② 차트 및 ③ 종목리스트 패널
            baseTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 180F)); // ④ 하단 실시간 분석 패널
            this.Controls.Add(baseTable);

            // ------------------------------------------
            // ① 상단 옵션 영역 생성
            // ------------------------------------------
            topLayout = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = PanelBgColor, Padding = new Padding(12), FlowDirection = FlowDirection.TopDown, WrapContents = false };

            // 1) 데이터 표시 체크박스
            FlowLayoutPanel checkPanel = CreateRowFlow();
            string[] checkOpts = { "거래대금", "거래량", "거래량 증가율", "평균 등락률", "외국인 순매수", "기관 순매수", "AI 점수" };
            foreach (var opt in checkOpts)
            {
                CheckBox cb = new CheckBox { Text = opt, AutoSize = true, ForeColor = TextColor, FlatStyle = FlatStyle.Flat };
                if (opt == "거래대금" || opt == "AI 점수") cb.Checked = true;
                checkPanel.Controls.Add(cb);
            }
            topLayout.Controls.Add(checkPanel);

            // 2) 섹터 선택 라디오버튼
            sectorLayout = CreateRowFlow();
            string[] targetSectors = { "반도체", "AI", "바이오", "건설", "로봇", "방산", "자동차", "2차전지", "화장품", "원전" };
            foreach (var sec in targetSectors)
            {
                RadioButton rb = new RadioButton { Text = sec, AutoSize = true, ForeColor = TextColor, FlatStyle = FlatStyle.Flat };
                rb.CheckedChanged += (s, e) => { if (rb.Checked) RefreshScreenData(rb.Text, "오늘"); };
                if (sec == "반도체") rb.Checked = true;
                sectorLayout.Controls.Add(rb);
            }
            topLayout.Controls.Add(sectorLayout);

            // 3) 기간 토글 및 정렬 콤보박스
            FlowLayoutPanel controlPanel = CreateRowFlow();
            string[] periods = { "오늘", "5일", "20일", "60일", "120일", "1년" };
            foreach (var p in periods)
            {
                Button btn = new Button { Text = p, Size = new Size(65, 28), FlatStyle = FlatStyle.Flat, BackColor = (p == "오늘") ? AccentColor : BgColor, ForeColor = TextColor };
                btn.FlatAppearance.BorderSize = 1;
                btn.Click += PeriodButton_Click;
                controlPanel.Controls.Add(btn);
            }
            controlPanel.Controls.Add(new Label { Text = "    정렬 기준: ", AutoSize = true, Padding = new Padding(0, 4, 0, 0) });
            ComboBox cmbSort = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, BackColor = BgColor, ForeColor = TextColor, Width = 110 };
            cmbSort.Items.AddRange(new object[] { "거래대금", "거래량 증가율", "상승률", "AI 점수" });
            cmbSort.SelectedIndex = 0;
            controlPanel.Controls.Add(cmbSort);
            topLayout.Controls.Add(controlPanel);

            baseTable.Controls.Add(topLayout, 0, 0);

            // ------------------------------------------
            // ② 메인 차트 & ③ 우측 정보 패널 (Split)
            // ------------------------------------------
            mainSplitter = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterDistance = 780, BackColor = BgColor };

            // 좌측 차트 레이아웃 구성
            TableLayoutPanel leftChartContainer = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
            leftChartContainer.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            leftChartContainer.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F));

            mainChart = new CustomAreaChart { Dock = DockStyle.Fill };
            leftChartContainer.Controls.Add(mainChart, 0, 0);

            lblSectorSummary = new Label { Dock = DockStyle.Fill, BackColor = PanelBgColor, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("맑은 고딕", 10.5F, FontStyle.Bold) };
            leftChartContainer.Controls.Add(lblSectorSummary, 0, 1);
            mainSplitter.Panel1.Controls.Add(leftChartContainer);

            // 우측 TOP 종목 그리드 구성
            dgvSectorStocks = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = PanelBgColor,
                BorderStyle = BorderStyle.None,
                AllowUserToAddRows = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                EnableHeadersVisualStyles = false
            };
            dgvSectorStocks.DefaultCellStyle.BackColor = PanelBgColor;
            dgvSectorStocks.DefaultCellStyle.ForeColor = TextColor;
            dgvSectorStocks.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(45, 45, 45);
            dgvSectorStocks.ColumnHeadersDefaultCellStyle.ForeColor = TextColor;
            dgvSectorStocks.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex >= 0)
                {
                    string name = dgvSectorStocks.Rows[e.RowIndex].Cells["Name"].Value.ToString();
                    MessageBox.Show($"[{name}] 상세 분석 대시보드로 이동합니다. (확장 모듈 연동용 설계 완료)", "종목 상세 정보", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };

            mainSplitter.Panel2.Controls.Add(dgvSectorStocks);
            baseTable.Controls.Add(mainSplitter, 0, 1);

            // ------------------------------------------
            // ④ 하단 실시간 AI 분석 패널
            // ------------------------------------------
            rtbAiLog = new RichTextBox { Dock = DockStyle.Fill, BackColor = Color.FromArgb(15, 15, 15), ForeColor = Color.LightGreen, Font = new Font("Consolas", 10.5F), ReadOnly = true, BorderStyle = BorderStyle.None };
            baseTable.Controls.Add(rtbAiLog, 0, 2);
        }

        private FlowLayoutPanel CreateRowFlow()
        {
            return new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0, 0, 0, 4) };
        }

        // ------------------------------------------
        // 4. 로직 및 데이터 갱신 이벤트 처리
        // ------------------------------------------
        private void InitializeDefaultData()
        {
            RefreshScreenData("반도체", "오늘");
            LogAiMessage("[시스템 모니터링] Market Flow Analyzer 엔진 활성화 완료.", Color.DarkGray);
            LogAiMessage("[자금 이동 감지] ★★★★★ 2차전지(-5,200억) ↘ 수급 유출 발생 -> 반도체(+6,100억) ↗ 강한 자금 유입 감지됨.", Color.Cyan);
            LogAiMessage("[신규 테마 발견] 양자컴퓨터 섹터 평균 거래대금 180억 대비 오늘 1,700억 돌파 (AI 인기 점수: 94점)", Color.Gold);
        }

        private void PeriodButton_Click(object sender, EventArgs e)
        {
            Button clicked = sender as Button;
            if (clicked == null) return;

            // 기간 토글 버튼 디자인 변경 효과
            foreach (Control ctrl in clicked.Parent.Controls)
            {
                if (ctrl is Button btn) btn.BackColor = BgColor;
            }
            clicked.BackColor = AccentColor;

            // 현재 선택되어 있는 섹터 확인
            string currentSector = "반도체";
            foreach (RadioButton rb in sectorLayout.Controls)
            {
                if (rb.Checked) currentSector = rb.Text;
            }

            RefreshScreenData(currentSector, clicked.Text);
        }

        private void RefreshScreenData(string sector, string period)
        {
            // [해결 코드] UI가 완전히 만들어지기 전에 이벤트가 불리는 것을 방지
            if (mainChart == null) return;

            // 1) 순수 그래픽 차트 업데이트
            var chartData = _dataService.GetSectorChartData(sector, period);
            mainChart.UpdateChart($"{sector} 섹터 실시간 자금 흐름 추적 상태 ({period})", chartData.X, chartData.Y);

            // 2) 하단 텍스트 요약 정보 갱신
            lblSectorSummary.Text = $"   ■ 현재 선택 섹터: {sector}   |   현재 총 거래대금: 2조 3,400억 (+18.4%)   |   AI 가중치 점수: 92점   |   5일 누적 등락률: +14.0%";

            // 3) 우측 DataGridView 바인딩 및 헤더 한글화
            dgvSectorStocks.DataSource = null;
            dgvSectorStocks.DataSource = _dataService.GetTopStocks(sector);

            if (dgvSectorStocks.Columns.Count > 0)
            {
                dgvSectorStocks.Columns["Rank"].HeaderText = "순위";
                dgvSectorStocks.Columns["Name"].HeaderText = "종목명";
                dgvSectorStocks.Columns["CurrentPrice"].HeaderText = "현재가";
                dgvSectorStocks.Columns["ChangeRate"].HeaderText = "등락률(%)";
                dgvSectorStocks.Columns["TradeValue"].HeaderText = "거래대금";
                dgvSectorStocks.Columns["ForeignBuy"].HeaderText = "외인 순매수";
                dgvSectorStocks.Columns["InstitutionalBuy"].HeaderText = "기관 순매수";
                dgvSectorStocks.Columns["AiScore"].HeaderText = "AI 점수";
            }
        }

        private void LogAiMessage(string text, Color color)
        {
            rtbAiLog.SelectionStart = rtbAiLog.TextLength;
            rtbAiLog.SelectionLength = 0;
            rtbAiLog.SelectionColor = color;
            rtbAiLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {text}\n");
            rtbAiLog.SelectionColor = rtbAiLog.ForeColor;
            rtbAiLog.ScrollToCaret();
        }
    }

    // ==========================================
    // 5. 진입점 (Program Main)
    // ==========================================
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
}
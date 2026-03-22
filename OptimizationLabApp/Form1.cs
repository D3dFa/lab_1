using System.Drawing.Imaging;
using System.Globalization;

namespace OptimizationLabApp;

public partial class Form1 : Form
{
    private readonly SimplexSolver solver = new();
    private readonly SensitivityExperimentRunner experimentRunner = new();

    private readonly TabControl mainTabControl = new();
    private readonly TabPage solverTabPage = new("Решение задачи");
    private readonly TabPage experimentTabPage = new("Вычислительный эксперимент");

    private readonly NumericUpDown variableCountUpDown = new();
    private readonly NumericUpDown constraintCountUpDown = new();
    private readonly ComboBox objectiveSenseComboBox = new();
    private readonly Button loadSampleButton = new();
    private readonly Button solveButton = new();
    private readonly Button clearOutputButton = new();

    private readonly DataGridView objectiveGrid = new();
    private readonly DataGridView constraintsGrid = new();
    private readonly DataGridView boundsGrid = new();
    private readonly DataGridView solutionGrid = new();
    private readonly DataGridView experimentGrid = new();

    private readonly Label solveStatusValueLabel = new();
    private readonly Label objectiveValueLabel = new();
    private readonly Label experimentStatusValueLabel = new();
    private readonly Label baseObjectiveValueLabel = new();

    private readonly TextBox canonicalFormTextBox = CreateReadOnlyTextBox();
    private readonly TextBox transformationTextBox = CreateReadOnlyTextBox();
    private readonly TextBox iterationLogTextBox = CreateReadOnlyTextBox();

    private readonly NumericUpDown experimentCountUpDown = new();
    private readonly NumericUpDown minPowerUpDown = new();
    private readonly NumericUpDown maxPowerUpDown = new();
    private readonly NumericUpDown randomSeedUpDown = new();
    private readonly Button runExperimentButton = new();

    private readonly PictureBox absoluteErrorChart = new();
    private readonly PictureBox relativeErrorChart = new();

    private bool suppressGridRebuild;

    public Form1()
    {
        InitializeComponent();
        ConfigureWindow();
        BuildInterface();
        HookEvents();
        ApplyProblemToUi(SampleProblems.CreateLabProblem());
        ClearOutputs();
    }

    public void PrepareDemoState()
    {
        var problem = SampleProblems.CreateLabProblem();
        ApplyProblemToUi(problem);
        PopulateSolveResult(solver.Solve(problem, includeTrace: true));
        PopulateExperimentResult(experimentRunner.Run(problem, new ExperimentSettings
        {
            ExperimentsPerRange = 25,
            MinPower = -5,
            MaxPower = -1,
            Seed = 42
        }));
    }

    public void ExportScreenshot(string path)
    {
        mainTabControl.SelectedTab = solverTabPage;
        PerformLayout();
        Refresh();
        Application.DoEvents();

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var bitmap = new Bitmap(ClientSize.Width, ClientSize.Height);
        DrawToBitmap(bitmap, new Rectangle(Point.Empty, ClientSize));
        bitmap.Save(fullPath, ImageFormat.Png);
    }

    private void ConfigureWindow()
    {
        Text = "Лабораторная работа по методам оптимизации";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1400, 900);
        ClientSize = new Size(1600, 980);
        Font = new Font("Segoe UI", 9.5f, FontStyle.Regular, GraphicsUnit.Point);
    }

    private void BuildInterface()
    {
        mainTabControl.Dock = DockStyle.Fill;
        mainTabControl.TabPages.Add(solverTabPage);
        mainTabControl.TabPages.Add(experimentTabPage);
        Controls.Add(mainTabControl);

        BuildSolverTab();
        BuildExperimentTab();
        RebuildInputGrids();
    }

    private void BuildSolverTab()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(10)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        solverTabPage.Controls.Add(root);

        var topPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            WrapContents = true,
            Padding = new Padding(0, 0, 0, 8)
        };

        ConfigureNumericUpDown(variableCountUpDown, 1, 8, 5);
        ConfigureNumericUpDown(constraintCountUpDown, 1, 8, 4);
        objectiveSenseComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        objectiveSenseComboBox.Items.AddRange(["Максимизация", "Минимизация"]);
        objectiveSenseComboBox.SelectedIndex = 0;

        loadSampleButton.Text = "Загрузить пример";
        solveButton.Text = "Решить задачу";
        clearOutputButton.Text = "Очистить вывод";

        topPanel.Controls.AddRange(
        [
            CreateCaptionLabel("Переменные:"),
            variableCountUpDown,
            CreateCaptionLabel("Ограничения:"),
            constraintCountUpDown,
            CreateCaptionLabel("Цель:"),
            objectiveSenseComboBox,
            loadSampleButton,
            solveButton,
            clearOutputButton
        ]);

        root.Controls.Add(topPanel, 0, 0);

        var workspaceSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 350
        };
        root.Controls.Add(workspaceSplit, 0, 1);

        var inputLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2
        };
        inputLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 72f));
        inputLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28f));
        inputLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 28f));
        inputLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 72f));
        workspaceSplit.Panel1.Controls.Add(inputLayout);

        var objectiveGroup = CreateGroupBox("Целевая функция");
        objectiveGroup.Controls.Add(objectiveGrid);
        ConfigureInputGrid(objectiveGrid);
        inputLayout.Controls.Add(objectiveGroup, 0, 0);
        inputLayout.SetColumnSpan(objectiveGroup, 2);

        var constraintsGroup = CreateGroupBox("Ограничения");
        constraintsGroup.Controls.Add(constraintsGrid);
        ConfigureInputGrid(constraintsGrid);
        inputLayout.Controls.Add(constraintsGroup, 0, 1);

        var boundsGroup = CreateGroupBox("Ограничения на переменные");
        boundsGroup.Controls.Add(boundsGrid);
        ConfigureInputGrid(boundsGrid);
        inputLayout.Controls.Add(boundsGroup, 1, 1);

        var outputSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 760
        };
        workspaceSplit.Panel2.Controls.Add(outputSplit);

        var leftOutputLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3
        };
        leftOutputLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 38f));
        leftOutputLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 31f));
        leftOutputLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 31f));
        outputSplit.Panel1.Controls.Add(leftOutputLayout);

        var resultGroup = CreateGroupBox("Результат решения");
        resultGroup.Controls.Add(BuildResultPanel());
        leftOutputLayout.Controls.Add(resultGroup, 0, 0);

        var canonicalGroup = CreateGroupBox("Каноническая форма");
        canonicalGroup.Controls.Add(canonicalFormTextBox);
        leftOutputLayout.Controls.Add(canonicalGroup, 0, 1);

        var transformationsGroup = CreateGroupBox("Преобразования");
        transformationsGroup.Controls.Add(transformationTextBox);
        leftOutputLayout.Controls.Add(transformationsGroup, 0, 2);

        var logGroup = CreateGroupBox("Журнал итераций");
        logGroup.Controls.Add(iterationLogTextBox);
        outputSplit.Panel2.Controls.Add(logGroup);
    }

    private void BuildExperimentTab()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(10)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        experimentTabPage.Controls.Add(root);

        var topPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            WrapContents = true,
            Padding = new Padding(0, 0, 0, 8)
        };

        ConfigureNumericUpDown(experimentCountUpDown, 5, 300, 45);
        ConfigureNumericUpDown(minPowerUpDown, -8, -1, -5);
        ConfigureNumericUpDown(maxPowerUpDown, -8, -1, -1);
        ConfigureNumericUpDown(randomSeedUpDown, 0, 100000, 42);
        runExperimentButton.Text = "Запустить эксперимент";

        topPanel.Controls.AddRange(
        [
            CreateCaptionLabel("Прогонов на диапазон:"),
            experimentCountUpDown,
            CreateCaptionLabel("Минимальная степень 10:"),
            minPowerUpDown,
            CreateCaptionLabel("Максимальная степень 10:"),
            maxPowerUpDown,
            CreateCaptionLabel("Seed:"),
            randomSeedUpDown,
            runExperimentButton
        ]);

        root.Controls.Add(topPanel, 0, 0);

        var workspaceSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 760
        };
        root.Controls.Add(workspaceSplit, 0, 1);

        var chartsLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        chartsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
        chartsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
        workspaceSplit.Panel1.Controls.Add(chartsLayout);

        var absoluteGroup = CreateGroupBox("Средняя абсолютная погрешность");
        ConfigureChart(absoluteErrorChart, "Средняя абсолютная погрешность");
        absoluteGroup.Controls.Add(absoluteErrorChart);
        chartsLayout.Controls.Add(absoluteGroup, 0, 0);

        var relativeGroup = CreateGroupBox("Средняя относительная погрешность");
        ConfigureChart(relativeErrorChart, "Средняя относительная погрешность");
        relativeGroup.Controls.Add(relativeErrorChart);
        chartsLayout.Controls.Add(relativeGroup, 0, 1);

        var rightLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        rightLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rightLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        workspaceSplit.Panel2.Controls.Add(rightLayout);

        var summaryPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            WrapContents = true,
            Padding = new Padding(0, 0, 0, 8)
        };
        experimentStatusValueLabel.AutoSize = true;
        baseObjectiveValueLabel.AutoSize = true;
        summaryPanel.Controls.AddRange(
        [
            CreateCaptionLabel("Статус:"),
            experimentStatusValueLabel,
            CreateCaptionLabel("Базовое значение целевой функции:"),
            baseObjectiveValueLabel
        ]);
        rightLayout.Controls.Add(summaryPanel, 0, 0);

        var experimentGroup = CreateGroupBox("Сводная таблица эксперимента");
        ConfigureReadOnlyGrid(experimentGrid);
        experimentGroup.Controls.Add(experimentGrid);
        rightLayout.Controls.Add(experimentGroup, 0, 1);
    }

    private Control BuildResultPanel()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        var statusPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            WrapContents = true
        };
        solveStatusValueLabel.AutoSize = true;
        objectiveValueLabel.AutoSize = true;
        statusPanel.Controls.AddRange(
        [
            CreateCaptionLabel("Статус:"),
            solveStatusValueLabel,
            CreateCaptionLabel("Оптимальное значение:"),
            objectiveValueLabel
        ]);
        layout.Controls.Add(statusPanel, 0, 0);

        var helperLabel = new Label
        {
            Text = "Значения исходных переменных",
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Padding = new Padding(0, 4, 0, 4)
        };
        layout.Controls.Add(helperLabel, 0, 1);

        ConfigureReadOnlyGrid(solutionGrid);
        layout.Controls.Add(solutionGrid, 0, 2);

        return layout;
    }

    private void HookEvents()
    {
        variableCountUpDown.ValueChanged += (_, _) => RebuildInputGrids();
        constraintCountUpDown.ValueChanged += (_, _) => RebuildInputGrids();
        loadSampleButton.Click += (_, _) => ApplyProblemToUi(SampleProblems.CreateLabProblem());
        clearOutputButton.Click += (_, _) => ClearOutputs();
        solveButton.Click += async (_, _) => await SolveCurrentProblemAsync();
        runExperimentButton.Click += async (_, _) => await RunExperimentAsync();
    }

    private void ConfigureNumericUpDown(NumericUpDown control, decimal minValue, decimal maxValue, decimal value)
    {
        control.Minimum = minValue;
        control.Maximum = maxValue;
        control.Value = value;
        control.Width = 80;
    }

    private void ConfigureInputGrid(DataGridView grid)
    {
        grid.Dock = DockStyle.Fill;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = false;
        grid.RowHeadersVisible = false;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
        grid.BackgroundColor = SystemColors.Window;
    }

    private void ConfigureReadOnlyGrid(DataGridView grid)
    {
        ConfigureInputGrid(grid);
        grid.ReadOnly = true;
        grid.MultiSelect = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
    }

    private void ConfigureChart(PictureBox chart, string title)
    {
        chart.Dock = DockStyle.Fill;
        chart.BackColor = SystemColors.Window;
        chart.BorderStyle = BorderStyle.FixedSingle;
        chart.SizeMode = PictureBoxSizeMode.StretchImage;
        chart.Tag = title;
    }

    private void RebuildInputGrids()
    {
        if (suppressGridRebuild)
        {
            return;
        }

        var objectiveValues = CaptureObjectiveValues();
        var constraintValues = CaptureConstraintValues();
        var boundValues = CaptureBoundValues();

        var variableCount = (int)variableCountUpDown.Value;
        var constraintCount = (int)constraintCountUpDown.Value;

        objectiveGrid.Columns.Clear();
        objectiveGrid.Rows.Clear();
        for (var columnIndex = 0; columnIndex < variableCount; columnIndex++)
        {
            objectiveGrid.Columns.Add($"obj_{columnIndex}", $"x{columnIndex + 1}");
        }

        objectiveGrid.Rows.Add();
        for (var columnIndex = 0; columnIndex < variableCount; columnIndex++)
        {
            objectiveGrid.Rows[0].Cells[columnIndex].Value =
                columnIndex < objectiveValues.Count ? objectiveValues[columnIndex] : "0";
        }

        constraintsGrid.Columns.Clear();
        constraintsGrid.Rows.Clear();
        for (var columnIndex = 0; columnIndex < variableCount; columnIndex++)
        {
            constraintsGrid.Columns.Add($"c_{columnIndex}", $"x{columnIndex + 1}");
        }

        var signColumn = new DataGridViewComboBoxColumn
        {
            Name = "relation",
            HeaderText = "Знак",
            FlatStyle = FlatStyle.Flat
        };
        signColumn.Items.AddRange("<=", "=", ">=");
        constraintsGrid.Columns.Add(signColumn);
        constraintsGrid.Columns.Add("rhs", "Правая часть");

        for (var rowIndex = 0; rowIndex < constraintCount; rowIndex++)
        {
            constraintsGrid.Rows.Add();
            for (var columnIndex = 0; columnIndex < variableCount; columnIndex++)
            {
                constraintsGrid.Rows[rowIndex].Cells[columnIndex].Value =
                    rowIndex < constraintValues.Coefficients.Count && columnIndex < constraintValues.Coefficients[rowIndex].Count
                        ? constraintValues.Coefficients[rowIndex][columnIndex]
                        : "0";
            }

            constraintsGrid.Rows[rowIndex].Cells["relation"].Value =
                rowIndex < constraintValues.Relations.Count ? constraintValues.Relations[rowIndex] : "=";
            constraintsGrid.Rows[rowIndex].Cells["rhs"].Value =
                rowIndex < constraintValues.RightHandSides.Count ? constraintValues.RightHandSides[rowIndex] : "0";
        }

        boundsGrid.Columns.Clear();
        boundsGrid.Rows.Clear();
        boundsGrid.Columns.Add("variable", "Переменная");
        var boundColumn = new DataGridViewComboBoxColumn
        {
            Name = "bound",
            HeaderText = "Тип",
            FlatStyle = FlatStyle.Flat
        };
        boundColumn.Items.AddRange(">= 0", "free");
        boundsGrid.Columns.Add(boundColumn);

        for (var rowIndex = 0; rowIndex < variableCount; rowIndex++)
        {
            boundsGrid.Rows.Add($"x{rowIndex + 1}", rowIndex < boundValues.Count ? boundValues[rowIndex] : ">= 0");
        }

        if (boundsGrid.Columns["variable"] is DataGridViewColumn variableColumn)
        {
            variableColumn.ReadOnly = true;
        }
    }

    private void ApplyProblemToUi(LinearProgrammingProblem problem)
    {
        suppressGridRebuild = true;
        variableCountUpDown.Value = problem.VariableNames.Length;
        constraintCountUpDown.Value = problem.Constraints.Count;
        objectiveSenseComboBox.SelectedIndex = problem.Sense == ObjectiveSense.Maximize ? 0 : 1;
        suppressGridRebuild = false;

        RebuildInputGrids();

        for (var columnIndex = 0; columnIndex < problem.ObjectiveCoefficients.Length; columnIndex++)
        {
            objectiveGrid.Rows[0].Cells[columnIndex].Value = FormattingHelpers.FormatNumber(problem.ObjectiveCoefficients[columnIndex]);
        }

        for (var rowIndex = 0; rowIndex < problem.Constraints.Count; rowIndex++)
        {
            var constraint = problem.Constraints[rowIndex];
            for (var columnIndex = 0; columnIndex < constraint.Coefficients.Length; columnIndex++)
            {
                constraintsGrid.Rows[rowIndex].Cells[columnIndex].Value = FormattingHelpers.FormatNumber(constraint.Coefficients[columnIndex]);
            }

            constraintsGrid.Rows[rowIndex].Cells["relation"].Value = RelationToText(constraint.Relation);
            constraintsGrid.Rows[rowIndex].Cells["rhs"].Value = FormattingHelpers.FormatNumber(constraint.RightHandSide);
        }

        for (var rowIndex = 0; rowIndex < problem.VariableBounds.Length; rowIndex++)
        {
            boundsGrid.Rows[rowIndex].Cells["bound"].Value = BoundToText(problem.VariableBounds[rowIndex]);
        }

        ClearOutputs();
    }

    private async Task SolveCurrentProblemAsync()
    {
        try
        {
            SetBusyState(true);
            var problem = BuildProblemFromUi();
            var result = await Task.Run(() => solver.Solve(problem, includeTrace: true));
            PopulateSolveResult(result);
        }
        catch (InvalidOperationException exception)
        {
            ShowError(exception.Message);
        }
        finally
        {
            SetBusyState(false);
        }
    }

    private async Task RunExperimentAsync()
    {
        try
        {
            SetBusyState(true);
            var problem = BuildProblemFromUi();
            var settings = new ExperimentSettings
            {
                ExperimentsPerRange = (int)experimentCountUpDown.Value,
                MinPower = Decimal.ToInt32(minPowerUpDown.Value),
                MaxPower = Decimal.ToInt32(maxPowerUpDown.Value),
                Seed = (int)randomSeedUpDown.Value
            };

            var result = await Task.Run(() => experimentRunner.Run(problem, settings));
            PopulateExperimentResult(result);
            mainTabControl.SelectedTab = experimentTabPage;
        }
        catch (InvalidOperationException exception)
        {
            ShowError(exception.Message);
        }
        finally
        {
            SetBusyState(false);
        }
    }

    private LinearProgrammingProblem BuildProblemFromUi()
    {
        var variableCount = (int)variableCountUpDown.Value;
        var constraintCount = (int)constraintCountUpDown.Value;

        var objective = new double[variableCount];
        for (var columnIndex = 0; columnIndex < variableCount; columnIndex++)
        {
            objective[columnIndex] = ParseDoubleCell(
                objectiveGrid.Rows[0].Cells[columnIndex].Value,
                $"коэффициент целевой функции x{columnIndex + 1}");
        }

        var bounds = new VariableBoundType[variableCount];
        for (var rowIndex = 0; rowIndex < variableCount; rowIndex++)
        {
            var text = boundsGrid.Rows[rowIndex].Cells["bound"].Value?.ToString() ?? ">= 0";
            bounds[rowIndex] = ParseBound(text);
        }

        var constraints = new List<ConstraintInput>(constraintCount);
        for (var rowIndex = 0; rowIndex < constraintCount; rowIndex++)
        {
            var coefficients = new double[variableCount];
            for (var columnIndex = 0; columnIndex < variableCount; columnIndex++)
            {
                coefficients[columnIndex] = ParseDoubleCell(
                    constraintsGrid.Rows[rowIndex].Cells[columnIndex].Value,
                    $"коэффициент ограничения {rowIndex + 1}, x{columnIndex + 1}");
            }

            var relationText = constraintsGrid.Rows[rowIndex].Cells["relation"].Value?.ToString() ?? "=";
            var rhs = ParseDoubleCell(
                constraintsGrid.Rows[rowIndex].Cells["rhs"].Value,
                $"правая часть ограничения {rowIndex + 1}");

            constraints.Add(new ConstraintInput
            {
                Coefficients = coefficients,
                Relation = ParseRelation(relationText),
                RightHandSide = rhs
            });
        }

        return new LinearProgrammingProblem
        {
            Sense = objectiveSenseComboBox.SelectedIndex == 0 ? ObjectiveSense.Maximize : ObjectiveSense.Minimize,
            VariableNames = Enumerable.Range(1, variableCount).Select(index => $"x{index}").ToArray(),
            ObjectiveCoefficients = objective,
            VariableBounds = bounds,
            Constraints = constraints
        };
    }

    private void PopulateSolveResult(SimplexSolveResult result)
    {
        solveStatusValueLabel.Text = result.Message;
        objectiveValueLabel.Text = result.Status == SimplexStatus.Optimal
            ? FormattingHelpers.FormatNumber(result.ObjectiveValue)
            : "н/д";

        solutionGrid.Columns.Clear();
        solutionGrid.Rows.Clear();
        solutionGrid.Columns.Add("variable", "Переменная");
        solutionGrid.Columns.Add("value", "Значение");

        if (result.Status == SimplexStatus.Optimal)
        {
            for (var index = 0; index < result.VariableNames.Length; index++)
            {
                solutionGrid.Rows.Add(result.VariableNames[index], FormattingHelpers.FormatNumber(result.VariableValues[index]));
            }
        }

        canonicalFormTextBox.Text = result.CanonicalForm;
        transformationTextBox.Text = result.TransformationSummary;
        iterationLogTextBox.Text = result.IterationLog;
    }

    private void PopulateExperimentResult(ExperimentResult result)
    {
        experimentStatusValueLabel.Text = result.Message;
        baseObjectiveValueLabel.Text = result.Status == SimplexStatus.Optimal
            ? FormattingHelpers.FormatNumber(result.BaseObjectiveValue)
            : "н/д";

        experimentGrid.Columns.Clear();
        experimentGrid.Rows.Clear();
        experimentGrid.Columns.Add("range", "Диапазон ошибки");
        experimentGrid.Columns.Add("meanAbs", "Средняя абсолютная");
        experimentGrid.Columns.Add("stdAbs", "СКО абсолютной");
        experimentGrid.Columns.Add("meanRel", "Средняя относительная");
        experimentGrid.Columns.Add("stdRel", "СКО относительной");
        experimentGrid.Columns.Add("runs", "Успешных прогонов");

        if (result.Status == SimplexStatus.Optimal)
        {
            foreach (var row in result.Rows)
            {
                experimentGrid.Rows.Add(
                    FormattingHelpers.FormatNumber(row.ErrorRange),
                    FormattingHelpers.FormatNumber(row.MeanAbsoluteError),
                    FormattingHelpers.FormatNumber(row.StandardDeviationAbsoluteError),
                    FormattingHelpers.FormatNumber(row.MeanRelativeError),
                    FormattingHelpers.FormatNumber(row.StandardDeviationRelativeError),
                    row.SuccessfulRuns.ToString(CultureInfo.InvariantCulture));
            }

            PopulateChart(
                absoluteErrorChart,
                result.Rows,
                "Средняя абсолютная",
                row => row.MeanAbsoluteError);

            PopulateChart(
                relativeErrorChart,
                result.Rows,
                "Средняя относительная",
                row => row.MeanRelativeError);
        }
        else
        {
            ClearChart(absoluteErrorChart);
            ClearChart(relativeErrorChart);
        }
    }

    private void PopulateChart(
        PictureBox chart,
        IReadOnlyList<ExperimentSummaryRow> rows,
        string seriesName,
        Func<ExperimentSummaryRow, double> selector)
    {
        ClearChart(chart);
        chart.Image = CreateChartBitmap(
            rows,
            selector,
            chart.Tag?.ToString() ?? seriesName,
            seriesName == "Средняя абсолютная" ? Color.SteelBlue : Color.Firebrick,
            chart.Width);
    }

    private void ClearChart(PictureBox chart)
    {
        chart.Image?.Dispose();
        chart.Image = null;
    }

    private void SetBusyState(bool busy)
    {
        UseWaitCursor = busy;
        solveButton.Enabled = !busy;
        runExperimentButton.Enabled = !busy;
        loadSampleButton.Enabled = !busy;
        clearOutputButton.Enabled = !busy;
    }

    private void ClearOutputs()
    {
        solveStatusValueLabel.Text = "ожидание ввода";
        objectiveValueLabel.Text = "-";
        experimentStatusValueLabel.Text = "эксперимент не запущен";
        baseObjectiveValueLabel.Text = "-";

        solutionGrid.Columns.Clear();
        solutionGrid.Rows.Clear();
        experimentGrid.Columns.Clear();
        experimentGrid.Rows.Clear();

        canonicalFormTextBox.Clear();
        transformationTextBox.Clear();
        iterationLogTextBox.Clear();

        ClearChart(absoluteErrorChart);
        ClearChart(relativeErrorChart);
    }

    private static Label CreateCaptionLabel(string text)
    {
        return new Label
        {
            AutoSize = true,
            Text = text,
            Padding = new Padding(0, 6, 0, 0)
        };
    }

    private static GroupBox CreateGroupBox(string text)
    {
        return new GroupBox
        {
            Dock = DockStyle.Fill,
            Text = text,
            Padding = new Padding(8)
        };
    }

    private static TextBox CreateReadOnlyTextBox()
    {
        return new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Font = new Font("Consolas", 9.5f, FontStyle.Regular, GraphicsUnit.Point)
        };
    }

    private Bitmap CreateChartBitmap(
        IReadOnlyList<ExperimentSummaryRow> rows,
        Func<ExperimentSummaryRow, double> selector,
        string title,
        Color lineColor,
        int preferredWidth)
    {
        var width = Math.Max(720, preferredWidth > 0 ? preferredWidth : 720);
        var height = 320;
        var bitmap = new Bitmap(width, height);

        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.White);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        using var titleFont = new Font("Segoe UI", 10.5f, FontStyle.Bold, GraphicsUnit.Point);
        using var axisFont = new Font("Segoe UI", 8.5f, FontStyle.Regular, GraphicsUnit.Point);
        using var gridPen = new Pen(Color.Gainsboro, 1f);
        using var axisPen = new Pen(Color.DimGray, 1.5f);
        using var linePen = new Pen(lineColor, 2.5f);
        using var pointBrush = new SolidBrush(lineColor);
        using var textBrush = new SolidBrush(Color.Black);

        var plotArea = new Rectangle(70, 40, width - 110, height - 90);
        graphics.DrawString(title, titleFont, textBrush, 12, 10);

        if (rows.Count == 0)
        {
            graphics.DrawRectangle(axisPen, plotArea);
            graphics.DrawString("Нет данных для построения графика.", axisFont, textBrush, plotArea.Left + 12, plotArea.Top + 12);
            return bitmap;
        }

        var xLogs = rows.Select(row => Math.Log10(row.ErrorRange)).ToArray();
        var yValues = rows.Select(selector).ToArray();
        var positiveYValues = yValues.Where(value => value > 0).ToArray();
        var useLogY = positiveYValues.Length == yValues.Length;
        var yCoordinates = useLogY ? positiveYValues.Select(Math.Log10).ToArray() : yValues.ToArray();
        var minX = xLogs.Min();
        var maxX = xLogs.Max();
        var minY = yCoordinates.Min();
        var maxY = yCoordinates.Max();

        if (Math.Abs(maxX - minX) < 1e-9)
        {
            minX -= 1.0;
            maxX += 1.0;
        }

        if (Math.Abs(maxY - minY) < 1e-9)
        {
            minY -= 1.0;
            maxY += 1.0;
        }

        for (var step = 0; step <= 4; step++)
        {
            var x = plotArea.Left + plotArea.Width * step / 4f;
            var y = plotArea.Top + plotArea.Height * step / 4f;
            graphics.DrawLine(gridPen, x, plotArea.Top, x, plotArea.Bottom);
            graphics.DrawLine(gridPen, plotArea.Left, y, plotArea.Right, y);
        }

        graphics.DrawRectangle(axisPen, plotArea);

        var points = new PointF[rows.Count];
        for (var index = 0; index < rows.Count; index++)
        {
            var normalizedX = (float)((xLogs[index] - minX) / (maxX - minX));
            var currentY = useLogY ? Math.Log10(yValues[index]) : yValues[index];
            var normalizedY = (float)((currentY - minY) / (maxY - minY));

            points[index] = new PointF(
                plotArea.Left + normalizedX * plotArea.Width,
                plotArea.Bottom - normalizedY * plotArea.Height);
        }

        if (points.Length > 1)
        {
            graphics.DrawLines(linePen, points);
        }

        foreach (var point in points)
        {
            graphics.FillEllipse(pointBrush, point.X - 4, point.Y - 4, 8, 8);
        }

        for (var index = 0; index < rows.Count; index++)
        {
            var label = FormattingHelpers.FormatNumber(rows[index].ErrorRange);
            var labelSize = graphics.MeasureString(label, axisFont);
            graphics.DrawString(
                label,
                axisFont,
                textBrush,
                points[index].X - labelSize.Width / 2,
                plotArea.Bottom + 6);
        }

        for (var step = 0; step <= 4; step++)
        {
            var t = step / 4f;
            var yValue = maxY - (maxY - minY) * t;
            var label = useLogY
                ? $"1e{FormattingHelpers.FormatNumber(yValue)}"
                : FormattingHelpers.FormatNumber(yValue);
            var labelSize = graphics.MeasureString(label, axisFont);
            var y = plotArea.Top + plotArea.Height * t - labelSize.Height / 2;
            graphics.DrawString(label, axisFont, textBrush, 8, y);
        }

        graphics.DrawString("Диапазон ошибки", axisFont, textBrush, plotArea.Left + plotArea.Width / 2f - 48, height - 28);
        graphics.DrawString("Ошибка", axisFont, textBrush, 12, plotArea.Top - 20);

        return bitmap;
    }

    private double ParseDoubleCell(object? rawValue, string description)
    {
        var text = rawValue?.ToString()?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0.0;
        }

        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
        {
            return value;
        }

        throw new InvalidOperationException($"Не удалось распознать число для поля \"{description}\".");
    }

    private static ConstraintRelation ParseRelation(string text)
    {
        return text switch
        {
            "<=" => ConstraintRelation.LessOrEqual,
            "=" => ConstraintRelation.Equal,
            ">=" => ConstraintRelation.GreaterOrEqual,
            _ => throw new InvalidOperationException("Неизвестный тип ограничения.")
        };
    }

    private static string RelationToText(ConstraintRelation relation)
    {
        return relation switch
        {
            ConstraintRelation.LessOrEqual => "<=",
            ConstraintRelation.Equal => "=",
            ConstraintRelation.GreaterOrEqual => ">=",
            _ => "="
        };
    }

    private static VariableBoundType ParseBound(string text)
    {
        return text == "free" ? VariableBoundType.Free : VariableBoundType.NonNegative;
    }

    private static string BoundToText(VariableBoundType bound)
    {
        return bound == VariableBoundType.Free ? "free" : ">= 0";
    }

    private List<string> CaptureObjectiveValues()
    {
        var values = new List<string>();
        if (objectiveGrid.Rows.Count == 0)
        {
            return values;
        }

        foreach (DataGridViewCell cell in objectiveGrid.Rows[0].Cells)
        {
            values.Add(cell.Value?.ToString() ?? "0");
        }

        return values;
    }

    private (List<List<string>> Coefficients, List<string> Relations, List<string> RightHandSides) CaptureConstraintValues()
    {
        var coefficients = new List<List<string>>();
        var relations = new List<string>();
        var rightHandSides = new List<string>();

        foreach (DataGridViewRow row in constraintsGrid.Rows)
        {
            var rowValues = new List<string>();
            for (var columnIndex = 0; columnIndex < constraintsGrid.Columns.Count; columnIndex++)
            {
                var columnName = constraintsGrid.Columns[columnIndex].Name;
                if (columnName == "relation")
                {
                    relations.Add(row.Cells[columnIndex].Value?.ToString() ?? "=");
                }
                else if (columnName == "rhs")
                {
                    rightHandSides.Add(row.Cells[columnIndex].Value?.ToString() ?? "0");
                }
                else
                {
                    rowValues.Add(row.Cells[columnIndex].Value?.ToString() ?? "0");
                }
            }

            coefficients.Add(rowValues);
        }

        return (coefficients, relations, rightHandSides);
    }

    private List<string> CaptureBoundValues()
    {
        var values = new List<string>();
        foreach (DataGridViewRow row in boundsGrid.Rows)
        {
            values.Add(row.Cells["bound"].Value?.ToString() ?? ">= 0");
        }

        return values;
    }

    private void ShowError(string message)
    {
        MessageBox.Show(this, message, "Ошибка ввода", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
}

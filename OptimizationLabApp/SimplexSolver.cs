using System.Text;

namespace OptimizationLabApp;

public sealed class SimplexSolver
{
    private const double Tolerance = 1e-9;
    private const int MaxIterations = 500;

    public SimplexSolveResult Solve(LinearProgrammingProblem problem, bool includeTrace = true)
    {
        try
        {
            Validate(problem);

            var trace = includeTrace ? new StringBuilder() : null;
            var canonical = Canonicalize(problem);

            if (trace is not null)
            {
                trace.AppendLine("Подготовка задачи к решению.");
                trace.AppendLine(canonical.TransformationSummary);
                trace.AppendLine();
            }

            var phaseOneObjective = new double[canonical.VariableNames.Length];
            foreach (var artificialIndex in canonical.ArtificialIndices)
            {
                phaseOneObjective[artificialIndex] = 1.0;
            }

            var phaseOneResult = RunSimplexMinimization(
                canonical.Matrix,
                canonical.RightHandSide,
                phaseOneObjective,
                canonical.InitialBasis.ToList(),
                canonical.VariableNames,
                trace,
                "Фаза I");

            if (phaseOneResult.Status != SimplexStatus.Optimal)
            {
                return new SimplexSolveResult
                {
                    Status = phaseOneResult.Status,
                    Message = phaseOneResult.Message,
                    CanonicalForm = canonical.CanonicalForm,
                    TransformationSummary = canonical.TransformationSummary,
                    IterationLog = trace?.ToString() ?? string.Empty
                };
            }

            if (Dot(phaseOneObjective, phaseOneResult.Solution) > 1e-7)
            {
                return new SimplexSolveResult
                {
                    Status = SimplexStatus.Infeasible,
                    Message = "Допустимое решение отсутствует: на фазе I сумма искусственных переменных осталась положительной.",
                    CanonicalForm = canonical.CanonicalForm,
                    TransformationSummary = canonical.TransformationSummary,
                    IterationLog = trace?.ToString() ?? string.Empty
                };
            }

            var phaseTwoData = PreparePhaseTwo(canonical, phaseOneResult, trace);
            var phaseTwoResult = RunSimplexMinimization(
                phaseTwoData.Matrix,
                phaseTwoData.RightHandSide,
                phaseTwoData.Objective,
                phaseTwoData.InitialBasis.ToList(),
                phaseTwoData.VariableNames,
                trace,
                "Фаза II");

            if (phaseTwoResult.Status != SimplexStatus.Optimal)
            {
                return new SimplexSolveResult
                {
                    Status = phaseTwoResult.Status,
                    Message = phaseTwoResult.Message,
                    CanonicalForm = canonical.CanonicalForm,
                    TransformationSummary = canonical.TransformationSummary,
                    IterationLog = trace?.ToString() ?? string.Empty
                };
            }

            var variableValues = RecoverOriginalVariables(problem.VariableNames.Length, phaseTwoData.RecoveryTerms, phaseTwoResult.Solution);
            var internalObjective = Dot(phaseTwoData.Objective, phaseTwoResult.Solution);
            var finalObjective = problem.Sense == ObjectiveSense.Maximize ? -internalObjective : internalObjective;

            if (trace is not null)
            {
                trace.AppendLine("Решение исходной задачи восстановлено из канонической формы.");
                trace.AppendLine($"Оптимальное значение целевой функции: {FormattingHelpers.FormatNumber(finalObjective)}");
            }

            return new SimplexSolveResult
            {
                Status = SimplexStatus.Optimal,
                Message = "Оптимальное решение найдено.",
                ObjectiveValue = Sanitize(finalObjective),
                VariableNames = (string[])problem.VariableNames.Clone(),
                VariableValues = variableValues,
                CanonicalForm = canonical.CanonicalForm,
                TransformationSummary = canonical.TransformationSummary,
                IterationLog = trace?.ToString() ?? string.Empty
            };
        }
        catch (ArgumentException exception)
        {
            return new SimplexSolveResult
            {
                Status = SimplexStatus.InvalidInput,
                Message = exception.Message
            };
        }
        catch (InvalidOperationException exception)
        {
            return new SimplexSolveResult
            {
                Status = SimplexStatus.InvalidInput,
                Message = exception.Message
            };
        }
    }

    private static void Validate(LinearProgrammingProblem problem)
    {
        if (problem.VariableNames.Length == 0)
        {
            throw new ArgumentException("Число переменных должно быть положительным.");
        }

        if (problem.Constraints.Count == 0)
        {
            throw new ArgumentException("Число ограничений должно быть положительным.");
        }

        if (problem.ObjectiveCoefficients.Length != problem.VariableNames.Length)
        {
            throw new ArgumentException("Размер вектора целевой функции не совпадает с числом переменных.");
        }

        if (problem.VariableBounds.Length != problem.VariableNames.Length)
        {
            throw new ArgumentException("Размер массива ограничений на переменные не совпадает с числом переменных.");
        }

        foreach (var constraint in problem.Constraints)
        {
            if (constraint.Coefficients.Length != problem.VariableNames.Length)
            {
                throw new ArgumentException("Каждое ограничение должно содержать коэффициенты для всех переменных.");
            }
        }
    }

    private static CanonicalFormData Canonicalize(LinearProgrammingProblem problem)
    {
        var internalVariableNames = new List<string>();
        var objective = new List<double>();
        var recoveryTerms = new List<RecoveryTerm>[problem.VariableNames.Length];
        var transformationSteps = new List<string>();
        var originalToInternal = new List<(int PositiveIndex, int? NegativeIndex)>(problem.VariableNames.Length);
        var objectiveMultiplier = problem.Sense == ObjectiveSense.Maximize ? -1.0 : 1.0;

        if (problem.Sense == ObjectiveSense.Maximize)
        {
            transformationSteps.Add("Целевая функция умножена на -1 для перехода к задаче минимизации во внутреннем решателе.");
        }

        for (var index = 0; index < problem.VariableNames.Length; index++)
        {
            recoveryTerms[index] = new List<RecoveryTerm>();
            var adjustedCoefficient = objectiveMultiplier * problem.ObjectiveCoefficients[index];
            var variableName = problem.VariableNames[index];

            if (problem.VariableBounds[index] == VariableBoundType.NonNegative)
            {
                var newIndex = internalVariableNames.Count;
                internalVariableNames.Add(variableName);
                objective.Add(adjustedCoefficient);
                recoveryTerms[index].Add(new RecoveryTerm(newIndex, 1.0));
                originalToInternal.Add((newIndex, null));
            }
            else
            {
                var positiveIndex = internalVariableNames.Count;
                var negativeIndex = positiveIndex + 1;

                internalVariableNames.Add($"{variableName}_pos");
                internalVariableNames.Add($"{variableName}_neg");
                objective.Add(adjustedCoefficient);
                objective.Add(-adjustedCoefficient);

                recoveryTerms[index].Add(new RecoveryTerm(positiveIndex, 1.0));
                recoveryTerms[index].Add(new RecoveryTerm(negativeIndex, -1.0));
                originalToInternal.Add((positiveIndex, negativeIndex));

                transformationSteps.Add($"{variableName} заменена разностью: {variableName} = {variableName}_pos - {variableName}_neg.");
            }
        }

        var rows = new List<List<double>>();
        var rightHandSide = new List<double>();
        var initialBasis = new List<int>();
        var artificialIndices = new List<int>();

        for (var rowIndex = 0; rowIndex < problem.Constraints.Count; rowIndex++)
        {
            var sourceConstraint = problem.Constraints[rowIndex];
            var row = new List<double>(new double[internalVariableNames.Count]);

            for (var columnIndex = 0; columnIndex < sourceConstraint.Coefficients.Length; columnIndex++)
            {
                var coefficient = sourceConstraint.Coefficients[columnIndex];
                var mapping = originalToInternal[columnIndex];
                row[mapping.PositiveIndex] += coefficient;
                if (mapping.NegativeIndex.HasValue)
                {
                    row[mapping.NegativeIndex.Value] -= coefficient;
                }
            }

            var relation = sourceConstraint.Relation;
            var rhs = sourceConstraint.RightHandSide;
            if (rhs < 0)
            {
                rhs = -rhs;
                relation = Flip(relation);

                for (var index = 0; index < row.Count; index++)
                {
                    row[index] = -row[index];
                }

                transformationSteps.Add($"Ограничение {rowIndex + 1} умножено на -1 для получения неотрицательной правой части.");
            }

            switch (relation)
            {
                case ConstraintRelation.LessOrEqual:
                    AddColumn(rows, row, internalVariableNames, objective, $"s{rowIndex + 1}", 0.0, 1.0);
                    initialBasis.Add(internalVariableNames.Count - 1);
                    transformationSteps.Add($"К ограничению {rowIndex + 1} добавлена переменная запаса s{rowIndex + 1}.");
                    break;
                case ConstraintRelation.GreaterOrEqual:
                    AddColumn(rows, row, internalVariableNames, objective, $"e{rowIndex + 1}", 0.0, -1.0);
                    AddColumn(rows, row, internalVariableNames, objective, $"a{rowIndex + 1}", 0.0, 1.0);
                    artificialIndices.Add(internalVariableNames.Count - 1);
                    initialBasis.Add(internalVariableNames.Count - 1);
                    transformationSteps.Add($"К ограничению {rowIndex + 1} добавлены переменная избытка e{rowIndex + 1} и искусственная переменная a{rowIndex + 1}.");
                    break;
                case ConstraintRelation.Equal:
                    AddColumn(rows, row, internalVariableNames, objective, $"a{rowIndex + 1}", 0.0, 1.0);
                    artificialIndices.Add(internalVariableNames.Count - 1);
                    initialBasis.Add(internalVariableNames.Count - 1);
                    transformationSteps.Add($"Для ограничения {rowIndex + 1} введена искусственная переменная a{rowIndex + 1}.");
                    break;
                default:
                    throw new InvalidOperationException("Неизвестный тип ограничения.");
            }

            rows.Add(row);
            rightHandSide.Add(rhs);
        }

        var matrix = ToMatrix(rows);
        var objectiveArray = objective.ToArray();
        var variableNames = internalVariableNames.ToArray();

        var phaseTwoColumns = Enumerable.Range(0, variableNames.Length)
            .Where(index => !artificialIndices.Contains(index))
            .ToArray();

        var displayMatrix = SelectColumns(matrix, phaseTwoColumns);
        var displayObjective = phaseTwoColumns.Select(index => objectiveArray[index]).ToArray();
        var displayNames = phaseTwoColumns.Select(index => variableNames[index]).ToArray();
        var canonicalForm = BuildCanonicalForm(displayMatrix, rightHandSide.ToArray(), displayObjective, displayNames);

        return new CanonicalFormData
        {
            Matrix = matrix,
            RightHandSide = rightHandSide.ToArray(),
            Objective = objectiveArray,
            VariableNames = variableNames,
            InitialBasis = initialBasis.ToArray(),
            ArtificialIndices = artificialIndices.ToArray(),
            RecoveryTerms = recoveryTerms,
            CanonicalForm = canonicalForm,
            TransformationSummary = transformationSteps.Count == 0
                ? "Дополнительных преобразований не потребовалось."
                : string.Join(Environment.NewLine, transformationSteps.Select((step, index) => $"{index + 1}. {step}"))
        };
    }

    private static PhaseTwoData PreparePhaseTwo(
        CanonicalFormData canonical,
        SimplexPhaseResult phaseOneResult,
        StringBuilder? trace)
    {
        var workingMatrix = canonical.Matrix;
        var workingRightHandSide = canonical.RightHandSide;
        var basis = new List<int>(phaseOneResult.Basis);
        var artificialSet = canonical.ArtificialIndices.ToHashSet();

        var rowIndex = 0;
        while (rowIndex < basis.Count)
        {
            if (!artificialSet.Contains(basis[rowIndex]))
            {
                rowIndex++;
                continue;
            }

            var basisMatrix = GetColumns(workingMatrix, basis);
            var inverseBasis = Invert(basisMatrix);

            var replacement = -1;
            for (var columnIndex = 0; columnIndex < workingMatrix.GetLength(1); columnIndex++)
            {
                if (artificialSet.Contains(columnIndex) || basis.Contains(columnIndex))
                {
                    continue;
                }

                var direction = Multiply(inverseBasis, GetColumn(workingMatrix, columnIndex));
                if (Math.Abs(direction[rowIndex]) > Tolerance)
                {
                    replacement = columnIndex;
                    break;
                }
            }

            if (replacement >= 0)
            {
                if (trace is not null)
                {
                    trace.AppendLine(
                        $"Фаза II: искусственная переменная {canonical.VariableNames[basis[rowIndex]]} вытеснена переменной {canonical.VariableNames[replacement]}.");
                }

                basis[rowIndex] = replacement;
                rowIndex++;
                continue;
            }

            if (trace is not null)
            {
                trace.AppendLine(
                    $"Фаза II: ограничение {rowIndex + 1} оказалось избыточным и было удалено после исключения искусственной переменной.");
            }

            workingMatrix = RemoveRow(workingMatrix, rowIndex);
            workingRightHandSide = RemoveElement(workingRightHandSide, rowIndex);
            basis.RemoveAt(rowIndex);
        }

        var keptColumns = Enumerable.Range(0, canonical.VariableNames.Length)
            .Where(index => !artificialSet.Contains(index))
            .ToArray();

        var oldToNew = new Dictionary<int, int>(keptColumns.Length);
        for (var index = 0; index < keptColumns.Length; index++)
        {
            oldToNew[keptColumns[index]] = index;
        }

        var remappedBasis = basis.Select(index =>
        {
            if (!oldToNew.TryGetValue(index, out var newIndex))
            {
                throw new InvalidOperationException("Не удалось исключить искусственные переменные из базиса.");
            }

            return newIndex;
        }).ToArray();

        var remappedRecovery = new List<RecoveryTerm>[canonical.RecoveryTerms.Length];
        for (var variableIndex = 0; variableIndex < canonical.RecoveryTerms.Length; variableIndex++)
        {
            remappedRecovery[variableIndex] = new List<RecoveryTerm>();
            foreach (var recoveryTerm in canonical.RecoveryTerms[variableIndex])
            {
                if (oldToNew.TryGetValue(recoveryTerm.Index, out var newIndex))
                {
                    remappedRecovery[variableIndex].Add(new RecoveryTerm(newIndex, recoveryTerm.Factor));
                }
            }
        }

        return new PhaseTwoData
        {
            Matrix = SelectColumns(workingMatrix, keptColumns),
            RightHandSide = workingRightHandSide,
            Objective = keptColumns.Select(index => canonical.Objective[index]).ToArray(),
            VariableNames = keptColumns.Select(index => canonical.VariableNames[index]).ToArray(),
            InitialBasis = remappedBasis,
            RecoveryTerms = remappedRecovery
        };
    }

    private static SimplexPhaseResult RunSimplexMinimization(
        double[,] matrix,
        double[] rightHandSide,
        double[] objective,
        List<int> basis,
        string[] variableNames,
        StringBuilder? trace,
        string phaseName)
    {
        var rowCount = matrix.GetLength(0);
        var columnCount = matrix.GetLength(1);

        if (basis.Count != rowCount)
        {
            throw new InvalidOperationException("Число базисных переменных должно совпадать с числом ограничений.");
        }

        for (var iteration = 1; iteration <= MaxIterations; iteration++)
        {
            var basisMatrix = GetColumns(matrix, basis);
            var inverseBasis = Invert(basisMatrix);
            var basicValues = Multiply(inverseBasis, rightHandSide);
            var basisCosts = basis.Select(index => objective[index]).ToArray();
            var potentials = MultiplyRowVector(basisCosts, inverseBasis);

            for (var index = 0; index < basicValues.Length; index++)
            {
                if (basicValues[index] < -1e-7)
                {
                    throw new InvalidOperationException(
                        $"Найдено отрицательное базисное значение {FormattingHelpers.FormatNumber(basicValues[index])}. Проверьте корректность постановки задачи.");
                }

                basicValues[index] = Sanitize(basicValues[index]);
            }

            var reducedCosts = new double[columnCount];
            for (var columnIndex = 0; columnIndex < columnCount; columnIndex++)
            {
                reducedCosts[columnIndex] = objective[columnIndex] - Dot(potentials, GetColumn(matrix, columnIndex));
                reducedCosts[columnIndex] = Sanitize(reducedCosts[columnIndex]);
            }

            var currentObjective = Dot(basisCosts, basicValues);
            var basisSet = basis.ToHashSet();
            var enteringIndex = -1;
            var minimumReducedCost = -Tolerance;

            for (var columnIndex = 0; columnIndex < columnCount; columnIndex++)
            {
                if (basisSet.Contains(columnIndex))
                {
                    continue;
                }

                if (reducedCosts[columnIndex] < minimumReducedCost)
                {
                    minimumReducedCost = reducedCosts[columnIndex];
                    enteringIndex = columnIndex;
                }
            }

            if (trace is not null)
            {
                AppendIterationHeader(trace, phaseName, iteration);
                trace.AppendLine($"Базис: {string.Join(", ", basis.Select(index => variableNames[index]))}");
                trace.AppendLine($"Значения базисных переменных: {FormatNamedVector(basicValues, basis.Select(index => variableNames[index]).ToArray())}");
                trace.AppendLine($"Текущее значение внутренней целевой функции: {FormattingHelpers.FormatNumber(currentObjective)}");
                trace.AppendLine($"Оценки: {FormatNamedVector(reducedCosts, variableNames)}");
            }

            if (enteringIndex == -1)
            {
                if (trace is not null)
                {
                    trace.AppendLine("Отрицательных оценок нет. На этой фазе найден оптимум.");
                    trace.AppendLine();
                }

                var solution = new double[columnCount];
                for (var index = 0; index < basis.Count; index++)
                {
                    solution[basis[index]] = basicValues[index];
                }

                for (var index = 0; index < solution.Length; index++)
                {
                    solution[index] = Sanitize(solution[index]);
                }

                return new SimplexPhaseResult
                {
                    Status = SimplexStatus.Optimal,
                    Message = "Оптимум найден.",
                    Solution = solution,
                    Basis = basis,
                    ObjectiveValue = Sanitize(currentObjective)
                };
            }

            var directionVector = Multiply(inverseBasis, GetColumn(matrix, enteringIndex));
            var ratios = new double[rowCount];
            var leavingRow = -1;
            var minimumRatio = double.PositiveInfinity;

            for (var index = 0; index < rowCount; index++)
            {
                if (directionVector[index] > Tolerance)
                {
                    ratios[index] = basicValues[index] / directionVector[index];
                    ratios[index] = Sanitize(ratios[index]);

                    if (ratios[index] + Tolerance < minimumRatio)
                    {
                        minimumRatio = ratios[index];
                        leavingRow = index;
                    }
                }
                else
                {
                    ratios[index] = double.PositiveInfinity;
                }
            }

            if (trace is not null)
            {
                trace.AppendLine($"Входящая переменная: {variableNames[enteringIndex]}");
                trace.AppendLine($"Направление: {FormatVector(directionVector)}");
                trace.AppendLine($"Отношения: {FormatNamedVector(ratios, basis.Select(index => variableNames[index]).ToArray())}");
            }

            if (leavingRow == -1)
            {
                if (trace is not null)
                {
                    trace.AppendLine("Положительных элементов в направляющем столбце нет. Задача неограничена.");
                    trace.AppendLine();
                }

                return new SimplexPhaseResult
                {
                    Status = SimplexStatus.Unbounded,
                    Message = "Целевая функция не ограничена на допустимой области.",
                    Solution = Array.Empty<double>(),
                    Basis = basis,
                    ObjectiveValue = double.NaN
                };
            }

            if (trace is not null)
            {
                trace.AppendLine($"Выходящая переменная: {variableNames[basis[leavingRow]]}");
                trace.AppendLine($"Переход: {variableNames[basis[leavingRow]]} -> {variableNames[enteringIndex]}");
                trace.AppendLine();
            }

            basis[leavingRow] = enteringIndex;
        }

        throw new InvalidOperationException("Превышено допустимое количество итераций симплекс-метода.");
    }

    private static string BuildCanonicalForm(
        double[,] matrix,
        double[] rightHandSide,
        double[] objective,
        string[] variableNames)
    {
        var builder = new StringBuilder();
        builder.AppendLine("min f = " + FormatExpression(objective, variableNames));
        builder.AppendLine("при ограничениях:");

        for (var rowIndex = 0; rowIndex < matrix.GetLength(0); rowIndex++)
        {
            builder.AppendLine(
                $"{FormatExpression(GetRow(matrix, rowIndex), variableNames)} = {FormattingHelpers.FormatNumber(rightHandSide[rowIndex])}");
        }

        builder.AppendLine("и условиях неотрицательности:");
        builder.AppendLine(string.Join(", ", variableNames.Select(name => $"{name} >= 0")));

        return builder.ToString().TrimEnd();
    }

    private static double[] RecoverOriginalVariables(
        int originalVariableCount,
        IReadOnlyList<IReadOnlyList<RecoveryTerm>> recoveryTerms,
        double[] canonicalSolution)
    {
        var recovered = new double[originalVariableCount];
        for (var variableIndex = 0; variableIndex < originalVariableCount; variableIndex++)
        {
            foreach (var recoveryTerm in recoveryTerms[variableIndex])
            {
                recovered[variableIndex] += recoveryTerm.Factor * canonicalSolution[recoveryTerm.Index];
            }

            recovered[variableIndex] = Sanitize(recovered[variableIndex]);
        }

        return recovered;
    }

    private static ConstraintRelation Flip(ConstraintRelation relation)
    {
        return relation switch
        {
            ConstraintRelation.LessOrEqual => ConstraintRelation.GreaterOrEqual,
            ConstraintRelation.GreaterOrEqual => ConstraintRelation.LessOrEqual,
            ConstraintRelation.Equal => ConstraintRelation.Equal,
            _ => throw new InvalidOperationException("Неизвестный тип ограничения.")
        };
    }

    private static void AddColumn(
        List<List<double>> existingRows,
        List<double> currentRow,
        List<string> variableNames,
        List<double> objective,
        string name,
        double objectiveCoefficient,
        double currentRowValue)
    {
        foreach (var existingRow in existingRows)
        {
            existingRow.Add(0.0);
        }

        currentRow.Add(currentRowValue);
        variableNames.Add(name);
        objective.Add(objectiveCoefficient);
    }

    private static double[,] ToMatrix(IReadOnlyList<IReadOnlyList<double>> rows)
    {
        if (rows.Count == 0)
        {
            return new double[0, 0];
        }

        var matrix = new double[rows.Count, rows[0].Count];
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            for (var columnIndex = 0; columnIndex < rows[rowIndex].Count; columnIndex++)
            {
                matrix[rowIndex, columnIndex] = rows[rowIndex][columnIndex];
            }
        }

        return matrix;
    }

    private static double[,] GetColumns(double[,] matrix, IReadOnlyList<int> columns)
    {
        var result = new double[matrix.GetLength(0), columns.Count];
        for (var rowIndex = 0; rowIndex < matrix.GetLength(0); rowIndex++)
        {
            for (var columnIndex = 0; columnIndex < columns.Count; columnIndex++)
            {
                result[rowIndex, columnIndex] = matrix[rowIndex, columns[columnIndex]];
            }
        }

        return result;
    }

    private static double[,] SelectColumns(double[,] matrix, IReadOnlyList<int> columns)
    {
        return GetColumns(matrix, columns);
    }

    private static double[,] RemoveRow(double[,] matrix, int removedRow)
    {
        var result = new double[matrix.GetLength(0) - 1, matrix.GetLength(1)];
        var targetRow = 0;
        for (var rowIndex = 0; rowIndex < matrix.GetLength(0); rowIndex++)
        {
            if (rowIndex == removedRow)
            {
                continue;
            }

            for (var columnIndex = 0; columnIndex < matrix.GetLength(1); columnIndex++)
            {
                result[targetRow, columnIndex] = matrix[rowIndex, columnIndex];
            }

            targetRow++;
        }

        return result;
    }

    private static double[] RemoveElement(double[] vector, int removedIndex)
    {
        var result = new double[vector.Length - 1];
        var targetIndex = 0;
        for (var index = 0; index < vector.Length; index++)
        {
            if (index == removedIndex)
            {
                continue;
            }

            result[targetIndex++] = vector[index];
        }

        return result;
    }

    private static double[] GetColumn(double[,] matrix, int columnIndex)
    {
        var column = new double[matrix.GetLength(0)];
        for (var rowIndex = 0; rowIndex < matrix.GetLength(0); rowIndex++)
        {
            column[rowIndex] = matrix[rowIndex, columnIndex];
        }

        return column;
    }

    private static double[] GetRow(double[,] matrix, int rowIndex)
    {
        var row = new double[matrix.GetLength(1)];
        for (var columnIndex = 0; columnIndex < matrix.GetLength(1); columnIndex++)
        {
            row[columnIndex] = matrix[rowIndex, columnIndex];
        }

        return row;
    }

    private static double[,] Invert(double[,] matrix)
    {
        var size = matrix.GetLength(0);
        if (size != matrix.GetLength(1))
        {
            throw new InvalidOperationException("Матрица базиса должна быть квадратной.");
        }

        var augmented = new double[size, size * 2];
        for (var rowIndex = 0; rowIndex < size; rowIndex++)
        {
            for (var columnIndex = 0; columnIndex < size; columnIndex++)
            {
                augmented[rowIndex, columnIndex] = matrix[rowIndex, columnIndex];
                augmented[rowIndex, columnIndex + size] = rowIndex == columnIndex ? 1.0 : 0.0;
            }
        }

        for (var pivotIndex = 0; pivotIndex < size; pivotIndex++)
        {
            var bestRow = pivotIndex;
            var bestValue = Math.Abs(augmented[pivotIndex, pivotIndex]);
            for (var candidate = pivotIndex + 1; candidate < size; candidate++)
            {
                var candidateValue = Math.Abs(augmented[candidate, pivotIndex]);
                if (candidateValue > bestValue)
                {
                    bestValue = candidateValue;
                    bestRow = candidate;
                }
            }

            if (bestValue < Tolerance)
            {
                throw new InvalidOperationException("Не удалось обратить матрицу базиса: базис вырожден или зависим.");
            }

            if (bestRow != pivotIndex)
            {
                SwapRows(augmented, bestRow, pivotIndex);
            }

            var pivot = augmented[pivotIndex, pivotIndex];
            for (var columnIndex = 0; columnIndex < size * 2; columnIndex++)
            {
                augmented[pivotIndex, columnIndex] /= pivot;
            }

            for (var rowIndex = 0; rowIndex < size; rowIndex++)
            {
                if (rowIndex == pivotIndex)
                {
                    continue;
                }

                var factor = augmented[rowIndex, pivotIndex];
                if (Math.Abs(factor) < Tolerance)
                {
                    continue;
                }

                for (var columnIndex = 0; columnIndex < size * 2; columnIndex++)
                {
                    augmented[rowIndex, columnIndex] -= factor * augmented[pivotIndex, columnIndex];
                }
            }
        }

        var inverse = new double[size, size];
        for (var rowIndex = 0; rowIndex < size; rowIndex++)
        {
            for (var columnIndex = 0; columnIndex < size; columnIndex++)
            {
                inverse[rowIndex, columnIndex] = Sanitize(augmented[rowIndex, columnIndex + size]);
            }
        }

        return inverse;
    }

    private static void SwapRows(double[,] matrix, int firstRow, int secondRow)
    {
        for (var columnIndex = 0; columnIndex < matrix.GetLength(1); columnIndex++)
        {
            (matrix[firstRow, columnIndex], matrix[secondRow, columnIndex]) =
                (matrix[secondRow, columnIndex], matrix[firstRow, columnIndex]);
        }
    }

    private static double[] Multiply(double[,] matrix, double[] vector)
    {
        var result = new double[matrix.GetLength(0)];
        for (var rowIndex = 0; rowIndex < matrix.GetLength(0); rowIndex++)
        {
            var sum = 0.0;
            for (var columnIndex = 0; columnIndex < matrix.GetLength(1); columnIndex++)
            {
                sum += matrix[rowIndex, columnIndex] * vector[columnIndex];
            }

            result[rowIndex] = Sanitize(sum);
        }

        return result;
    }

    private static double[] MultiplyRowVector(double[] vector, double[,] matrix)
    {
        var result = new double[matrix.GetLength(1)];
        for (var columnIndex = 0; columnIndex < matrix.GetLength(1); columnIndex++)
        {
            var sum = 0.0;
            for (var rowIndex = 0; rowIndex < matrix.GetLength(0); rowIndex++)
            {
                sum += vector[rowIndex] * matrix[rowIndex, columnIndex];
            }

            result[columnIndex] = Sanitize(sum);
        }

        return result;
    }

    private static double Dot(double[] first, double[] second)
    {
        var sum = 0.0;
        for (var index = 0; index < first.Length; index++)
        {
            sum += first[index] * second[index];
        }

        return Sanitize(sum);
    }

    private static double Sanitize(double value)
    {
        return Math.Abs(value) < Tolerance ? 0.0 : value;
    }

    private static string FormatExpression(double[] coefficients, string[] variableNames)
    {
        var terms = new List<string>();
        for (var index = 0; index < coefficients.Length; index++)
        {
            var coefficient = coefficients[index];
            if (Math.Abs(coefficient) < Tolerance)
            {
                continue;
            }

            var absoluteCoefficient = Math.Abs(coefficient);
            var formattedCoefficient = Math.Abs(absoluteCoefficient - 1.0) < Tolerance
                ? variableNames[index]
                : $"{FormattingHelpers.FormatNumber(absoluteCoefficient)}*{variableNames[index]}";

            if (terms.Count == 0)
            {
                terms.Add(coefficient < 0 ? $"-{formattedCoefficient}" : formattedCoefficient);
            }
            else
            {
                terms.Add(coefficient < 0 ? $"- {formattedCoefficient}" : $"+ {formattedCoefficient}");
            }
        }

        return terms.Count == 0 ? "0" : string.Join(" ", terms);
    }

    private static string FormatVector(double[] values)
    {
        return "[" + string.Join("; ", values.Select(FormattingHelpers.FormatNumber)) + "]";
    }

    private static string FormatNamedVector(double[] values, string[] names)
    {
        var items = new List<string>(values.Length);
        for (var index = 0; index < values.Length; index++)
        {
            var valueText = double.IsPositiveInfinity(values[index])
                ? "inf"
                : FormattingHelpers.FormatNumber(values[index]);
            items.Add($"{names[index]} = {valueText}");
        }

        return string.Join("; ", items);
    }

    private static void AppendIterationHeader(StringBuilder trace, string phaseName, int iteration)
    {
        trace.AppendLine($"{phaseName}, итерация {iteration}");
        trace.AppendLine(new string('-', 60));
    }

    private sealed class CanonicalFormData
    {
        public required double[,] Matrix { get; init; }

        public required double[] RightHandSide { get; init; }

        public required double[] Objective { get; init; }

        public required string[] VariableNames { get; init; }

        public required int[] InitialBasis { get; init; }

        public required int[] ArtificialIndices { get; init; }

        public required List<RecoveryTerm>[] RecoveryTerms { get; init; }

        public required string CanonicalForm { get; init; }

        public required string TransformationSummary { get; init; }
    }

    private sealed class PhaseTwoData
    {
        public required double[,] Matrix { get; init; }

        public required double[] RightHandSide { get; init; }

        public required double[] Objective { get; init; }

        public required string[] VariableNames { get; init; }

        public required int[] InitialBasis { get; init; }

        public required List<RecoveryTerm>[] RecoveryTerms { get; init; }
    }

    private sealed class SimplexPhaseResult
    {
        public required SimplexStatus Status { get; init; }

        public required string Message { get; init; }

        public required double[] Solution { get; init; }

        public required List<int> Basis { get; init; }

        public required double ObjectiveValue { get; init; }
    }

    private readonly record struct RecoveryTerm(int Index, double Factor);
}

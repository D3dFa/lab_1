namespace OptimizationLabApp;

public sealed class SensitivityExperimentRunner
{
    private readonly SimplexSolver solver = new();

    public ExperimentResult Run(LinearProgrammingProblem problem, ExperimentSettings settings)
    {
        if (settings.ExperimentsPerRange <= 0)
        {
            return new ExperimentResult
            {
                Status = SimplexStatus.InvalidInput,
                Message = "Количество экспериментов должно быть положительным."
            };
        }

        var baseResult = solver.Solve(problem, includeTrace: false);
        if (baseResult.Status != SimplexStatus.Optimal)
        {
            return new ExperimentResult
            {
                Status = baseResult.Status,
                Message = "Не удалось запустить эксперимент: базовая задача не решается оптимально."
            };
        }

        var rows = new List<ExperimentSummaryRow>();
        var random = new Random(settings.Seed);
        var startPower = Math.Min(settings.MinPower, settings.MaxPower);
        var endPower = Math.Max(settings.MinPower, settings.MaxPower);
        var baseObjective = baseResult.ObjectiveValue;
        var baseMagnitude = Math.Abs(baseObjective);

        for (var power = startPower; power <= endPower; power++)
        {
            var errorRange = Math.Pow(10.0, power);
            var absoluteErrors = new List<double>(settings.ExperimentsPerRange);
            var relativeErrors = new List<double>(settings.ExperimentsPerRange);

            for (var attempt = 0; attempt < settings.ExperimentsPerRange; attempt++)
            {
                var perturbedRightHandSides = problem.Constraints
                    .Select(constraint => constraint.RightHandSide + NextUniform(random, -errorRange, errorRange))
                    .ToArray();

                var perturbedProblem = problem.CloneWithRightHandSides(perturbedRightHandSides);
                var solveResult = solver.Solve(perturbedProblem, includeTrace: false);
                if (solveResult.Status != SimplexStatus.Optimal)
                {
                    continue;
                }

                var absoluteError = Math.Abs(solveResult.ObjectiveValue - baseObjective);
                var relativeError = baseMagnitude < 1e-12 ? 0.0 : absoluteError / baseMagnitude;

                absoluteErrors.Add(absoluteError);
                relativeErrors.Add(relativeError);
            }

            rows.Add(new ExperimentSummaryRow
            {
                ErrorRange = errorRange,
                MeanAbsoluteError = Mean(absoluteErrors),
                StandardDeviationAbsoluteError = StandardDeviation(absoluteErrors),
                MeanRelativeError = Mean(relativeErrors),
                StandardDeviationRelativeError = StandardDeviation(relativeErrors),
                SuccessfulRuns = absoluteErrors.Count
            });
        }

        return new ExperimentResult
        {
            Status = SimplexStatus.Optimal,
            Message = "Эксперимент успешно завершён.",
            BaseObjectiveValue = baseObjective,
            Rows = rows
        };
    }

    private static double NextUniform(Random random, double minValue, double maxValue)
    {
        return minValue + random.NextDouble() * (maxValue - minValue);
    }

    private static double Mean(IReadOnlyList<double> values)
    {
        if (values.Count == 0)
        {
            return 0.0;
        }

        var sum = 0.0;
        foreach (var value in values)
        {
            sum += value;
        }

        return sum / values.Count;
    }

    private static double StandardDeviation(IReadOnlyList<double> values)
    {
        if (values.Count == 0)
        {
            return 0.0;
        }

        var mean = Mean(values);
        var sum = 0.0;
        foreach (var value in values)
        {
            sum += Math.Pow(value - mean, 2);
        }

        return Math.Sqrt(sum / values.Count);
    }
}

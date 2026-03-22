using System.Globalization;

namespace OptimizationLabApp;

public enum ObjectiveSense
{
    Maximize,
    Minimize
}

public enum ConstraintRelation
{
    LessOrEqual,
    Equal,
    GreaterOrEqual
}

public enum VariableBoundType
{
    NonNegative,
    Free
}

public enum SimplexStatus
{
    Optimal,
    Unbounded,
    Infeasible,
    InvalidInput
}

public sealed class ConstraintInput
{
    public required double[] Coefficients { get; init; }

    public required ConstraintRelation Relation { get; init; }

    public required double RightHandSide { get; init; }
}

public sealed class LinearProgrammingProblem
{
    public required ObjectiveSense Sense { get; init; }

    public required string[] VariableNames { get; init; }

    public required double[] ObjectiveCoefficients { get; init; }

    public required VariableBoundType[] VariableBounds { get; init; }

    public required IReadOnlyList<ConstraintInput> Constraints { get; init; }

    public LinearProgrammingProblem CloneWithRightHandSides(double[] rightHandSides)
    {
        if (rightHandSides.Length != Constraints.Count)
        {
            throw new ArgumentException("Количество правых частей должно совпадать с числом ограничений.");
        }

        var clonedConstraints = new List<ConstraintInput>(Constraints.Count);
        for (var index = 0; index < Constraints.Count; index++)
        {
            clonedConstraints.Add(new ConstraintInput
            {
                Coefficients = (double[])Constraints[index].Coefficients.Clone(),
                Relation = Constraints[index].Relation,
                RightHandSide = rightHandSides[index]
            });
        }

        return new LinearProgrammingProblem
        {
            Sense = Sense,
            VariableNames = (string[])VariableNames.Clone(),
            ObjectiveCoefficients = (double[])ObjectiveCoefficients.Clone(),
            VariableBounds = (VariableBoundType[])VariableBounds.Clone(),
            Constraints = clonedConstraints
        };
    }
}

public sealed class SimplexSolveResult
{
    public required SimplexStatus Status { get; init; }

    public required string Message { get; init; }

    public double ObjectiveValue { get; init; }

    public string[] VariableNames { get; init; } = Array.Empty<string>();

    public double[] VariableValues { get; init; } = Array.Empty<double>();

    public string CanonicalForm { get; init; } = string.Empty;

    public string TransformationSummary { get; init; } = string.Empty;

    public string IterationLog { get; init; } = string.Empty;
}

public sealed class ExperimentSettings
{
    public required int ExperimentsPerRange { get; init; }

    public required int MinPower { get; init; }

    public required int MaxPower { get; init; }

    public required int Seed { get; init; }
}

public sealed class ExperimentSummaryRow
{
    public required double ErrorRange { get; init; }

    public required double MeanAbsoluteError { get; init; }

    public required double StandardDeviationAbsoluteError { get; init; }

    public required double MeanRelativeError { get; init; }

    public required double StandardDeviationRelativeError { get; init; }

    public required int SuccessfulRuns { get; init; }
}

public sealed class ExperimentResult
{
    public required SimplexStatus Status { get; init; }

    public required string Message { get; init; }

    public double BaseObjectiveValue { get; init; }

    public IReadOnlyList<ExperimentSummaryRow> Rows { get; init; } = Array.Empty<ExperimentSummaryRow>();
}

public static class SampleProblems
{
    public static LinearProgrammingProblem CreateLabProblem()
    {
        return new LinearProgrammingProblem
        {
            Sense = ObjectiveSense.Maximize,
            VariableNames = ["x1", "x2", "x3", "x4", "x5"],
            ObjectiveCoefficients = [-1.0, -1.0, -1.0, -1.0, 2.0],
            VariableBounds =
            [
                VariableBoundType.NonNegative,
                VariableBoundType.NonNegative,
                VariableBoundType.NonNegative,
                VariableBoundType.Free,
                VariableBoundType.Free
            ],
            Constraints =
            [
                new ConstraintInput
                {
                    Coefficients = [1.0, 1.0, 1.0, 1.0, 1.0],
                    Relation = ConstraintRelation.Equal,
                    RightHandSide = 1.0
                },
                new ConstraintInput
                {
                    Coefficients = [1.0, -1.0, -1.0, 1.0, 1.0],
                    Relation = ConstraintRelation.Equal,
                    RightHandSide = 1.0
                },
                new ConstraintInput
                {
                    Coefficients = [6.0, 2.0, -1.0, 1.0, -3.0],
                    Relation = ConstraintRelation.GreaterOrEqual,
                    RightHandSide = -1.0
                },
                new ConstraintInput
                {
                    Coefficients = [1.0, -3.0, -5.0, -4.0, 2.0],
                    Relation = ConstraintRelation.LessOrEqual,
                    RightHandSide = 1.0
                }
            ]
        };
    }
}

public static class FormattingHelpers
{
    public static string FormatNumber(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        var absoluteValue = Math.Abs(value);
        if ((absoluteValue > 0 && absoluteValue < 0.001) || absoluteValue >= 10000)
        {
            return value.ToString("0.###E+0", CultureInfo.InvariantCulture);
        }

        return value.ToString("0.######", CultureInfo.InvariantCulture);
    }
}

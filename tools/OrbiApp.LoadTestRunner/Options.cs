namespace OrbiApp.LoadTestRunner;

public sealed record RunnerOptions(
    string Command,
    int MaxVus = 100,
    int Replicas = 1,
    bool AllowStress = false,
    bool LabOnly = false,
    bool ConfirmMultiReplica = false,
    bool ConfirmPodDelete = false,
    string? FromImage = null,
    string? ToImage = null,
    string InputPath = "tests/k6/results")
{
    public static RunnerOptions Parse(IReadOnlyList<string> args)
    {
        if (args.Count == 0 || args[0] is "help" or "--help" or "-h")
            return new RunnerOptions("help");

        var command = args[0].ToLowerInvariant();
        var maxVus = 100;
        var replicas = 1;
        var allowStress = false;
        var labOnly = false;
        var confirmMulti = false;
        var confirmDelete = false;
        string? from = null;
        string? to = null;
        var input = "tests/k6/results";

        for (var index = 1; index < args.Count; index++)
        {
            var arg = args[index];
            switch (arg)
            {
                case "--max-vus":
                    maxVus = ParseInt(args, ref index, arg);
                    break;
                case "--replicas":
                    replicas = ParseInt(args, ref index, arg);
                    break;
                case "--allow-stress":
                    allowStress = true;
                    break;
                case "--lab-only":
                    labOnly = true;
                    break;
                case "--confirm-multi-replica":
                    confirmMulti = true;
                    break;
                case "--confirm-pod-delete":
                    confirmDelete = true;
                    break;
                case "--from":
                    from = ParseString(args, ref index, arg);
                    break;
                case "--to":
                    to = ParseString(args, ref index, arg);
                    break;
                case "--input":
                    input = ParseString(args, ref index, arg);
                    break;
                default:
                    throw new ArgumentException($"Opción no reconocida: {arg}");
            }
        }

        if (maxVus is not (20 or 50 or 100 or 200 or 500))
            throw new ArgumentException("--max-vus debe ser 20, 50, 100, 200 o 500.");
        if (replicas is not (1 or 3 or 10))
            throw new ArgumentException("--replicas debe ser 1, 3 o 10.");
        if (maxVus > 100 && (!allowStress || !labOnly))
            throw new ArgumentException("200/500 VUs requieren --allow-stress y --lab-only.");
        if (command == "availability" && (!confirmDelete || !labOnly))
            throw new ArgumentException("availability requiere --confirm-pod-delete y --lab-only.");
        if (command == "rollout" && (!labOnly || string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to)))
            throw new ArgumentException("rollout requiere --from, --to y --lab-only.");

        return new RunnerOptions(command, maxVus, replicas, allowStress, labOnly,
            confirmMulti, confirmDelete, from, to, input);
    }

    private static int ParseInt(IReadOnlyList<string> args, ref int index, string name)
    {
        if (index + 1 >= args.Count || !int.TryParse(args[++index], out var result))
            throw new ArgumentException($"{name} requiere un entero.");
        return result;
    }

    private static string ParseString(IReadOnlyList<string> args, ref int index, string name)
    {
        if (index + 1 >= args.Count || string.IsNullOrWhiteSpace(args[++index]))
            throw new ArgumentException($"{name} requiere un valor.");
        return args[index];
    }
}

public sealed record MatrixCase(int Replicas, int Vus, int DurationSeconds = 30);

public static class MatrixPlanner
{
    public static IReadOnlyList<MatrixCase> Plan(int maxVus)
    {
        if (maxVus is not (20 or 50 or 100 or 200 or 500))
            throw new ArgumentOutOfRangeException(nameof(maxVus));

        int[] replicas = [1, 3, 10];
        int[] allVus = [20, 50, 100, 200, 500];
        return replicas
            .SelectMany(replicaCount => allVus.Where(vus => vus <= maxVus)
                .Select(vus => new MatrixCase(replicaCount, vus)))
            .ToArray();
    }
}
